﻿using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Configuration;
using System.Collections.Specialized;
using System.Data.SqlClient;
using DupesMaint2.Models;
using System.IO;
using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using ExifLibrary;
using MetadataExtractor;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Data.Common;

namespace DupesMaint2
{
	public class HelperLib
	{
		private static readonly IConfiguration _config;
		public static string ConnectionString => _config.GetValue<string>("PopsDB");

		// constructor
		static HelperLib()
        {
			var builder = new ConfigurationBuilder();
			BuildConfig(builder);
			_config = builder.Build();

			Log.Logger = new LoggerConfiguration()
				.ReadFrom.Configuration(_config)
				.Enrich.FromLogContext()
				.WriteTo.Console()
				.WriteTo.File("./Logs/DupesMaint2-.log", rollingInterval: RollingInterval.Day, flushToDiskInterval: TimeSpan.FromSeconds(10))
				.CreateLogger();

			// Ensure the log promintently shows the database being used
			var CnStr = new SqlConnectionStringBuilder(ConnectionString);

			// Log the assembly version number
			string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			Serilog.Log.Information(new String('=', 60));
			Serilog.Log.Information($"DupesMaint2 v{assemblyVersion} - starting using DATABASE: {CnStr.InitialCatalog.ToUpper()}");
			Serilog.Log.Information(new String('=', 60));
		}

		// map for file extensions to media group
		static readonly List<FileExtensionTypes> fileExtensionTypes = new()
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

		// list of file extensions that can be hashed by the library
		static readonly List<FileExtensionTypes> fileExtensionTypes2Hashing = new()
		{
			new FileExtensionTypes { Type = ".BMP", Group = "Photo" },
			new FileExtensionTypes { Type = ".GIF", Group = "Photo" },
			new FileExtensionTypes { Type = ".JPEG", Group = "Photo" },
			new FileExtensionTypes { Type = ".JPG", Group = "Photo" },
			new FileExtensionTypes { Type = ".PNG", Group = "Photo" },
		};

		// List of Checksum rows where filenames are the same
		private static List<CheckSum> _checkSums = new();

		/// <summary>
		/// Root Command - Process
		/// 
		/// </summary>
		/// <param name="folder">DirectoryInfo - root folder to the folder structure.</param>
		/// <param name="replace">Bool - If true truncate table CheckSum else add rows.</param>
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
			Serilog.Log.Information($"Process - Total execution time: {_stopwatch.Elapsed.Minutes:N1} mins. # of files processed: {fileCount}\n.{new String('-', 150)}\n");
		}


