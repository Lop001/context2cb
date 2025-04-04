﻿using System.CommandLine;
using System.Text.Json;
using System.Text;
using System.Diagnostics;
using ContextCli.Services;

namespace ContextCli
{
    class Program
    {
        // --- Main entry point ---
        static async Task<int> Main(string[] args)
        {
            // === Command and option definitions ===

            // -- Main command (run in directory or with a single file) --
            var rootCommand = new RootCommand("ContextCli: Copies project context (file structure and contents) to the clipboard for AI prompts.")
            {
                // Argument for optional single file input
                new Argument<FileInfo?>("file", () => null, "Optional path to a single file to copy its content.")
            };

            // -- Global options (valid for main command) --
            var ignoreOption = new Option<string[]>(
                aliases: new[] { "--ignore", "-i" },
                description: "Glob patterns for files/directories to ignore (can be used multiple times).")
            { AllowMultipleArgumentsPerToken = true }; // Allows -i pattern1 pattern2

            var noIgnoreOption = new Option<bool>(
                "--no-ignore",
                description: "Temporarily ignore .gitignore and configured ignore patterns.");

            var depthOption = new Option<int?>( // nullable int
                aliases: new[] { "--depth", "-d" },
                description: "Limit the depth of directory scanning.");

            var stdoutOption = new Option<bool>(
                aliases: new[] { "--stdout", "-s" },
                description: "Print the output to stdout instead of copying to clipboard.");

            var maxSizeOption = new Option<int?>( // nullable int
                "--max-size",
                description: "Ignore files larger than this size in KB (overrides config).");

            var noContentOption = new Option<bool>(
                "--no-content",
                description: "Copy only the file structure tree, without file contents.");

            rootCommand.AddGlobalOption(ignoreOption);
            rootCommand.AddGlobalOption(noIgnoreOption);
            rootCommand.AddGlobalOption(depthOption);
            rootCommand.AddGlobalOption(stdoutOption);
            rootCommand.AddGlobalOption(maxSizeOption);
            rootCommand.AddGlobalOption(noContentOption);

            // -- Handler for main command --
            rootCommand.SetHandler(async (context) =>
            {
                var fileInfo = context.ParseResult.GetValueForArgument(rootCommand.Arguments.OfType<Argument<FileInfo?>>().First());
                var ignorePatterns = context.ParseResult.GetValueForOption(ignoreOption);
                var noIgnore = context.ParseResult.GetValueForOption(noIgnoreOption);
                var depth = context.ParseResult.GetValueForOption(depthOption);
                var useStdout = context.ParseResult.GetValueForOption(stdoutOption);
                var maxSize = context.ParseResult.GetValueForOption(maxSizeOption);
                var noContent = context.ParseResult.GetValueForOption(noContentOption);

                await HandleMainExecution(fileInfo, ignorePatterns ?? Array.Empty<string>(), noIgnore, depth, useStdout, maxSize, noContent);
            });


            // === 'config' command ===
            var configCommand = new Command("config", "Manage the .contextcli.json configuration file.");

            // -- Subcommand 'config --edit' --
            var editCommand = new Command("edit", "Open the configuration file in the default editor.")
            {
                new Option<bool>(aliases: new[] { "-e", "--edit-alias" }, "Alias for edit command") // Alias just for demonstration, not necessary
            };
            editCommand.SetHandler(HandleConfigEdit);
            configCommand.AddCommand(editCommand);

            // -- Subcommand 'config --show' --
            var showCommand = new Command("show", "Show the effective configuration.");
            showCommand.SetHandler(HandleConfigShow);
            configCommand.AddCommand(showCommand);

            // -- Subcommand 'config --path' --
            var pathCommand = new Command("path", "Show the path to the used configuration file.");
            pathCommand.SetHandler(HandleConfigPath);
            configCommand.AddCommand(pathCommand);

            // -- Subcommand 'config --init' --
            var initCommand = new Command("init", "Create a default configuration file in the current directory.");
            initCommand.SetHandler(HandleConfigInit);
            configCommand.AddCommand(initCommand);

            // -- Subcommand 'config --list-extensions' --
            var listExtensionsCommand = new Command("list-extensions", "List the configured file extensions.")
            {
                new Option<bool>(aliases: new[] { "-le", "--list-alias" }, "Alias for list-extensions")
            };
            listExtensionsCommand.SetHandler(HandleConfigListExtensions);
            configCommand.AddCommand(listExtensionsCommand);


            // -- Subcommand 'config --add-extension' --
            var addExtensionCommand = new Command("add-extension", "Add one or more extensions to the configuration.")
            {
                new Argument<string[]>("extensions", "Extensions to add (e.g., .cshtml .razor)") { Arity = ArgumentArity.OneOrMore },
                 new Option<bool>(aliases: new[] { "-ae", "--add-alias" }, "Alias for add-extension")
            };
            addExtensionCommand.SetHandler(async (context) =>
            {
                var extensionsToAdd = context.ParseResult.GetValueForArgument(addExtensionCommand.Arguments.OfType<Argument<string[]>>().First());
                await HandleConfigAddExtensions(extensionsToAdd);
            });
            configCommand.AddCommand(addExtensionCommand);


            // -- Subcommand 'config --remove-extension' --
            var removeExtensionCommand = new Command("remove-extension", "Remove one or more extensions from the configuration.")
            {
                new Argument<string[]>("extensions", "Extensions to remove (e.g., .txt .log)") { Arity = ArgumentArity.OneOrMore },
                 new Option<bool>(aliases: new[] { "-re", "--remove-alias" }, "Alias for remove-extension")
            };
            removeExtensionCommand.SetHandler(async (context) =>
            {
                var extensionsToRemove = context.ParseResult.GetValueForArgument(removeExtensionCommand.Arguments.OfType<Argument<string[]>>().First());
                await HandleConfigRemoveExtensions(extensionsToRemove);
            });
            configCommand.AddCommand(removeExtensionCommand);


            // -- Subcommand 'config --reset-extensions' --
            var resetExtensionsCommand = new Command("reset-extensions", "Reset extensions to the default list.");
            resetExtensionsCommand.SetHandler(HandleConfigResetExtensions);
            configCommand.AddCommand(resetExtensionsCommand);

            // Adding 'config' command to the main command
            rootCommand.AddCommand(configCommand);

            // === Run parsing and execution ===
            return await rootCommand.InvokeAsync(args);
        }

