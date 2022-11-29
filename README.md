# DupesMaint2

## CLI application - Similar to Find Duplicates

Finds duplicate files from database tables in Pops database, but only for the specified folder. 

### Root command `DupesMaintConsole`

`from c in this._popsContext.CheckSum
                           join d in this._popsContext.CheckSumDups
                           on c.Sha equals d.Sha
                           where c.Folder.StartsWith(@folder.FullName)
                           select new { c.TheFileName })
                           .Distinct()`

### --folder string

The root folder of the tree to scan which must exist.

Example:  --folder "`C:\\Users\\User\\OneDrive\\Photos`"

### --replace <u>true</u>/false

Replace default (true) or append (false) to the database table Photos.CheckSum.

Example: `--replace false`

### Usage

Using PowerShell from Bin folder or Developer PowerShell in Visual Studio.

`./DupesMaint2 --folder "C:\\Users\\User\\OneDrive\\Photos"`

`./DupesMaint2 --folder "C:\\Users\\User\\OneDrive\\Photos" --replace false`

## Subcommand - ProcessEXIF

Command to extract EXIF date/time from all JPG image files in a folder tree. Optionally update the columns in the POPS.CheckSum table.

### --folder string

The root folder of the tree to scan which must exist.

Example:  --folder "`C:\\Users\\User\\OneDrive\\Photos`"

### --replace <u>true</u>/false

Replace default (true) or append (false) to the database table POPS.CheckSum.

Example: `--replace false`

### Usage

Using PowerShell from Bin folder or Developer PowerShell in Visual Studio.

`./DupesMaint2 ProcessExif --folder "C:\\Users\\User\\OneDrive\\Photos"`

`./DupesMaint2 ProcessExif --folder "C:\\Users\\User\\OneDrive\\Photos" --replace false`

## Subcommand3 - anExif

Extracts all EXIF data from a JPG image to the console and log.

### --image

The image path. Must exist.

### Usage

Using PowerShell from Bin folder or Developer PowerShell in Visual Studio.

`./DupesMaint2 anExif --image "C:\\Users\\User\\OneDrive\\Photos\\2013\\02\\2013-02-24 12.34.54-3.jpg"`

## Subcommand4 - CameraRoll_Move

Move media files from folder "C:\Users\User\OneDrive\Pictures\Camera Roll" to Photos\YYYY\MM or Video\YYYY-MM folders for appropriate year and month. Reads EXIF data from Photos.

### --mediaFileType

Photo or Video. Video not yet implemented

### --verbose true/<u>false</u>

Toggle verbose logging.

### Usage

Using PowerShell from Bin folder or Developer PowerShell in Visual Studio.

`./DupesMaint2 CameraRoll_Move --mediaFileType Photo --verbose true`

`./DupesMaint2 CameraRoll_Move --mediaFileType Photo`

## Subcommand5 - CalculateHashes

Calculate and store up to 4 hashes in the CheckSum table for photos only.

### --ShaHash                true/<u>false</u>

Claculate the SHA 256 value for the column.

### --averageHash        true/<u>false</u>

Calculate the AverageHash column.

### --differenceHash    true/<u>false</u>

Calculate the DifferenceHash column.

### --perceptualHash    true/<u>false</u>

Calculate the PerceptualHash column.

### --verbose                    true/<u>false</u>

Verbose logging.

### Usage

Using PowerShell from Bin folder or Developer PowerShell in Visual Studio.

`./DupesMaint2 CalculateHashes --averageHash true --differenceHash false --perceptualHash false`

`./DupesMaint2 CalculateHashes --averageHash true`

## Subcommand6 - FindDupsUsingHash DEPRECATED

CheckSumDups insert or update based on hash column from CheckSum table.

### --hash     FromAmong("average","difference","perceptual")

Select which hash column to use.

### --verbose    true/<u>false</u>

Verbose logging.

### Usage

Using PowerShell from Bin folder or Developer PowerShell in Visual Studio.

`./dupesmaint2 FindDupsUsingHash --hash average`

### Log file

Writes to a log file by date in \Logs folder.

## Process

1. 
2. 
3. 
4. 
