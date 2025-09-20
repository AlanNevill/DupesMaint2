# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

DupesMaint2 is a .NET 9 console application for managing duplicate files and media organization. It focuses on analyzing photos and videos, calculating various hashes for duplicate detection, processing EXIF metadata, and organizing media files into structured folder hierarchies.

The application operates against a SQL Server database ("Photos" or "pops") and integrates with OneDrive folder structures across multiple machines (WILLBOT, SNOWBALL, BEELINK-1).

## Architecture

### Core Components

**Program.cs**: Main entry point using System.CommandLine for CLI parsing. Implements dependency injection with Entity Framework Core and Serilog logging. Machine-specific configuration selection based on Environment.MachineName.

**HelperLib.cs**: Primary business logic class containing all command implementations. Methods are static and handle file processing, hash calculations, EXIF processing, and database operations.

**Models/PhotosDbContext.cs**: Entity Framework Core database context with DbSets for CheckSum, CheckSumDupsBasedOn, VCheckSumBasedOnGroup, and HashTypes tables.

**Models/CheckSum.cs**: Primary entity representing files in the database with properties for file paths, hashes (SHA256, AverageHash, DifferenceHash, PerceptualHash), EXIF data, and computed file paths.

**Hashs/ directory**: Contains image hashing implementations:
- `IImageHash.cs`: Interface for perceptual hashing algorithms
- `AverageHash.cs`, `DifferenceHash.cs`, `PerceptualHash.cs`: Different hashing algorithm implementations
- `CompareHash.cs`: Hash comparison utilities

### Key Dependencies

- **Entity Framework Core 9.0**: Database ORM for SQL Server
- **System.CommandLine**: CLI framework (beta version)
- **Serilog**: Structured logging with file and console sinks
- **MetadataExtractor**: EXIF and media metadata reading
- **ImageSharp**: Image processing for hash calculations
- **Drastic.ImageHash & CoenM.ImageSharp.ImageHash**: Perceptual hashing libraries
- **Dapper**: SQL query execution
- **xUnit & FluentAssertions**: Unit testing framework

### Database Architecture

The application uses SQL Server with these primary tables:
- **CheckSum**: File records with metadata and hash values
- **CheckSumDupsBasedOn**: Duplicate relationships based on hash comparisons
- **VCheckSumBasedOnGroup**: View for grouped duplicate analysis

## Development Commands

### Build and Run
```powershell
# Build the project
dotnet build

# Build in release mode  
dotnet build -c Release

# Run the application (from project root)
dotnet run

# Run with specific command
dotnet run -- --folder "C:\Path\To\Photos" --mediaType Photo

# Run from bin directory after build
.\DupesMaint2.exe --folder "C:\Path\To\Photos"
```

### Testing
```powershell
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test -v normal

# Run specific test class
dotnet test --filter "HelperLibTests"

# Run tests with coverage (requires additional package)
dotnet test --collect:"XPlat Code Coverage"
```

### Database Operations
```powershell
# Entity Framework migrations (if needed)
dotnet ef migrations add MigrationName
dotnet ef database update

# Generate database context (if schema changes)
dotnet ef dbcontext scaffold "ConnectionString" Microsoft.EntityFrameworkCore.SqlServer -o Models -f
```

## Application Commands

### Root Command - Load File Types
```powershell
# Load photos from a folder tree
.\DupesMaint2 --folder "C:\Users\User\OneDrive\Photos" --mediaType Photo --replace false

# Load videos with table replacement
.\DupesMaint2 --folder "C:\Users\User\OneDrive\Videos" --mediaType Video --replace true --verbose true
```

### EXIF Processing
```powershell
# Process EXIF data for all photos in folder tree
.\DupesMaint2 EXIF --folder "C:\Users\User\OneDrive\Photos" --replace false

# Extract EXIF from single image
.\DupesMaint2 anEXIF --image "C:\Users\User\OneDrive\Photos\2013\02\image.jpg"
```

### Camera Roll Organization
```powershell
# Move photos from Camera Roll to organized folders
.\DupesMaint2 CameraRoll_Move --mediaFileType Photo --verbose true

# Move without database dependency
.\DupesMaint2 CameraRoll_MoveNoDb --verbose true
```

### Hash Calculations
```powershell
# Calculate multiple hash types
.\DupesMaint2 CalculateHashes --ShaHash true --AverageHash true --DifferenceHash false --PerceptualHash true --verbose true

# Calculate only average hash
.\DupesMaint2 CalculateHashes --AverageHash true --ShaHash false --DifferenceHash false --PerceptualHash false
```

### Duplicate Detection
```powershell
# Find duplicates using perceptual hash
.\DupesMaint2 FindDupsUsingHash --hash Perceptual --verbose true

# Find duplicates using SHA hash
.\DupesMaint2 FindDupsUsingHash --hash Sha
```

### Data Management
```powershell
# Create training CSV for ML model
.\DupesMaint2 TrainingCSV --verbose true

# Create perceptual hash CSV
.\DupesMaint2 PerceptualHashCSV --verbose true

# Delete records based on CSV file
.\DupesMaint2 ShaDelete --CSVfile "C:\Path\To\DeleteList.csv" --verbose true
```

## Configuration

### Machine-Specific Setup
The application automatically detects the machine name and uses corresponding connection strings and folder paths from `appsettings.json`. Supported machines: WILLBOT, SNOWBALL, BEELINK-1.

### Database Connection
Connection strings are configured per machine in `appsettings.json`. The application uses integrated security and expects SQL Server to be available.

### Logging
Logs are written to both console and file (`C:\Logs\DupesMaint2\DupesMaint2-.log`). File logging includes rotation by size and date with retention policies.

## Project Structure

```
DupesMaint2/
├── Program.cs              # CLI entry point and DI configuration
├── HelperLib.cs            # Core business logic (main processing)
├── HelperLibTests.cs       # Unit tests for HelperLib
├── Models/                 # Entity Framework models
│   ├── PhotosDbContext.cs  # Database context
│   ├── CheckSum.cs         # Primary file entity
│   ├── CheckSumDupsBasedOn.cs
│   └── ...
├── Hashs/                  # Image hashing implementations
│   ├── IImageHash.cs       # Hashing interface
│   ├── AverageHash.cs
│   ├── DifferenceHash.cs
│   └── PerceptualHash.cs
├── linq/                   # LINQPad query files
├── Properties/
├── appsettings.json        # Configuration and connection strings
└── README.md              # Detailed command documentation
```

## Development Notes

### File Processing Workflow
1. Files are scanned and loaded into CheckSum table via `LoadFileType`
2. EXIF metadata extracted and stored via `ProcessEXIF`  
3. Various hash algorithms applied via `CalculateHashes`
4. Duplicates identified and stored in CheckSumDupsBasedOn via `FindDupsUsingHash`
5. Files organized into date-based folders via `CameraRoll_Move`

### Hash Types Supported
- **SHA256**: File content hash for exact duplicates
- **AverageHash**: Perceptual hash based on image average
- **DifferenceHash**: Gradient-based perceptual hash
- **PerceptualHash**: DCT-based perceptual hash for similar images

### Media File Extensions
The application recognizes specific file extensions mapped to Photo/Video media types. See `_fileExtensionTypes` in HelperLib.cs for complete list.

### Testing Strategy
Unit tests focus on the CameraRoll file processing logic using temporary directories and mock configurations. Tests cover file movement, EXIF processing, error handling, and edge cases.