        /// <summary>
        /// Generates prefix for a line in the tree (indentation and vertical lines).
        /// </summary>
        /// <param name="parentIsLastStack">Stack of boolean values where true means that the parent at that level was the last element.</param>
        /// <returns>String with prefix for the tree.</returns>
        private static string GetTreePrefix(List<bool> parentIsLastStack)
        {
            StringBuilder prefixBuilder = new StringBuilder();
            // Process the stack of parents (excluding current level)
            for (int i = 0; i < parentIsLastStack.Count; i++)
            {
                // If the parent at level 'i' was the last one, we don't draw a vertical line, just indentation.
                // Otherwise we draw a vertical line │.
                prefixBuilder.Append(parentIsLastStack[i] ? "    " : "│   ");
            }
            return prefixBuilder.ToString();
        }

        // --- Main Logic  ---

        private static async Task HandleMainExecution(FileInfo? fileInfo, string[] cliIgnorePatterns, bool noIgnore, int? depth, bool useStdout, int? cliMaxSizeKb, bool noContent)
        {
            var stopwatch = Stopwatch.StartNew(); // Measuring time

            // --- Single file mode ---
            if (fileInfo != null)
            {
                Console.WriteLine($"Processing single file: {fileInfo.FullName}");
                if (!fileInfo.Exists)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error: File not found: {fileInfo.FullName}");
                    Console.ResetColor();
                    return; // Exit with error (or return error code)
                }

                try
                {
                    string content = await File.ReadAllTextAsync(fileInfo.FullName);
                    // Optionally add header with file name
                    string output = $"# File: {fileInfo.Name}\n\n```\n{content}\n```";
                    OutputService.OutputResult(output, useStdout);
                    Console.WriteLine($"Processed in {stopwatch.ElapsedMilliseconds} ms.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error reading file: {ex.Message}");
                    Console.ResetColor();
                }
                return;
            }