		/// <summary>
		/// Command5 - calculate and store the requested hashes in the CheckSum table
		/// </summary>
		/// <param name="averageHash">bool -</param>
		/// <param name="differenceHash">bool -</param>
		/// <param name="perceptualHash">bool - </param>
		/// <param name="verbose">bool - verbose logging</param>
		public static void CalculateHashes(bool averageHash, bool differenceHash, bool perceptualHash, bool verbose)
		{
			PopsDbContext popsDbContext = new PopsDbContext();
			Serilog.Log.Information($"CalculateHashes - Starting\n\taverageHash: {averageHash}\n\tdifferenceHash: {differenceHash}\n\tperceptualHash: {perceptualHash}\n\tverbose: {verbose}\n");
			System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
			int processedCount = 0, dropCount = 0;
			string logMessage;

			// get a list of all CheckSum rows 
			var checkSums = popsDbContext.CheckSums.Where(e => e.Id <= 48100).AsList();
			Serilog.Log.Information($"CalculateHashes - Found checkSums.Count: {checkSums.Count:N0}");

			foreach (var checkSum in checkSums)
			{
				// drop where MediaFileType is not 'Unknown'
				if (checkSum.MediaFileType == "Unknown")
				{
					dropCount++;
					continue;
				}

				var type = fileExtensionTypes2Hashing.Find(e => e.Type == checkSum.FileExt);
				if (type is null)
				{
					dropCount++;
					continue;
				}

				// type can have hashes calculated
				FileInfo fileInfo = new(checkSum.FileFullName);

				try    // Calculate the requested hashes
				{
					// image processor seems to have a limit on file size
					if (fileInfo.Length > 71000000)
					{
						logMessage = $"CalculateHashes - id: {checkSum.Id}, fileInfo.Length: {fileInfo.Length,12:N0}, greater than limit 71,000,000";
						checkSum.Notes2 = logMessage;
						Serilog.Log.Warning(logMessage);
						dropCount++;
						continue;
					}

					if (averageHash)
					{
						checkSum.AverageHash = calcAverageHash(fileInfo);
					}
					if (differenceHash)
					{
						checkSum.DifferenceHash = calcDifferenceHash(fileInfo);
					}
					if (perceptualHash)
					{
						checkSum.PerceptualHash = calcPerceptualHash(fileInfo);
					}
				}
				catch (SixLabors.ImageSharp.UnknownImageFormatException exc)
				{
					Serilog.Log.Error($"CalculateHashes - id: {checkSum.Id}, exc: {exc.Message}\n{new String('-', 150)}\n");
				}
				catch (SixLabors.ImageSharp.InvalidImageContentException iIcE)
				{
					Serilog.Log.Error($"CalculateHashes - id: {checkSum.Id}, iIcE: {iIcE.Message}\n{new String('-', 150)}\n");
				}
				catch (FileNotFoundException fnfEx)
				{
					Serilog.Log.Error($"CalculateHashes - id: {checkSum.Id}, fnfEx: {fnfEx.Message}\n{new String('-', 150)}\n");
				}
				catch (Exception ex)
				{
					Serilog.Log.Error($"CalculateHashes - id: {checkSum.Id}\nex: {ex}\n{new String('-', 150)}\n");
					throw;
				}

				if (verbose)
				{
					Serilog.Log.Information($"CalculateHashes - id: {checkSum.Id}, checkSum.AverageHash: {checkSum.AverageHash}, checkSum.DifferenceHash: {checkSum.DifferenceHash}, checkSum.PerceptualHash: {checkSum.PerceptualHash}.");
				}

				if ((++processedCount + dropCount) % 1000 == 0)
				{
					Serilog.Log.Information($"CalculateHashes - {processedCount + dropCount,6:N0}. Completed:{(((processedCount + dropCount) * 100) / checkSums.Count),3:N0}%.");
				}
			}
			Serilog.Log.Information($"CalculateHashes - Finished processing checkSums.Count: {checkSums.Count:N0}, processedCount: {processedCount:N0}, dropCount: {dropCount:N0}.");

			// update the database
			popsDbContext.SaveChanges();

			_stopwatch.Stop();
			Serilog.Log.Information($"CalculateHashes - Total execution time: {_stopwatch.Elapsed.TotalMinutes:N0} mins.\n{new String('-', 150)}");

			////////////////
			// local methods
			////////////////
			decimal calcAverageHash(FileInfo fileInfo)
			{
				AverageHash averageHash = new();    // instaniate the class
				return (decimal)averageHash.Hash(GetStream(fileInfo));
			}

			decimal calcDifferenceHash(FileInfo fileInfo)
			{
				DifferenceHash differenceHash = new();    // instaniate the class
				return (decimal)differenceHash.Hash(GetStream(fileInfo));
			}

			decimal calcPerceptualHash(FileInfo fileInfo)
			{
				PerceptualHash perceptualHash = new();    // instaniate the class
				return (decimal)perceptualHash.Hash(GetStream(fileInfo));
			}

		}


		internal void Tester()
        {
			var connectioString = _config.GetValue<string>("PopsDB");
			Serilog.Log.Information($"\tconnectionString: {connectioString}\n\t\tConnectionString: {ConnectionString}");
		}

