# context2cb - Project Context to Clipboard

`context2cb` is a command-line utility designed to help software developers quickly gather project context (file structure and relevant code content) and copy it to the clipboard. This is particularly useful for pasting into AI/LLM prompts (like ChatGPT, Claude, Copilot, etc.) to provide them with the necessary background information about a codebase.

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/Lop001/context2cb)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## The Problem

When working with Large Language Models for coding assistance, debugging, or documentation, you often need to provide context about your project. Manually copying the file structure and the content of multiple relevant files can be tedious and error-prone.

## The Solution

`context2cb` automates this process. Simply run it in your project directory, and it will:

1.  Scan the directory structure.
2.  Identify relevant source code files based on configurable extensions.
3.  Respect your `.gitignore` rules and other ignore patterns.
4.  Generate a clean text representation of the file tree.
5.  Append the contents of the identified files.
6.  Copy the combined output directly to your clipboard.

## Features

*   **Directory Scanning:** Recursively scans the current directory and subdirectories.
*   **File Tree Generation:** Creates a textual representation of the project structure.
*   **Content Inclusion:** Appends the content of filtered files, formatted in Markdown code blocks.
*   **Clipboard Integration:** Copies the final output to the system clipboard.
*   **Single File Mode:** Can copy the content of just one specific file.
*   **.gitignore Aware:** Respects rules found in `.gitignore` files by default.
*   **Configurable:** Uses a `.contextcli.json` file for project-specific settings (extensions, ignore patterns).
*   **Config Management:** Built-in commands to edit, show, initialize, and manage configuration.
*   **Filtering:** Options to filter by directory depth, file size, and custom ignore patterns via CLI.
*   **Exclusions:** Automatically skips binary files (based on heuristics) and symbolic links.
*   **Stdout Option:** Can print the output to standard output instead of the clipboard (useful for piping).
*   **Content Exclusion:** Option to copy only the file tree without file contents.
*   **Cross-Platform:** Built with .NET, runs on Windows, macOS, and Linux.

## Installation

### Option 1: Pre-compiled Binaries (Recommended)