            // --- Directory mode ---
            Console.WriteLine($"Processing directory: {Environment.CurrentDirectory}");

            // 1. Load effective configuration (config + CLI overrides)
            var config = await ConfigurationService.GetEffectiveConfigurationAsync();
            ApplyCliOverrides(config, noIgnore, cliMaxSizeKb);
            Console.WriteLine($"Effective MaxFileSize: {(config.MaxFileSizeKb.HasValue ? config.MaxFileSizeKb + " KB" : "Unlimited")}");
            Console.WriteLine($"Effective UseGitignore: {config.UseGitignore}");

            // 2. Build Glob matcher for ignoring
            var ignoreMatchers = await IgnoreService.BuildIgnoreMatchersAsync(config, cliIgnorePatterns);

            // 3. Prepare HashSet for quick extension checking (case-insensitive)
            var allowedExtensions = new HashSet<string>(config.Extensions ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            Console.WriteLine($"Allowed extensions: {allowedExtensions.Count}");


            // 4. Start recursive scanning
            var includedFiles = new List<FileInfo>();
            var treeBuilder = new StringBuilder();
            // Root directory (dot) is outside recursive call, we add it manually
            // or we omit it and start directly with content. Let's omit it.
            // treeBuilder.AppendLine("."); // Removed - tree will start with content

            Console.WriteLine("Scanning directory structure...");
            await DirectoryScannerService.ScanDirectoryRecursiveAsync(
                new DirectoryInfo(Environment.CurrentDirectory),
                depth ?? int.MaxValue, // Effective maximum depth
                ignoreMatchers,
                allowedExtensions,
                config.MaxFileSizeKb * 1024L ?? long.MaxValue, // Max size in bytes
                includedFiles,
                treeBuilder,
                new List<bool>() // Start with empty stack for level 0
            );

            Console.WriteLine($"Found {includedFiles.Count} files matching criteria.");

            // 5. Generate final output
            var finalOutput = new StringBuilder();

            // Structure section
            finalOutput.AppendLine("# Project Files Structure");
            finalOutput.AppendLine();
            finalOutput.Append(treeBuilder.ToString()); // Add generated tree
            finalOutput.AppendLine(); // Empty line for separation

            // Content section (if not --no-content)
            if (!noContent && includedFiles.Any())
            {
                Console.WriteLine("Reading file contents...");
                finalOutput.AppendLine("---");
                finalOutput.AppendLine();
                finalOutput.AppendLine("# File Contents");
                finalOutput.AppendLine();

                // Sort files for consistent output
                includedFiles.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

                foreach (var file in includedFiles)
                {
                    await DirectoryScannerService.AppendFileContentAsync(file, finalOutput);
                }
            }
            else if (noContent)
            {
                 Console.WriteLine("Skipping file contents (--no-content).");
            }


            // 6. Send result to clipboard or stdout
            OutputService.OutputResult(finalOutput.ToString(), useStdout);
            stopwatch.Stop();
            Console.WriteLine($"Processed in {stopwatch.ElapsedMilliseconds} ms.");
        }

        // --- Helper methods for HandleMainExecution (Directory Mode) ---

