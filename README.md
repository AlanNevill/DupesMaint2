# DupesMaint2


## CLI application - root command `DupesMaintConsole`
Finds duplicate files from  database tables in Pops database, but only for the specified folder. 

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

Replace default (true) or append (false) to the database tables POPS.CheckSum & POPS.CheckSumDupes.

Example: `--replace false`

### Usage

Using PowerShell from Bin folder or Developer PowerShell in Visual Studio.

`./DupesMaint2 --folder "C:\\Users\\User\\OneDrive\\Photos"`

`./DupesMaint2 --folder "C:\\Users\\User\\OneDrive\\Photos" --replace false`



## Subcommand ProcessEXIF

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



## Subcommand anExif

Extracts all EXIF data from a JPG image to the console and log.

### --image
The image path. Must exist.

### Usage

Using PowerShell from Bin folder or Developer PowerShell in Visual Studio.

`./DupesMaint2 anExif --image "C:\\Users\\User\\OneDrive\\Photos\\2013\\02\\2013-02-24 12.34.54-3.jpg"`



## Subcommand deleteDups

Process the duplicate rows in the POPS.CheckSum table and report files to delete or report AND delete.

### --delete true/<u>false</u>

Set to true if the duplicate files should be deleted.

### Usage

Using PowerShell from Bin folder or Developer PowerShell in Visual Studio.

`./DupesMaint2 deleteDups`

`./DupesMaint2 deleteDups --delete true`



### Log file

Writes to a log file in \Logs folder.







## Process

1. 
2. 
3. 
4. 





