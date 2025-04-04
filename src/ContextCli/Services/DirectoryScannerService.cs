using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ContextCli.Utils;
using DotNet.Globbing;

namespace ContextCli.Services
{
    /// <summary>
    /// Service for scanning directories and building file structure
    /// </summary>
    public static class DirectoryScannerService
    {
        /// <summary>
        /// Recursively scans a directory and builds a tree structure
        /// </summary>
        public static async Task ScanDirectoryRecursiveAsync(
            DirectoryInfo directory,
            int maxDepth,
            List<Glob>? ignoreMatchers,
            HashSet<string> allowedExtensions,
            long maxFileSizeBytes,
            List<FileInfo> includedFiles,
            StringBuilder treeBuilder,
            List<bool> parentIsLastStack)
        {
            if (maxDepth <= 0) return; // Reached max depth

            // Get all files and subdirectories
            FileInfo[] files;
            DirectoryInfo[] subDirectories;

            try
            {
                files = directory.GetFiles();
                subDirectories = directory.GetDirectories();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException)
            {
                // Print error only once for the entire directory
                if (parentIsLastStack.Count > 0) // Don't print error for root directory if it fails right at the start
                {
                    string errorPrefix = StringUtils.GetTreePrefix(parentIsLastStack);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Error.WriteLine($"{errorPrefix}└── [!] Cannot access: {directory.Name}/ ({ex.GetType().Name})");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Error.WriteLine($"[!] Warning: Cannot access contents of {directory.FullName}. Skipping. ({ex.GetType().Name})");
                    Console.ResetColor();
                }
                return;
            }

            // Combine files and directories into a single list for easier determination of the last element
            var items = new List<FileSystemInfo>(files.Length + subDirectories.Length);
            items.AddRange(files);
            items.AddRange(subDirectories);

            // --- Process all items (files and directories) ---
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                bool isLastItem = (i == items.Count - 1); // Is this item the last one in the current directory?

                // Filters for ignoring (common for files and directories)
                if (item.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue; // Ignore symlinks

                string relativePath = Path.GetRelativePath(Environment.CurrentDirectory, item.FullName).Replace(Path.DirectorySeparatorChar, '/');
                string matchPath = item is DirectoryInfo ? relativePath + "/" : relativePath; // Add slash for directories
                
                // Check if path matches any ignore pattern
                if (IgnoreService.ShouldIgnorePath(matchPath, ignoreMatchers)) continue;

                // --- Specific processing for files ---
                if (item is FileInfo file)
                {
                    // Additional filters for files (extension, size, binary)
                    string extension = file.Extension;
                    string fileName = file.Name;
                    bool extensionAllowed = allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
                                        allowedExtensions.Contains(fileName, StringComparer.OrdinalIgnoreCase);
                    if (!extensionAllowed) continue;

                    if (file.Length > maxFileSizeBytes) continue;
                    
                    if (await FileUtils.IsLikelyBinaryAsync(file)) continue;

                    // === File passed all filters ===
                    includedFiles.Add(file);

                    // Add file to the tree
                    string prefix = StringUtils.GetTreePrefix(parentIsLastStack);
                    string connector = isLastItem ? "└──" : "├──";
                    treeBuilder.AppendLine($"{prefix}{connector} {file.Name}");
                }
                // --- Specific processing for directories ---
                else if (item is DirectoryInfo subDir)
                {
                    // === Directory passed filters ===

                    // Add directory to the tree
                    string prefix = StringUtils.GetTreePrefix(parentIsLastStack);
                    string connector = isLastItem ? "└──" : "├──";
                    treeBuilder.AppendLine($"{prefix}{connector} {subDir.Name}/");

                    // Recursive call for subdirectory
                    var nextStack = new List<bool>(parentIsLastStack); // Create a copy of the stack
                    nextStack.Add(isLastItem); // Add information whether THIS directory was the last one

                    await ScanDirectoryRecursiveAsync(
                        subDir,
                        maxDepth - 1, // Decrease depth for next level
                        ignoreMatchers,
                        allowedExtensions,
                        maxFileSizeBytes,
                        includedFiles,
                        treeBuilder, // Continue with the same builder
                        nextStack // Pass the new stack
                    );
                }
            }
        }

        /// <summary>
        /// Adds file content to StringBuilder, formatted as a Markdown code block.
        /// </summary>
        public static async Task AppendFileContentAsync(FileInfo file, StringBuilder outputBuilder)
        {
            string relativePath = Path.GetRelativePath(Environment.CurrentDirectory, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
            string language = FileUtils.GetLanguageFromExtension(file.Extension);

            outputBuilder.AppendLine($"## ./{relativePath}"); // Use relative path with ./
            outputBuilder.AppendLine($"```{language}"); // Start code block with language

            try
            {
                // Read file line by line for better processing of large files (even with a limit)
                using var reader = file.OpenText(); // StreamReader - detects encoding
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    outputBuilder.AppendLine(line);
                }
            }
            catch (Exception ex)
            {
                outputBuilder.AppendLine($"[Error reading file: {ex.Message}]");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine($"Warning: Failed to read content of {relativePath}: {ex.Message}");
                Console.ResetColor();
            }

            outputBuilder.AppendLine("```"); // End code block
            outputBuilder.AppendLine(); // Empty line for separation
        }
    }
}