        /// <summary>
        /// Applies configuration overrides from CLI arguments.
        /// </summary>
        private static void ApplyCliOverrides(CliConfiguration config, bool noIgnoreCli, int? cliMaxSizeKb)
        {
            if (noIgnoreCli)
            {
                config.UseGitignore = false;
                // We can decide whether --no-ignore also disables config.IgnorePatterns
                // config.IgnorePatterns.Clear(); // Option: --no-ignore disables ALL ignore patterns
                Console.WriteLine("Note: --no-ignore flag is active, ignoring .gitignore files.");
            }
            if (cliMaxSizeKb.HasValue)
            {
                config.MaxFileSizeKb = cliMaxSizeKb;
            }
        }

    
        // --- Methods for 'config' commands ---

        private static Task HandleConfigEdit()
        {
            Console.WriteLine("Executing: config --edit");
            string? configPath = ConfigurationService.FindConfigFile();
            string filePathToEdit;

            if (configPath == null)
            {
                filePathToEdit = Path.Combine(Environment.CurrentDirectory, CliConfiguration.ConfigFileName);
                Console.WriteLine($"Configuration file not found. Creating default '{CliConfiguration.ConfigFileName}' in the current directory for editing.");
                // Create default config so the editor has something to open
                var defaultConfig = CliConfiguration.GetDefault();
                // Using synchronous variant here for simplicity, or make the whole method async Task
                 ConfigurationService.SaveConfigurationAsync(defaultConfig, filePathToEdit).GetAwaiter().GetResult();
                 // It would be better to make the HandleConfigEdit method an async Task
            }
            else
            {
                 filePathToEdit = configPath;
                 Console.WriteLine($"Found configuration file at: {filePathToEdit}");
            }

            Console.WriteLine("Attempting to open in default editor...");

            try
            {
                // Editor detection (simplified)
                string? editor = Environment.GetEnvironmentVariable("EDITOR") ?? Environment.GetEnvironmentVariable("VISUAL");
                System.Diagnostics.Process process = new System.Diagnostics.Process();

                if (!string.IsNullOrEmpty(editor))
                {
                    // If editor is defined, use it. We need to properly split arguments if they're included.
                    // This is simplified; a real solution might need to parse editor arguments.
                     process.StartInfo.FileName = editor.Split(' ')[0]; // Take the first part as the command
                     process.StartInfo.Arguments = $"\"{filePathToEdit}\""; // Pass the file path in quotes
                     // Possibly add the rest of the arguments from 'editor', if they exist
                }
                else if (OperatingSystem.IsWindows())
                {
                     process.StartInfo.FileName = "notepad.exe";
                     process.StartInfo.Arguments = $"\"{filePathToEdit}\"";
                     process.StartInfo.UseShellExecute = true; // Notepad runs better through shell
                }
                else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    // Try to find common editors in PATH
                    string[] commonEditors = { "nano", "vim", "vi", "code", "gedit", "open" }; // 'open' on macOS
                    string? foundEditor = commonEditors.FirstOrDefault(e => Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator).Any(p => File.Exists(Path.Combine(p, e))) ?? false);

                    if(foundEditor != null) {
                        process.StartInfo.FileName = foundEditor;
                        process.StartInfo.Arguments = $"\"{filePathToEdit}\"";
                        if (foundEditor == "open" && OperatingSystem.IsMacOS()) // 'open' on macOS only needs the path
                           process.StartInfo.Arguments = $"\"{filePathToEdit}\"";

                         // For terminal editors (nano, vim), we need to ensure they run within the current terminal
                         // UseShellExecute=false is often needed for direct execution
                         process.StartInfo.UseShellExecute = false; // Try without shell for terminal editors
                         // More complex terminal interaction might be needed here
                    } else {
                         Console.Error.WriteLine("Error: Could not find a default editor (set EDITOR/VISUAL env var or ensure nano/vim/code is in PATH).");
                         return Task.CompletedTask;
                    }
                }
                else
                {
                     Console.Error.WriteLine("Error: Unsupported OS for editor detection.");
                     return Task.CompletedTask;
                }


