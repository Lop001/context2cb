# context2cb - Project Context to Clipboard

`context2cb` is a command-line utility designed to help software developers quickly gather project context (file structure and relevant code content) and copy it to the clipboard. This is particularly useful for pasting into AI/LLM prompts (like ChatGPT, Claude, Copilot, etc.) to provide them with the necessary background information about a codebase.

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/your-username/your-repo) <!-- Replace with actual badges if you set up CI/CD -->
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

## Usage
### Basic Usage (Directory Mode)
Navigate to your project's root directory in your terminal and run:

context2cb

### Single File Mode
To copy the content of only one file:

context2cb path/to/your/file.cs










