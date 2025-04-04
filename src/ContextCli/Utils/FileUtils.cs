using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DotNet.Globbing;

namespace ContextCli.Utils
{
    /// <summary>
    /// Utility methods for file operations
    /// </summary>
    public static class FileUtils
    {
        /// <summary>
        /// Detects whether a file is likely binary (contains NULL byte).
        /// Limited to reading a small chunk of the file for performance.
        /// </summary>
        public static async Task<bool> IsLikelyBinaryAsync(FileInfo file)
        {
            // Ignore very small files (probably not binary in the usual sense)
            if (file.Length == 0) return false;

            // Define known "safe" text extensions that don't need to be checked
            var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
             { ".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".csv", ".html", ".css", ".js", ".ts",
               ".py", ".rb", ".php", ".pl", ".sh", ".bat", ".ps1", ".sql", ".java", ".cs", ".go", ".rs",
               ".c", ".cpp", ".h", ".hpp", ".csproj", ".sln", ".props", ".targets", ".gitignore", ".gitattributes" };

            if (textExtensions.Contains(file.Extension)) return false;

            // Read only the beginning of the file
            const int bytesToRead = 4096; // How many bytes to try to read
            byte[] buffer = new byte[bytesToRead];
            int bytesRead = 0;

            try
            {
                using var fs = file.OpenRead();
                bytesRead = await fs.ReadAsync(buffer, 0, (int)Math.Min(file.Length, bytesToRead));
            }
            catch (IOException)
            {
                // If we can't read, better skip it
                return true;
            }

            // Look for NULL byte (0x00)
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0x00)
                {
                    return true; // NULL byte found -> probably binary
                }
            }

            return false; // No NULL byte found in the first X bytes -> probably text
        }

        /// <summary>
        /// Finds all .gitignore files in the current directory and parent directories
        /// </summary>
        public static List<string> FindGitignoreFiles()
        {
            var gitignoreFiles = new List<string>();
            var currentDir = new DirectoryInfo(Environment.CurrentDirectory);

            // Look for .gitignore in current directory
            var gitignorePath = Path.Combine(currentDir.FullName, ".gitignore");
            if (File.Exists(gitignorePath))
            {
                gitignoreFiles.Add(gitignorePath);
            }

            // Look for .gitignore in parent directories
            while (currentDir.Parent != null)
            {
                currentDir = currentDir.Parent;
                gitignorePath = Path.Combine(currentDir.FullName, ".gitignore");
                if (File.Exists(gitignorePath))
                {
                    gitignoreFiles.Add(gitignorePath);
                    // Don't break - we want to collect all .gitignore files up to root
                }
            }

            return gitignoreFiles;
        }

        /// <summary>
        /// Maps file extension to language identifier for Markdown.
        /// </summary>
        public static string GetLanguageFromExtension(string extension)
        {
            // Simple mapping, can be extended
            return extension.ToLowerInvariant() switch
            {
                ".cs" => "csharp",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".py" => "python",
                ".java" => "java",
                ".go" => "go",
                ".rs" => "rust",
                ".php" => "php",
                ".rb" => "ruby",
                ".html" => "html",
                ".css" => "css",
                ".scss" => "scss",
                ".json" => "json",
                ".yaml" => "yaml",
                ".yml" => "yaml",
                ".xml" => "xml",
                ".sh" => "bash",
                ".bat" => "batch",
                ".ps1" => "powershell",
                ".sql" => "sql",
                ".md" => "markdown",
                _ => "" // Default: no language (or "plaintext")
            };
        }
    }
}