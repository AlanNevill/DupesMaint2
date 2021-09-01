using Serilog;
using System;
using System.Configuration;
using System.Data.SqlClient;
using DupesMaint2.Models;
using System.IO;
using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using ExifLibrary;
using MetadataExtractor;



namespace DupesMaint2
{
    static public class HelperLib
    {
		public static readonly string ConnectionString = @"data source=SNOWBALL\MSSQLSERVER01;initial catalog=POPS;integrated security=True;MultipleActiveResultSets=True";
		//public static string ConnectionString => ConfigurationManager.ConnectionStrings["PopsDB"].ConnectionString;

		public static List<FileExtensionTypes> fileExtensionTypes = new()
		{
			new FileExtensionTypes { Type = ".3GP", Group = "Video" },
			new FileExtensionTypes { Type = ".AVI", Group = "Video" },
			new FileExtensionTypes { Type = ".BMP", Group = "Photo" },
			new FileExtensionTypes { Type = ".EPS", Group = "Photo" },
			new FileExtensionTypes { Type = ".GIF", Group = "Photo" },
			new FileExtensionTypes { Type = ".ICO", Group = "Photo" },
			new FileExtensionTypes { Type = ".JPEG", Group = "Photo" },
			new FileExtensionTypes { Type = ".JPG", Group = "Photo" },
			new FileExtensionTypes { Type = ".M4V", Group = "Video" },
			new FileExtensionTypes { Type = ".MOV", Group = "Video" },
			new FileExtensionTypes { Type = ".MP", Group = "Video" },
			new FileExtensionTypes { Type = ".MP3", Group = "Video" },
			new FileExtensionTypes { Type = ".MP4", Group = "Video" },
			new FileExtensionTypes { Type = ".MPG", Group = "Video" },
			new FileExtensionTypes { Type = ".MTS", Group = "Video" },
			new FileExtensionTypes { Type = ".PCX", Group = "Photo" },
			new FileExtensionTypes { Type = ".PNG", Group = "Photo" },
			new FileExtensionTypes { Type = ".PSD", Group = "Photo" },
			new FileExtensionTypes { Type = ".TIF", Group = "Photo" },
			new FileExtensionTypes { Type = ".TIFF", Group = "Photo" },
			new FileExtensionTypes { Type = ".WMV", Group = "Video" },
			new FileExtensionTypes { Type = ".WEBP", Group = "Photo" },
		};

		// List of Checksum rows where filenames are the same
		private static readonly List<CheckSum> Checksums = new List<CheckSum>();


		public static void Process(DirectoryInfo folder, bool replace)
		{
			Serilog.Log.Information($"Process - Starting find duplicates in target folder is {folder.FullName}\tTruncate table CheckSum is: {replace}.");
			System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

			if (replace)
			{
				using IDbConnection db = new SqlConnection(ConnectionString);
				db.Execute("truncate table dbo.CheckSum");
			}

			// main processing
			int fileCount = ProcessFiles(folder);

			_stopwatch.Stop();
			Serilog.Log.Information($"INFO\t- Total execution time: {_stopwatch.ElapsedMilliseconds / 60000} mins. # of files processed: {fileCount}\n.{new String('-', 150)}\n");
		}