1.  Download the latest release executable for your operating system from the [Releases](https://github.com/Lop001/context2cb/releases) page. <!-- Update this link -->
2.  Place the downloaded executable (`context2cb.exe` on Windows, `context2cb` on macOS/Linux) in a directory that is included in your system's `PATH` environment variable.

    *   **Windows:** You can place it in `C:\Windows`, a custom tools directory added to `PATH`, or use `winget` / `scoop` if a package becomes available.
    *   **macOS/Linux:** Common locations include `/usr/local/bin` or `~/bin` (if `~/bin` is in your `PATH`). Make sure the file is executable (`chmod +x context2cb`).

### Option 2: Build from Source

See the [Building from Source](#building-from-source) section below.

## Usage

### Basic Usage (Directory Mode)

Navigate to your project's root directory in your terminal and run:

```bash
context2cb
```

This will scan the directory, generate the context, and copy it to your clipboard.

### Single File Mode

To copy the content of only one file:

```bash
context2cb path/to/your/file.cs
```

### Common Options

*   `--stdout` or `-s`: Print the output to the console instead of the clipboard.
    ```bash
    context2cb --stdout | less
    ```
*   `--no-content`: Copy only the file structure tree, without file contents.
    ```bash
    context2cb --no-content
    ```
*   `--ignore <pattern>` or `-i <pattern>`: Add temporary glob patterns to ignore files/directories (can be used multiple times).
    ```bash
    context2cb -i "**/*.log" -i "temp/*" -i "**/node_modules/**"
    ```
*   `--no-ignore`: Temporarily disable `.gitignore` and configured `ignorePatterns`.
    ```bash
    context2cb --no-ignore
    ```
*   `--depth <N>` or `-d <N>`: Limit directory scanning depth.
    ```bash
    context2cb --depth 2
    ```
*   `--max-size <KB>`: Ignore files larger than the specified size in KB (overrides config).
    ```bash
    context2cb --max-size 500 # Ignore files > 500 KB
    ```
*   `--help` or `-h`: Display help information.

### Configuration Management (`config` command)

Manage the `.contextcli.json` configuration file.

*   `context2cb config --edit` or `-e`: Open the project's configuration file in your default text editor. Creates a default file if none is found.
*   `context2cb config --show`: Display the effective configuration being used (defaults + loaded file).
*   `context2cb config --path`: Show the path to the active configuration file.
*   `context2cb config --init`: Create a default `.contextcli.json` file in the current directory if one doesn't exist higher up.
*   `context2cb config --list-extensions` or `-le`: List the file extensions currently configured to be included.
*   `context2cb config --add-extension .ext1 .ext2 ...` or `-ae`: Add one or more file extensions to the configuration.
*   `context2cb config --remove-extension .ext1 .ext2 ...` or `-re`: Remove one or more file extensions from the configuration.
*   `context2cb config --reset-extensions`: Reset the list of extensions in the configuration file back to the defaults.

## Configuration File (`.contextcli.json`)

You can customize `context2cb`'s behavior on a per-project basis by creating a `.contextcli.json` file in your project's root directory (or any parent directory). The tool searches for this file starting from the current directory upwards.

**Example `.contextcli.json`:**

```json
{
  "schemaVersion": 1,
  "extensions": [
    // List of file extensions (or full filenames) to include
    ".cs",
    ".js",
    ".ts",
    ".py",
    ".html",
    ".css",
    "requirements.txt",
    "Dockerfile",
    ".env.example"
  ],
  "ignorePatterns": [
    // Glob patterns for files/directories to always ignore (in addition to .gitignore)
    "**/bin/**",
    "**/obj/**",
    "**/node_modules/**",
    "**/.git/**",
    "**/*.log",
    ".env"
  ],
  "useGitignore": true, // Set to false to disable .gitignore processing
  "maxFileSizeKb": 1024 // Default max file size in KB (null or 0 for unlimited)
}
```

*   **`schemaVersion`**: Internal versioning for the config format.
*   **`extensions`**: An array of strings representing file extensions (e.g., `.cs`) or exact filenames (e.g., `Dockerfile`) to include. Case-insensitive matching.
*   **`ignorePatterns`**: An array of glob patterns. Files or directories matching these patterns will be ignored. Uses standard glob syntax.
*   **`useGitignore`**: If `true` (default), the tool will find and parse `.gitignore` files and apply their rules.
*   **`maxFileSizeKb`**: Files larger than this size (in kilobytes) will be skipped. Can be overridden by the `--max-size` CLI option. Set to `null` or `0` for no limit.

## Ignoring Files - Summary

Files and directories can be ignored based on the following rules (in order of application):

1.  **Symbolic Links:** Always ignored to prevent cycles and unintended inclusion.
2.  **`.gitignore`:** Patterns from discovered `.gitignore` files are applied if `useGitignore` is `true`.
3.  **`ignorePatterns` (config):** Patterns defined in `.contextcli.json` are applied.
4.  **`--ignore` (CLI):** Patterns provided via the command line are applied.
5.  **Binary Files:** Files detected as likely binary (containing null bytes) are automatically skipped.
6.  **File Size:** Files exceeding the `maxFileSizeKb` (from config or `--max-size` CLI) are skipped.
7.  **Extension Mismatch:** Files whose extensions (or full names) are not listed in the `extensions` array (in config) are skipped.

## Building from Source

**Prerequisites:**

*   .NET 8 SDK (or the version specified in `ContextCli.csproj`)

**Steps:**

1.  Clone the repository:
    ```bash
    git clone https://github.com/Lop001/context2cb
    cd your-repo-directory
    ```
2.  Publish the application (creates a self-contained executable):
    ```bash
    dotnet publish -c Release -r <RID> --self-contained true /p:PublishSingleFile=true
    ```
    Replace `<RID>` with your platform's Runtime Identifier (e.g., `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`). Find RIDs [here](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).
    *Example for Windows x64:*
    ```bash
    dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
    ```
3.  The executable will be located in the `bin/Release/net8.0/<RID>/publish/` directory. You can then copy this executable (`context2cb.exe` or `context2cb`) to a location in your `PATH`.

## Contributing

Contributions are welcome! If you find a bug or have a feature request, please open an issue. If you'd like to contribute code, please open a pull request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details. 
