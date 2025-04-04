using System;
using TextCopy;

namespace ContextCli.Services
{
    /// <summary>
    /// Service for handling output (clipboard, stdout)
    /// </summary>
    public static class OutputService
    {
        /// <summary>
        /// Outputs the result to clipboard or stdout
        /// </summary>
        public static void OutputResult(string content, bool useStdout)
        {
            if (useStdout)
            {
                Console.WriteLine("\n--- Output ---");
                Console.WriteLine(content);
                Console.WriteLine("--- End Output ---");
            }
            else
            {
                try
                {
                    ClipboardService.SetText(content); // Using TextCopy
                    Console.WriteLine("\n✅ Output successfully copied to clipboard.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"\n❌ Error copying to clipboard: {ex.Message}");
                    Console.ResetColor();
                    Console.WriteLine("\n--- Output (Fallback) ---");
                    Console.WriteLine(content); // Fallback to stdout
                    Console.WriteLine("--- End Output ---");
                }
            }
        }
    }
}