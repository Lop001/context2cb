using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public static class ConfigurationService
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true, // For nice output in JSON file
        PropertyNameCaseInsensitive = true, // Be more robust when reading
        // AllowTrailingCommas = true, // Allow trailing commas (if using .NET 5+)
        // ReadCommentHandling = JsonCommentHandling.Skip, // Ignore comments (if using .NET 5+)
    };

    // --- Finding configuration file ---

    /// <summary>
    /// Searches for a configuration file (.contextcli.json) upward from the current directory.
    /// </summary>
    /// <returns>Full path to the found file, or null if not found.</returns>
    public static string? FindConfigFile()
    {
        var currentDir = new DirectoryInfo(Environment.CurrentDirectory);
        DirectoryInfo? searchDir = currentDir;

        while (searchDir != null)
        {
            string potentialPath = Path.Combine(searchDir.FullName, CliConfiguration.ConfigFileName);
            if (File.Exists(potentialPath))
            {
                return potentialPath;
            }

            // Stop searching at repository root (if .git exists) or at disk root
            if (Directory.Exists(Path.Combine(searchDir.FullName, ".git")) || searchDir.Parent == null)
            {
                break;
            }

            searchDir = searchDir.Parent;
        }

        return null; // Not found
    }

    // --- Loading configuration ---

    /// <summary>
    /// Loads configuration from the given file or returns default if the file doesn't exist or is invalid.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <returns>Instance of CliConfiguration.</returns>
    public static async Task<CliConfiguration> LoadConfigurationAsync(string? configPath)
    {
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
        {
            return CliConfiguration.GetDefault(); // File doesn't exist, return default
        }

        try
        {
            using FileStream openStream = File.OpenRead(configPath);
            var config = await JsonSerializer.DeserializeAsync<CliConfiguration>(openStream, _jsonOptions);

            // Basic validation
            if (config == null)
            {
                 Console.Error.WriteLine($"Warning: Configuration file '{configPath}' is empty or invalid. Using default configuration.");
                 return CliConfiguration.GetDefault();
            }
             if (config.SchemaVersion > CliConfiguration.CurrentSchemaVersion)
            {
                Console.Error.WriteLine($"Warning: Configuration file '{configPath}' has schema version {config.SchemaVersion}, which is newer than supported version {CliConfiguration.CurrentSchemaVersion}. Some settings might be ignored.");
                // We can decide to use default or continue with what we have
            }

            // Fill null collections if JSON contained null instead of empty array
            config.Extensions ??= new List<string>();
            config.IgnorePatterns ??= new List<string>();

            return config;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse configuration file '{configPath}'. Error: {ex.Message}. Using default configuration.");
            return CliConfiguration.GetDefault();
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Warning: Failed to read configuration file '{configPath}'. Error: {ex.Message}. Using default configuration.");
            return CliConfiguration.GetDefault();
        }
    }

    /// <summary>
    /// Finds and loads configuration (or returns default).
    /// </summary>
    public static async Task<CliConfiguration> GetEffectiveConfigurationAsync()
    {
        string? configPath = FindConfigFile();
        return await LoadConfigurationAsync(configPath);
    }


    // --- Saving configuration ---

    /// <summary>
    /// Saves the given configuration to a file. Overwrites existing file.
    /// </summary>
    /// <param name="config">Configuration to save.</param>
    /// <param name="configPath">Path where to save the file.</param>
    /// <returns>True on success, false on error.</returns>
    public static async Task<bool> SaveConfigurationAsync(CliConfiguration config, string configPath)
    {
        if (string.IsNullOrEmpty(configPath))
        {
            Console.Error.WriteLine("Error: Cannot save configuration, path is invalid.");
            return false;
        }

        try
        {
            // Ensure directory exists (for init in non-existent subdirectory - although init should be in current dir)
             Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

            using FileStream createStream = File.Create(configPath);
            await JsonSerializer.SerializeAsync(createStream, config, _jsonOptions);
            await createStream.FlushAsync();
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
        {
            Console.Error.WriteLine($"Error saving configuration file '{configPath}': {ex.Message}");
            return false;
        }
    }
}
