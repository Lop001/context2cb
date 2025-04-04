using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContextCli.Utils;
using DotNet.Globbing;

namespace ContextCli.Services
{
    /// <summary>
    /// Service for handling file/directory ignore patterns
    /// </summary>
    public static class IgnoreService
    {
        /// <summary>
        /// Builds a list of glob matchers from configuration and CLI patterns
        /// </summary>
        public static async Task<List<Glob>?> BuildIgnoreMatchersAsync(CliConfiguration config, string[] cliIgnorePatterns)
        {
            var allPatterns = new List<string>();

            // 1. Patterns from configuration
            if (config.IgnorePatterns != null)
            {
                allPatterns.AddRange(config.IgnorePatterns);
            }

            // 2. Patterns from CLI (--ignore)
            allPatterns.AddRange(cliIgnorePatterns);

            // 3. Patterns from .gitignore (if enabled)
            if (config.UseGitignore)
            {
                var gitignorePaths = FileUtils.FindGitignoreFiles();
                foreach (var gitignorePath in gitignorePaths)
                {
                    Console.WriteLine($"Loading ignore patterns from: {Path.GetRelativePath(Environment.CurrentDirectory, gitignorePath)}");
                    try
                    {
                        var lines = await File.ReadAllLinesAsync(gitignorePath);
                        allPatterns.AddRange(lines
                            .Select(line => line.Trim())
                            .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("#"))); // Ignore empty lines and comments
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Error.WriteLine($"Warning: Could not read or parse {gitignorePath}: {ex.Message}");
                        Console.ResetColor();
                    }
                }
            }

            if (!allPatterns.Any())
            {
                return null; // No patterns to ignore
            }

            // Create individual Glob matcher instances for each pattern
            var matchers = new List<Glob>();
            var options = new GlobOptions { Evaluation = { CaseInsensitive = false } };
            
            foreach (var pattern in allPatterns)
            {
                try
                {
                    // Convert gitignore pattern to glob pattern if needed
                    string globPattern = StringUtils.ConvertGitignoreToGlob(pattern);
                    var glob = Glob.Parse(globPattern, options);
                    matchers.Add(glob);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Error.WriteLine($"Warning: Failed to parse ignore pattern '{pattern}': {ex.Message}");
                    Console.ResetColor();
                }
            }

            return matchers.Count > 0 ? matchers : null;
        }

        /// <summary>
        /// Checks if a path should be ignored based on the provided matchers
        /// </summary>
        public static bool ShouldIgnorePath(string relativePath, List<Glob>? ignoreMatchers)
        {
            if (ignoreMatchers == null) return false;

            foreach (var matcher in ignoreMatchers)
            {
                if (matcher.IsMatch(relativePath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}