		public static void FindDupsUsingHash(string hash, bool verbose)
		{
			PopsDbContext popsDbContext = new PopsDbContext();
			string hashToUse = string.Empty;
			string sqlRaw = string.Empty;
			int processedCount = 0, insertCount = 0, updateCount = 0;

			switch (hash.ToLower())
			{
				case "average":
					hashToUse = "AverageHash";
					sqlRaw = $"select Id, SHA, {hashToUse}, null as DifferenceHash, null as PerceptualHash from CheckSum where {hashToUse} in (select {hashToUse} from CheckSum group by {hashToUse} having count(*) > 1)";
					break;
				case "difference":
					hashToUse = "DifferenceHash";
					sqlRaw = $"select Id, SHA, null as AverageHash, {hashToUse}, null as PerceptualHash from CheckSum where {hashToUse} in (select {hashToUse} from CheckSum group by {hashToUse} having count(*) > 1)";
					break;
				case "perceptual":
					hashToUse = "PerceptualHash";
					sqlRaw = $"select Id, SHA, null as AverageHash, null as DifferenceHash, {hashToUse} from CheckSum where {hashToUse} in (select {hashToUse} from CheckSum group by {hashToUse} having count(*) > 1)";
					break;
			}

			Serilog.Log.Information($"FindDupsUsingHash - Starting\n\thash: {hash} - {hashToUse}\n\tverbose: {verbose}\n\tsqlRaw: {sqlRaw}\n");
			System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

			var checkSumDups = popsDbContext.CheckSumDups;
			Serilog.Log.Information($"FindDupsUsingHash - Loaded table checkSumDups.Count: {checkSumDups.LongCount():N0}");

			var dupOnhashes = popsDbContext.dupOnHashes.FromSqlRaw(sqlRaw);
			if (dupOnhashes.LongCount() == 0)
			{
				Serilog.Log.Warning($"FindDupsUsingHash - No duplicates found based on hash: {hashToUse}\n.{new String('-', 150)}");
				return;
			}
			Serilog.Log.Information($"FindDupsUsingHash - Loaded view model dupOnhashes.Count: {dupOnhashes.LongCount():N0}");

			// Process the list of duplicates found in CheckSum table
			foreach (var dupOnHash in dupOnhashes)
			{
				CheckSumDup_upsert(dupOnHash);

				if (++processedCount % 1000 == 0)
				{
					Serilog.Log.Information($"FindDupsUsingHash - {processedCount,6:N0}. Completed:{((processedCount * 100) / dupOnhashes.LongCount()),3:N0}%.");
				}
			}

			// update the database table CheckSumDups. NB. This only works for entities loaded as DBset type
			popsDbContext.SaveChanges();

			_stopwatch.Stop();
			Serilog.Log.Information($"FindDupsUsingHash - insertCount: {insertCount:N0}, updateCount: {updateCount:N0}, checkSumDups.Count: {checkSumDups.LongCount():N0}, execution time: {_stopwatch.Elapsed.TotalSeconds:N0} secs.\n{new String('-', 150)}");

			////////////////
			// local methods
			////////////////
			void CheckSumDup_upsert(dupOnHash checkSum)
			{
				var checkSumDup = checkSumDups.Where(e => e.CheckSumId == checkSum.Id).FirstOrDefault();
				if (checkSumDup is null)    // need to insert a new CheckSumDups row
				{
					CheckSumDups checkSumDup1 = new()
					{
						CheckSumId = checkSum.Id,
						DupBasedOn = hashToUse,
						Sha = checkSum.Sha,
						AverageHash = (checkSum.AverageHash is null) ? null : checkSum.AverageHash,
						DifferenceHash = (checkSum.DifferenceHash is null) ? null : checkSum.DifferenceHash,
						PerceptualHash = (checkSum.PerceptualHash is null) ? null : checkSum.PerceptualHash,
					};
					checkSumDups.Add(checkSumDup1);
					insertCount++;

					if (verbose)
					{
						Serilog.Log.Information($"FindDupsUsingHash - Added new CheckSumDup, checkSum.Id: {checkSum.Id}.");
					}
				}
				else     // need to update an existing CheckSumDup row
				{
					switch (hash.ToLower())
					{
						case "average":
							checkSumDup.AverageHash = checkSum.AverageHash;
							break;
						case "difference":
							checkSumDup.DifferenceHash = checkSum.DifferenceHash;
							break;
						case "perceptual":
							checkSumDup.PerceptualHash = checkSum.PerceptualHash;
							break;
					}
					updateCount++;
					if (verbose)
					{
						Serilog.Log.Information($"FindDupsUsingHash - Updated existing CheckSumDup, checkSum.Id: {checkSumDup.Id}, checkSumDup.CheckSumId: {checkSumDup.CheckSumId}.");
					}
				}
			}

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
				CheckSum checkSum = new()
				{
					Sha = "",
					Folder = fi.DirectoryName,
					TheFileName = fi.Name,
					FileExt = fi.Extension,
					FileSize = (int)fi.Length,
					FileCreateDt = fi.CreationTime,
					CreateDateTime = _CreateDateTime,
					SCreateDateTime = _sCreateDateTime
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


		/// <summary>
		/// ProcessFiles - Process all the .JPG files in a folder structure
		/// </summary>
		/// <param name="folder">DirectoryInfo - The root folder to search.</param>
		/// <returns>Int - Number of files processed</returns>
		private static int ProcessFiles(DirectoryInfo folder)
		{
			System.Diagnostics.Stopwatch process100Watch = System.Diagnostics.Stopwatch.StartNew();

			int _count = 0;
			FileInfo[] _files = folder.GetFiles("*.JPG", SearchOption.AllDirectories);
			Serilog.Log.Information($"ProcessFiles - Found {_files.Length:N0} files to process.");

			// Process all the JPG files in the source directory tree
			foreach (FileInfo fileInfo in _files)
			{
				// calculate the SHA string for the file and return it with the time taken in ms in a tuple
				(string SHA, int timerMs) = CalcSHA(fileInfo);

				// instantiate a new CheckSum object for the file
				CheckSum checkSum = new CheckSum
				{
					Sha = SHA,
					Folder = fileInfo.DirectoryName,
					TheFileName = fileInfo.Name,
					FileExt = fileInfo.Extension.ToUpper(),
					FileSize = (int)fileInfo.Length,
					FileCreateDt = fileInfo.CreationTime,
					TimerMs = timerMs
				};

				// see if the file name already exists in the CheckSums list
				CheckSum alreadyExists = _checkSums.Find(x => x.Sha == SHA);

				// if the file name already exists then write the Checksum and the new Checksum to the CheckSum tables in the DB
				if (alreadyExists != null)
				{
					CheckSum_upd(alreadyExists);
					CheckSum_upd(checkSum);
				}
				else // just add the new file to the CheckSum list in memory
				{
					_checkSums.Add(checkSum);
				}

				if (++_count % 1000 == 0)
				{
					process100Watch.Stop();
					Serilog.Log.Information($"ProcessFiles - {_count}. Last 100 in {process100Watch.Elapsed.Seconds} secs. Completed: {(_count * 100) / _files.Length}%. Processing folder: {fileInfo.DirectoryName}");
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
			const string sql = @"select SHA,Count(*) as DupeCount from CheckSum group by SHA having Count(*)>1";

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
			Serilog.Log.Information($"INFO\t- Total execution time: {_stopwatch.Elapsed.Minutes} mins.{new String('-', 150)}\n");

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


		private static void CheckSum_upd(CheckSum checkSum)
		{
			// create the SqlParameters for the stored procedure
			DynamicParameters p = new();
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
			DynamicParameters p = new();
			p.Add("@SHA", checkSum.Sha);
			p.Add("@Folder", checkSum.Folder);
			p.Add("@TheFileName", checkSum.TheFileName);
			p.Add("@FileExt", checkSum.FileExt);
			p.Add("@FileSize", checkSum.FileSize);
			p.Add("@FileCreateDt", checkSum.FileCreateDt);
			p.Add("@TimerMs", checkSum.TimerMs);
			p.Add("@Notes", "");
			p.Add("@CreateDateTime", checkSum.CreateDateTime);
			p.Add("@SCreateDateTime", checkSum.SCreateDateTime);

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
			//Serilog.Log.Logger = new LoggerConfiguration()
			//	.Enrich.FromLogContext()
			//	.MinimumLevel.Debug()
			//	.WriteTo.Console()
			//	.WriteTo.File("./Logs/DupesMaint2-.log", rollingInterval: RollingInterval.Day, flushToDiskInterval: TimeSpan.FromSeconds(5))
			//	.CreateLogger();

			// Ensure the log promintently shows the database being used
			var CnStr = new SqlConnectionStringBuilder(ConnectionString);

			// Log the assembly version number
			string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			Serilog.Log.Information(new String('=', 60));
			Serilog.Log.Information($"DupesMaint2 v{assemblyVersion} - starting using DATABASE: {CnStr.InitialCatalog.ToUpper()}");
			Serilog.Log.Information(new String('=', 60));
		}

		private static Stream GetStream(FileInfo fileInfo)
		{
			if (fileInfo.Exists)
			{
				return fileInfo.OpenRead();
			}

			throw new FileNotFoundException(fileInfo.FullName);
		}
		static void BuildConfig(IConfigurationBuilder builder)
		{
			builder.SetBasePath(System.IO.Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true);
		}

	}
}