		/// <summary>
		/// Command2 - Process all the files in the folder tree passed in and add rows to CheckSum table
		/// </summary>
		/// <param name="folder">DirectoryInfo - root folder to the folder structure.</param>
		/// <param name="replace">Bool - If True truncate the CheckSum table else add rows.</param>
		public static void ProcessEXIF(DirectoryInfo folder, bool replace)
		{
			int _count = 0;
			Serilog.Log.Information($"ProcessEXIF: target folder is {folder.FullName}\tTruncate CheckSum is: {replace}.");

			if (replace)
			{
				using IDbConnection db = new SqlConnection(HelperLib.ConnectionString);
				db.Execute("truncate table dbo.CheckSum");
			}

			// get an array of FileInfo objects from the folder tree
			FileInfo[] _files = folder.GetFiles("*.JPG", SearchOption.AllDirectories);

			foreach (FileInfo fi in _files)
			{
				// get the EXIF date/time 
				(DateTime _CreateDateTime, string _sCreateDateTime) = HelperLib.ImageEXIF(fi);


				// instantiate a new CheckSum object for the file
				CheckSum checkSum = new CheckSum
				{
					Sha = "",
					Folder = fi.DirectoryName,
					TheFileName = fi.Name,
					FileExt = fi.Extension,
					FileSize = (int)fi.Length,
					FileCreateDt = fi.CreationTime,
					CreateDateTime = _CreateDateTime,
					ScreateDateTime = _sCreateDateTime
				};

				// insert into DB table
				HelperLib.CheckSum_ins2(checkSum);


				_count++;

				if (_count % 1000 == 0)
				{
					Serilog.Log.Information($"ProcessEXIF - {_count,6:N0}. Completed: {((_count * 100) / _files.Length)}%. Processing folder: {fi.DirectoryName}");
				}
			}

		}

		/// <summary>
		/// subCommand3 - Log all the EXIF directories for the image file passed in.
		/// </summary>
		/// <param name="image">FileInfo - A photo or video file.</param>
		public static void ProcessAnEXIF(FileInfo image)
		{
			IEnumerable<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(image.FullName);

			Serilog.Log.Information($"ProcessAnEXIF - image: {image.FullName}");

			foreach (MetadataExtractor.Directory _directory in directories)
			{
				foreach (Tag tag in _directory.Tags)
				{
					Serilog.Log.Information($"[{_directory.Name}]\t - [{tag.Name}] = [{tag.Description}]");
				}
			}
			Serilog.Log.Information($"ProcessAnEXIF - Finished: {image.FullName}\n{new String('-', 150)}");
		}



		private static int ProcessFiles(DirectoryInfo folder)
		{
			int _count = 0;

			System.Diagnostics.Stopwatch process100Watch = System.Diagnostics.Stopwatch.StartNew();

			FileInfo[] _files = folder.GetFiles("*.JPG", SearchOption.AllDirectories);
			Serilog.Log.Information($"ProcessFiles - Found {_files.Length:N0} to process.");

			// Process all the JPG files in the source directory tree
			foreach (FileInfo fi in _files)
			{
				// calculate the SHA string for the file and return with the time taken in ms in a tuple
				(string SHA, int timerMs) = CalcSHA(fi);

				// instantiate a new CheckSum object for the file
				CheckSum checkSum = new CheckSum
				{
					Sha = SHA,
					Folder = fi.DirectoryName,
					TheFileName = fi.Name,
					FileExt = fi.Extension.ToUpper(),
					FileSize = (int)fi.Length,
					FileCreateDt = fi.CreationTime,
					TimerMs = timerMs
				};

				// see if the file name already exists in the Checksums list
				CheckSum alreadyExists = Checksums.Find(x => x.Sha == SHA);

				// if the file name already exists then write the Checksum and the new Checksum to the CheckSum table is the DB
				if (alreadyExists != null)
				{
					CheckSum_ins(alreadyExists);
					CheckSum_ins(checkSum);
				}
				else // just add the new file to the CheckSum list in memory
				{
					Checksums.Add(checkSum);
				}

				_count++;

				if (_count % 1000 == 0)
				{
					process100Watch.Stop();
					Serilog.Log.Information($"ProcessFiles - {_count}. Last 100 in {process100Watch.ElapsedMilliseconds / 1000} secs. " +
						$"Completed: {(_count * 100) / _files.Length}%. " +
						$"Processing folder: {fi.DirectoryName}");
					process100Watch.Reset();
					process100Watch.Start();
				}
			}
			return _count;
		}