                if (process.Start())
                {
                     Console.WriteLine($"Launched editor '{process.StartInfo.FileName}'. Waiting for exit...");
                     // We need to wait until the user closes the editor
                     // WaitForExit can be blocking; for async use WaitForExitAsync()
                     process.WaitForExit(); // In async method use await process.WaitForExitAsync();
                     Console.WriteLine("Editor closed.");
                     // TODO: Optionally validate JSON after editing
                }
                else
                {
                    Console.Error.WriteLine("Error: Failed to start the editor process.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error launching editor: {ex.Message}");
            }

            return Task.CompletedTask; // Should be async Task
        }

        private static async Task HandleConfigShow()
        {
            Console.WriteLine("Executing: config --show");
            Console.WriteLine("Effective configuration:");
            var config = await ConfigurationService.GetEffectiveConfigurationAsync();
            string jsonOutput = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(jsonOutput);
        }

        private static Task HandleConfigPath()
        {
            Console.WriteLine("Executing: config --path");
            string? configPath = ConfigurationService.FindConfigFile();
            if (configPath != null)
            {
                Console.WriteLine($"Configuration file found at: {configPath}");
            }
            else
            {
                Console.WriteLine("No configuration file (.contextcli.json) found in the current directory or parent directories.");
            }
            return Task.CompletedTask;
        }

        private static async Task HandleConfigInit()
        {
            Console.WriteLine("Executing: config --init");
            string targetPath = Path.Combine(Environment.CurrentDirectory, CliConfiguration.ConfigFileName);

            if (File.Exists(targetPath))
            {
                Console.WriteLine($"Configuration file '{targetPath}' already exists.");
                // Ask whether to overwrite? Or just exit? For now, we'll just exit.
                // Console.Write("Overwrite? (y/N): ");
                // string response = Console.ReadLine() ?? "";
                // if (!response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)) return;
                return;
            }

            // Check if a config exists higher up - just for information, we'll still create one here
            string? higherPath = ConfigurationService.FindConfigFile();
            if (higherPath != null && Path.GetDirectoryName(higherPath) != Environment.CurrentDirectory) {
                 Console.WriteLine($"Note: Another config file exists higher up at '{higherPath}'.");
            }


            Console.WriteLine($"Creating default configuration file at '{targetPath}'...");
            var defaultConfig = CliConfiguration.GetDefault();
            bool success = await ConfigurationService.SaveConfigurationAsync(defaultConfig, targetPath);

            if (success)
            {
                Console.WriteLine("✅ Default configuration file created successfully.");
            }
            else
            {
                // Error was already printed in SaveConfigurationAsync
            }
        }

        private static async Task HandleConfigListExtensions()
        {
             Console.WriteLine("Executing: config --list-extensions");
             var config = await ConfigurationService.GetEffectiveConfigurationAsync();
             Console.WriteLine("Configured extensions:");
             if (config.Extensions.Any())
             {
                 foreach (var ext in config.Extensions)
                 {
                     Console.WriteLine($"- {ext}");
                 }
             }
             else
             {
                 Console.WriteLine("(No extensions configured)");
             }
        }

