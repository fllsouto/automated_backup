# Implementation Plan: Filesystem Analysis & Backup Insights Tool

## Goal
Build a TUI application that analyzes the filesystem and generates actionable insights for:
- Cleaning up space (Docker images, WSL2 distributions, temp files)
- Identifying static/archival files suitable for backup to external storage

## Current State
- **Exists**: `FileSystemService` with directory scanning, size calculation, drive enumeration
- **Missing**: `MainView` UI, analysis/insights engine, specific cleanup detectors

---

## Phase 1: Core UI Foundation

### 1.1 Create MainView
- Create `src/AutomatedBackup/UI/MainView.cs`
- Implement basic Terminal.Gui `Toplevel` window
- Add drive selection panel (left sidebar)
- Add main content area for displaying analysis results
- Add status bar with actions (Scan, Quit)

### 1.2 Drive/Folder Browser
- Display available drives using `FileSystemService.GetAvailableDrives()`
- Allow navigation into folders
- Show folder sizes inline using existing `GetSubdirectories()` method

---

## Phase 2: Analysis Engine

### 2.1 Create InsightAnalyzer Service
- Create `src/AutomatedBackup/Services/InsightAnalyzer.cs`
- Define `Insight` record: `(InsightType, Description, SizeInBytes, Path, RecommendedAction)`
- Implement pluggable analyzer pattern for different insight types

### 2.2 Implement Specific Analyzers

**Docker Analyzer**
- Detect Docker Desktop installation
- Query Docker via CLI (`docker system df`) or scan known paths:
  - `C:\ProgramData\Docker\`
  - `%USERPROFILE%\.docker\`
- Identify unused images, stopped containers, dangling volumes

**WSL2 Analyzer**
- Scan `%USERPROFILE%\AppData\Local\Packages\` for WSL distro VHDXs
- Check `%USERPROFILE%\.wslconfig` for custom paths
- Report VHDX sizes and last access times

**Temp/Cache Analyzer**
- Scan common temp locations:
  - `%TEMP%`
  - `%LOCALAPPDATA%\Temp`
  - Browser caches
  - npm/nuget/pip caches

**Static Files Analyzer**
- Identify files not accessed in X days (configurable)
- Detect large media files (videos, ISOs, archives)
- Flag download folders with old content

---

## Phase 3: Insights Display & Actions

### 3.1 Insights View
- Create `src/AutomatedBackup/UI/InsightsView.cs`
- Display categorized insights in a scrollable list
- Show potential space savings per category
- Group by: "Clean Up", "Archive to External", "Review"

### 3.2 Action Execution
- For cleanup: Execute commands (with confirmation)
- For backup: Generate file list or copy to specified destination
- Track what actions were taken

---

## Phase 4: External Storage Integration

### 4.1 Backup Configuration
- Detect external drives
- Allow user to configure backup destination
- Define rules: file types, age threshold, source folders

### 4.2 Backup Execution
- Copy selected files to external storage
- Maintain backup manifest (what was backed up, when)
- Option to delete after successful backup

---

## Technical Decisions

1. **CLI vs API for Docker**: Use `docker` CLI commands for simplicity; fall back to path scanning if Docker not installed
2. **Async scanning**: Long scans should run async with progress indication
3. **Caching**: Cache scan results to avoid rescanning unchanged directories
4. **Configuration**: Store user preferences in `%APPDATA%\AutomatedBackup\config.json`

---

## Immediate Next Steps

1. Create `MainView.cs` with basic layout
2. Wire up drive selection to folder browser
3. Add a "Scan" button that runs `FileSystemService` and displays results
4. Create the `InsightAnalyzer` interface and first analyzer (Docker or WSL2)
