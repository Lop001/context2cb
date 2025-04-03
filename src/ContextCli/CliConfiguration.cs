using System.Collections.Generic;
using System.Text.Json.Serialization; // Needed for JsonPropertyName

public class CliConfiguration
{
    public const string ConfigFileName = ".contextcli.json"; // Configuration file name
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("extensions")]
    public List<string> Extensions { get; set; } = GetDefaultExtensions();

    [JsonPropertyName("ignorePatterns")]
    public List<string> IgnorePatterns { get; set; } = GetDefaultIgnorePatterns();

    [JsonPropertyName("useGitignore")]
    public bool UseGitignore { get; set; } = true;

    [JsonPropertyName("maxFileSizeKb")]
    public int? MaxFileSizeKb { get; set; } = 1024; // Example: 1MB limit, null = no limit

    // --- Methods for default values ---

    public static List<string> GetDefaultExtensions() => new List<string>
    {
        ".cs", ".js", ".ts", ".py", ".java", ".go", ".rs", ".php", ".rb",
        ".html", ".css", ".scss", ".json", ".yaml", ".yml", ".xml",
        ".sh", ".bat", ".ps1", ".sql", ".md", ".txt",
        ".csproj", ".sln", ".props", ".targets", "requirements.txt",
        "Dockerfile", ".env.example", "pyproject.toml", "package.json"
        // Add more as needed
    };

    public static List<string> GetDefaultIgnorePatterns() => new List<string>
    {
        "**/bin/**",
        "**/obj/**",
        "**/node_modules/**",
        "**/.git/**",
        "**/.svn/**",
        "**/.hg/**",
        "**/.vs/**",
        "**/.vscode/**",
        "**/*.log",
        "**/*.dll",
        "**/*.exe",
        "**/*.so",
        "**/*.dylib",
        "**/*.pyc",
        "**/*.cache",
        "**/__pycache__/**",
        ".env" // Ignore .env files
        // Add more common ignore patterns
    };

    public static CliConfiguration GetDefault()
    {
        // Creates an instance with default values defined above
        return new CliConfiguration();
    }
}
