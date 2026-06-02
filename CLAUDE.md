# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DupesMaint2 is a .NET 10 C# console application for managing photo/video duplicates. It scans folder trees, loads file metadata into SQL Server, calculates multiple hash types for duplicate detection, extracts EXIF metadata, and organizes media into date-based folder hierarchies.

## Build and Test Commands

```powershell
dotnet build
dotnet build -c Release
dotnet test
dotnet test --filter "HelperLibTests"
dotnet test -v normal
dotnet run -- --folder "C:\Path\To\Photos" --mediaType Photo
```

## Architecture

### Key Design Constraint: Static HelperLib

`HelperLib.cs` (~3500 LOC) is the core business logic class. All methods are **static**, sharing state via static fields. This is a known anti-pattern identified for refactoring — do not extend this pattern further. New logic should be instance-based where possible.

### CLI Framework

`Program.cs` uses **System.CommandLine v2.0.0-beta4** (not the stable release). The API is pre-release — subcommand handlers use the `SetHandler` pattern with dependency injection wired through `ServiceProvider`.

### Machine-Specific Configuration

`appsettings.json` contains connection strings and OneDrive folder paths for three named machines: **WILLBOT**, **SNOWBALL**, **BEELINK-1**. `Program.cs` selects the active config block using `Environment.MachineName`. When adding configuration, follow this existing per-machine pattern.

### Database

SQL Server via Entity Framework Core 10 + Dapper. Primary tables:
- **CheckSum** — one row per file; holds path, SHA256, AverageHash, DifferenceHash, PerceptualHash, and EXIF fields
- **CheckSumDupsBasedOn** — duplicate relationships (composite key: `BasedOnVal`, `CheckSumId`)
- **VCheckSumBasedOnGroup** — read-only SQL view for grouped duplicate analysis

EF migrations folder exists but is empty; the schema is managed outside this project (scaffold from existing DB).

### Hash Implementations (`Hashs/`)

Four hash algorithms are used for duplicate detection at different similarity thresholds:
- **SHA256** — exact file content match
- **AverageHash** — 8×8 grayscale; fastest, least precise
- **DifferenceHash** — gradient-based; good general perceptual match
- **PerceptualHash** — DCT-based; most accurate for near-duplicate images

`CompareHash.cs` provides Hamming distance comparison used to determine similarity.

### Typical Workflow Order

1. `LoadFileType` — scan folder tree → populate CheckSum table
2. `EXIF` — extract EXIF dates/metadata → update CheckSum rows
3. `CalculateHashes` — compute hash columns on CheckSum rows
4. `FindDupsUsingHash` — populate CheckSumDupsBasedOn from hash comparisons (DEPRECATED in favour of SQL-side logic)
5. `CameraRoll_Move` / `CameraRoll_MoveNoDb` — move files to `Photos\YYYY\MM` or `Video\YYYY-MM` folders

### Logging

Serilog writes to console and a rolling file at `C:\Logs\DupesMaint2\DupesMaint2-.log`. Log verbosity is controlled per-command via `--verbose`.

## Testing

Unit tests live in `HelperLibTests.cs` (same project, not a separate test project). Tests use xUnit + FluentAssertions and focus on CameraRoll file movement logic using real temporary directories — no mocking of the filesystem.
