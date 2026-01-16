# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the solution
dotnet build

# Build specific project
dotnet build src/AutomatedBackup/AutomatedBackup.csproj

# Run the application
dotnet run --project src/AutomatedBackup/AutomatedBackup.csproj

# Build release configuration
dotnet build -c Release
```

## Architecture

This is a .NET 8 console application that provides an automated backup utility with a Terminal User Interface (TUI) using the Terminal.Gui library.

### Project Structure
- **src/AutomatedBackup/** - Main application project
  - **Program.cs** - Entry point; initializes Terminal.Gui and runs MainView
  - **Services/** - Business logic services (e.g., FileSystemService for directory scanning)
  - **UI/** - Terminal.Gui view components (namespace: `AutomatedBackup.UI`)

### Key Patterns
- Uses Terminal.Gui v1.19 for TUI - application lifecycle: `Application.Init()` → `Application.Run(view)` → `Application.Shutdown()`
- Services use records for immutable data containers (e.g., `FolderInfo`)
- File system operations handle `UnauthorizedAccessException` and `PathTooLongException` gracefully

### Dependencies
- **Terminal.Gui** - TUI framework for console-based user interfaces
