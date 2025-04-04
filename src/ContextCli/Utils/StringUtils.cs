using System.Collections.Generic;

namespace ContextCli.Utils
{
    /// <summary>
    /// Utility methods for string operations
    /// </summary>
    public static class StringUtils
    {
        /// <summary>
        /// Converts gitignore pattern to glob pattern
        /// </summary>
        public static string ConvertGitignoreToGlob(string pattern)
        {
            // Remove leading slash - gitignore uses it for root-relative paths
            if (pattern.StartsWith("/"))
                pattern = pattern.Substring(1);
            
            // If pattern doesn't start with **, make it relative to root
            if (!pattern.StartsWith("**/") && !pattern.StartsWith("**\\") && !pattern.Contains("/"))
                pattern = "**/" + pattern;
                
            return pattern;
        }

        /// <summary>
        /// Generates tree prefix for directory structure visualization
        /// </summary>
        public static string GetTreePrefix(List<bool> parentIsLastStack)
        {
            if (parentIsLastStack.Count == 0) return "";

            var result = "";
            // All but the last element determine the prefix
            for (int i = 0; i < parentIsLastStack.Count - 1; i++)
            {
                result += parentIsLastStack[i] ? "    " : "│   ";
            }
            return result;
        }
    }
}