		// Process the duplicate rows in the CheckSum table and report files to delete or report AND delete.
		public static void DeleteDupes(bool delete)
		{
			System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

			// 1. Make sure there are some duplicate rows in the CheckSum table
			const string sql = @"select SHA,Count(*) as DupeCount from Checksum group by SHA having Count(*)>1";

			Serilog.Log.Information($"DeleteDupes - Starting DeleteDupes: --delete: {delete} sql: {sql}");

			using var cnn = new SqlConnection(ConnectionString);
			var _CheckSumHasDuplicates = cnn.Query<CheckSumHasDuplicates>(sql).AsList();

			if (_CheckSumHasDuplicates.Count == 0)
			{
				Serilog.Log.Error("DeleteDupes - Abort. No duplicates found in CheckSum{new String('-', 150)}\n");
				return;
			}
			Serilog.Log.Information($"DeleteDupes - {_CheckSumHasDuplicates.Count} duplicates found in the CheckSum table.");

			// Main processing foreach loop local function
			ProcessDeleteDupes();

			_stopwatch.Stop();
			Serilog.Log.Information($"INFO\t- Total execution time: {_stopwatch.ElapsedMilliseconds / 60000} mins.{new String('-', 150)}\n");

			/////////////////
			// local function
			/////////////////
			void ProcessDeleteDupes()
			{
				foreach (var aCheckSumHasDuplicates in _CheckSumHasDuplicates)
				{
					using var cnn = new SqlConnection(ConnectionString);
					var _CheckSum = cnn.Query<CheckSum>($"select * from CheckSum where SHA='{aCheckSumHasDuplicates.SHA}' order by LEN(TheFileName) desc").AsList();
					if (_CheckSum.Count < 2)
					{
						Serilog.Log.Error($"ProcessDeleteDupes - Only found {_CheckSum.Count} CheckSum rows with SHA: {aCheckSumHasDuplicates.SHA}, should be >= 2.");
						continue;
					}

					Serilog.Log.Information($"ProcessDeleteDupes - {_CheckSum.Count} CheckSum rows for duplicate SHA: {aCheckSumHasDuplicates.SHA}");

					// get the CheckSum row with the longest name NB could be the same
					var aCheckSum = _CheckSum[0];
					if (delete)
					{
						FileInfo deleteFileInfo = new FileInfo(Path.Combine(aCheckSum.Folder, aCheckSum.TheFileName));
						if (deleteFileInfo.Exists)
						{
							deleteFileInfo.Delete();
							using IDbConnection db = new SqlConnection(ConnectionString);
							db.Execute($"delete from dbo.CheckSum where Id={aCheckSum.Id}");
							Serilog.Log.Warning($"ProcessDeleteDupes - Deleted the SHA with the longest duplicate name was id: {aCheckSum.Id}\tThe name was: {aCheckSum.TheFileName}\tThe folder was: {aCheckSum.Folder}");
						}
						else
						{
							Serilog.Log.Error($"ProcessDeleteDupes - The duplicate to delete {aCheckSum.TheFileName}\tdoes not now exits in folder: {aCheckSum.Folder}");
						}
					}
					else
					{
						Serilog.Log.Information($"ProcessDeleteDupes - No delete. The SHA with the longest duplicate name is id: {aCheckSum.Id}\tThe name is: {aCheckSum.TheFileName}\tThe folder is: {aCheckSum.Folder}");
					}
				}
			}
		}

		// calculate the SHA256 checksum for the file and return it with the elapsed processing time using a tuple
		private static (string SHA, int timerMs) CalcSHA(FileInfo fi)
		{
			System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

			FileStream fs = fi.OpenRead();
			fs.Position = 0;

			// ComputeHash - returns byte array  
			byte[] bytes = SHA256.Create().ComputeHash(fs);

			// BitConverter used to put all bytes into one string, hyphen delimited  
			string bitString = BitConverter.ToString(bytes);

			watch.Stop();

			return (SHA: bitString, timerMs: (int)watch.ElapsedMilliseconds);
		}