         private static async Task HandleConfigAddExtensions(string[] extensionsToAdd)
        {
            Console.WriteLine($"Executing: config --add-extension {string.Join(" ", extensionsToAdd)}");
            string? configPath = ConfigurationService.FindConfigFile();
            if (configPath == null)
            {
                Console.Error.WriteLine($"Error: No configuration file ({CliConfiguration.ConfigFileName}) found to modify.");
                Console.Error.WriteLine($"Run 'contextcli config --init' to create one first.");
                return;
            }

            Console.WriteLine($"Modifying configuration file: {configPath}");
            var config = await ConfigurationService.LoadConfigurationAsync(configPath);
            if (config == null) return; // Error was already printed during loading

            int addedCount = 0;
            config.Extensions ??= new List<string>(); // Safety check

            foreach (var ext in extensionsToAdd)
            {
                 // Normalize (e.g., add a dot if missing and it's not a special name)
                 string normalizedExt = ext.StartsWith(".") || !Path.HasExtension(ext) ? ext : "." + ext;
                 normalizedExt = normalizedExt.ToLowerInvariant(); // Store in lowercase? Or preserve case?

                if (!config.Extensions.Contains(normalizedExt, StringComparer.OrdinalIgnoreCase))
                {
                    config.Extensions.Add(normalizedExt);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                bool success = await ConfigurationService.SaveConfigurationAsync(config, configPath);
                if (success)
                {
                     Console.WriteLine($"✅ Successfully added {addedCount} extension(s).");
                }
            }
            else
            {
                Console.WriteLine("No new extensions were added (they might already exist).");
            }
        }

        private static async Task HandleConfigRemoveExtensions(string[] extensionsToRemove)
        {
            Console.WriteLine($"Executing: config --remove-extension {string.Join(" ", extensionsToRemove)}");
             string? configPath = ConfigurationService.FindConfigFile();
            if (configPath == null)
            {
                Console.Error.WriteLine($"Error: No configuration file ({CliConfiguration.ConfigFileName}) found to modify.");
                Console.Error.WriteLine($"Run 'contextcli config --init' to create one first.");
                return;
            }

             Console.WriteLine($"Modifying configuration file: {configPath}");
            var config = await ConfigurationService.LoadConfigurationAsync(configPath);
            if (config == null || config.Extensions == null) return;

            int removedCount = 0;
            foreach (var ext in extensionsToRemove)
            {
                string normalizedExt = ext.StartsWith(".") || !Path.HasExtension(ext) ? ext : "." + ext;
                 normalizedExt = normalizedExt.ToLowerInvariant();

                // Remove all occurrences (case-insensitive)
                int removed = config.Extensions.RemoveAll(existing => existing.Equals(normalizedExt, StringComparison.OrdinalIgnoreCase));
                removedCount += removed;
            }


            if (removedCount > 0)
            {
                bool success = await ConfigurationService.SaveConfigurationAsync(config, configPath);
                if (success)
                {
                     Console.WriteLine($"✅ Successfully removed {removedCount} extension(s).");
                }
            }
            else
            {
                Console.WriteLine("No extensions were removed (they might not have existed).");
            }
        }

        private static async Task HandleConfigResetExtensions()
        {
             Console.WriteLine("Executing: config --reset-extensions");
              string? configPath = ConfigurationService.FindConfigFile();
            if (configPath == null)
            {
                Console.Error.WriteLine($"Error: No configuration file ({CliConfiguration.ConfigFileName}) found to modify.");
                Console.Error.WriteLine($"Run 'contextcli config --init' to create one first.");
                return;
            }

            Console.WriteLine($"Modifying configuration file: {configPath}");
            var config = await ConfigurationService.LoadConfigurationAsync(configPath);
            if (config == null) return;

            config.Extensions = CliConfiguration.GetDefaultExtensions(); // Replace list with default

            bool success = await ConfigurationService.SaveConfigurationAsync(config, configPath);
            if (success)
            {
                 Console.WriteLine($"✅ Successfully reset extensions to default.");
            }
        }

           private static string GenerateMockResult()
        {
            // Just for demonstration, will be replaced with actual implementation
            // Using verbatim string literal (@"...") for easy insertion of multi-line text
            // and doubled quotes ("") to represent quotes inside the string.
            return @"
# Project Files Structure (Mock)

.
├── src/
│   └── app.cs
└── config/
    └── settings.json

---

# File Contents (Mock)

## ./src/app.cs
```csharp
// Mock content for app.cs
Console.WriteLine(""Hello, World!""); 


}";
        }
    }
}