		private static void CheckSum_ins(CheckSum checkSum)
		{
			// create the SqlParameters for the stored procedure
			DynamicParameters p = new DynamicParameters();
			p.Add("@SHA", checkSum.Sha);
			p.Add("@Folder", checkSum.Folder);
			p.Add("@TheFileName", checkSum.TheFileName);
			p.Add("@FileExt", checkSum.FileExt);
			p.Add("@FileSize", checkSum.FileSize);
			p.Add("@FileCreateDt", checkSum.FileCreateDt);
			p.Add("@TimerMs", checkSum.TimerMs);
			p.Add("@Notes", "");

            // call the stored procedure
            using IDbConnection db = new SqlConnection(ConnectionString);
            db.Execute("dbo.spCheckSum_ins", p, commandType: CommandType.StoredProcedure);

        }

		public static void CheckSum_ins2(CheckSum checkSum)
		{
			// create the SqlParameters for the stored procedure
			DynamicParameters p = new DynamicParameters();
			p.Add("@SHA", checkSum.Sha);
			p.Add("@Folder", checkSum.Folder);
			p.Add("@TheFileName", checkSum.TheFileName);
			p.Add("@FileExt", checkSum.FileExt);
			p.Add("@FileSize", checkSum.FileSize);
			p.Add("@FileCreateDt", checkSum.FileCreateDt);
			p.Add("@TimerMs", checkSum.TimerMs);
			p.Add("@Notes", "");
			p.Add("@CreateDateTime", checkSum.CreateDateTime);
			p.Add("@SCreateDateTime", checkSum.ScreateDateTime);

            // call the stored procedure
            using IDbConnection db = new SqlConnection(ConnectionString);
            db.Execute("dbo.spCheckSum_ins2", p, commandType: CommandType.StoredProcedure);
        }


		public static (DateTime CreateDateTime, string sCreateDateTime) ImageEXIF(FileInfo fileInfo)
		{
			DateTime _CreateDateTime = new DateTime(1753, 1, 1);
			string _sCreateDateTime = "Date not found";
			ImageFile _image;

			// try to convert the file into a EXIF ImageFile
			try
			{
				_image = ImageFile.FromFile(fileInfo.FullName);
			}
			catch (NotValidImageFileException)
			{
				_sCreateDateTime = "Not valid image";
				Serilog.Log.Error($"ImageEXIF - File: {fileInfo.FullName}, _sCreateDateTime: {_sCreateDateTime}, _CreateDateTime: {_CreateDateTime}");

				return (CreateDateTime: _CreateDateTime, sCreateDateTime: _sCreateDateTime);
			}
			catch (Exception exc)
			{
				_sCreateDateTime = "ERROR -see log";
				Serilog.Log.Error($"ImageEXIF - File: {fileInfo.FullName}\r\n{exc}\r\n");

				return (CreateDateTime: _CreateDateTime, sCreateDateTime: _sCreateDateTime);
			}

			ExifDateTime _dateTag = _image.Properties.Get<ExifDateTime>(ExifTag.DateTime);

			if (_dateTag != null)
			{
				_sCreateDateTime = _dateTag.ToString();
				if (DateTime.TryParse(_sCreateDateTime, out _CreateDateTime))
				{
					if (_CreateDateTime == DateTime.MinValue)
					{
						_CreateDateTime = new DateTime(1753, 1, 1);
					}
				}
			}

			return (CreateDateTime: _CreateDateTime, sCreateDateTime: _sCreateDateTime);
		}


		/// <summary>
		/// Initialise Serilog
		/// </summary>
		public static void SerilogSetup()
		{
			// Serilog setup
			Serilog.Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.WriteTo.Console()
				.WriteTo.File("./Logs/DupesMaint2-.log", rollingInterval: RollingInterval.Day, flushToDiskInterval: TimeSpan.FromMilliseconds(100))
				.CreateLogger();

			// Ensure the log promintently shows the database being used
			var CnStr = new SqlConnectionStringBuilder(ConnectionString);
			Serilog.Log.Information(new String('-', 50));
			Serilog.Log.Information($"DupesMaint2 - starting using DATABASE: {CnStr.InitialCatalog.ToUpper()}");
			Serilog.Log.Information(new String('-', 50));
		}
	}
}
