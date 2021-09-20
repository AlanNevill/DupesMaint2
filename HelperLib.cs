using Microsoft.Extensions.Configuration;
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
using System.Threading.Tasks;

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
			builder.SetBasePath(System.IO.Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true);

			//BuildConfig(builder);
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
		/// <param name="ShaHash">bool -</param>
		/// <param name="averageHash">bool -</param>
		/// <param name="differenceHash">bool -</param>
		/// <param name="perceptualHash">bool - </param>
		/// <param name="verbose">bool - verbose logging</param>
		public static void CalculateHashes(bool ShaHash, bool averageHash, bool differenceHash, bool perceptualHash, bool verbose)
		{
			PopsDbContext popsDbContext = new PopsDbContext();
			Serilog.Log.Information($"CalculateHashes - Starting\n\tShaHash: {ShaHash}\n\taverageHash: {averageHash}\n\tdifferenceHash: {differenceHash}\n\tperceptualHash: {perceptualHash}\n\tverbose: {verbose}\n");
			System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
			int processedCount = 0, dropCount = 0;
			string logMessage;

			// get a list of all CheckSum rows 
			//var checkSums = popsDbContext.CheckSums;
			Serilog.Log.Information($"CalculateHashes - Found checkSums.Count: {popsDbContext.CheckSums.LongCount():N0}");

			foreach (var checkSum in popsDbContext.CheckSums)
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
                    // calculate the Sha hash
                    if (ShaHash && checkSum.Sha is null)
                    {
						checkSum.Sha = calcShaHash(fileInfo);
                    }

					// image processor seems to have a limit on file size
					if (fileInfo.Length > 71000000)
					{
						logMessage = $"CalculateHashes - id: {checkSum.Id}, fileInfo.Length: {fileInfo.Length,12:N0}, greater than limit 71,000,000";
						checkSum.Notes2 = logMessage;
						Serilog.Log.Warning(logMessage);
						dropCount++;
						continue;
					}

					if (averageHash && checkSum.AverageHash is null)
					{
						checkSum.AverageHash = calcAverageHash(fileInfo);
					}
					if (differenceHash && checkSum.DifferenceHash is null)
					{
						checkSum.DifferenceHash = calcDifferenceHash(fileInfo);
					}
					if (perceptualHash && checkSum.PerceptualHash is null)
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
				}

				if (verbose)
				{
					Serilog.Log.Information($"CalculateHashes - id: {checkSum.Id}, checkSum.AverageHash: {checkSum.AverageHash}, checkSum.DifferenceHash: {checkSum.DifferenceHash}, checkSum.PerceptualHash: {checkSum.PerceptualHash}.");
				}

				if ((++processedCount + dropCount) % 1000 == 0)
				{
					Serilog.Log.Information($"CalculateHashes - {processedCount + dropCount,6:N0}. Completed:{(((processedCount + dropCount) * 100) / popsDbContext.CheckSums.LongCount()),3:N0}%.");
				}

			}
			Serilog.Log.Information($"CalculateHashes - Finished processing checkSums.Count: {popsDbContext.CheckSums.LongCount():N0}, processedCount: {processedCount:N0}, dropCount: {dropCount:N0}.");

			// update the database
			popsDbContext.SaveChanges();

			_stopwatch.Stop();
			Serilog.Log.Information($"CalculateHashes - Total execution time: {_stopwatch.Elapsed.TotalMinutes:N0} mins.\n{new String('-', 150)}");

			////////////////
			// local methods
			////////////////
			string calcShaHash(FileInfo fileInfo)
			{
				// calculate the SHA256 checksum for the file and return it with the elapsed processing time using a tuple

					FileStream fs = fileInfo.OpenRead();
					//fs.Position = 0;

					// ComputeHash - returns byte array  
					byte[] bytes = SHA256.Create().ComputeHash(fs);

					// BitConverter used to put all bytes into one string, hyphen delimited  
					return BitConverter.ToString(bytes);
			}

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

		/// <summary>
		/// Move all but the largest CheckSum files with the same PerceptualHash to a folder on the H drive
		/// </summary>
		/// <param name="verbose"></param>
		public void PerceptualHash_Move2Hdrive(bool verbose)
        {
			System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
			int counter = 0;
			PopsDbContext popsDbContext = new();

			var perceptualHashes = from p in popsDbContext.CheckSums
								   where p.PerceptualHash != null
								   group p by p.PerceptualHash into g
								   where g.Count() > 1
								   orderby g.Count() descending
								   select new { g.Key, Count = g.Count() };

			Serilog.Log.Information($"perceptualHashes.LongCount(): {perceptualHashes.LongCount():N0}");
  
			foreach (var perceptualHash in perceptualHashes)
			{
				PopsDbContext popsDbContext1 = new();
				List<CheckSum> checkSums = popsDbContext1.CheckSums.Where(y => y.PerceptualHash == perceptualHash.Key).ToList();
				int maxSize = checkSums.Max(y => y.FileSize);
				Serilog.Log.Information($"perceptualHash.Key: {perceptualHash.Key}, checkSums.Count: {checkSums.Count}, maxSize: {maxSize:N0}");

                foreach (var checkSum in checkSums.OrderByDescending(v => v.FileSize))
                {
                    if (checkSum.FileSize == maxSize)
                    {
						Serilog.Log.Information($"PerceptualHash_Move2Hdrive - checkSum.Id: {checkSum.Id}, has maxSize: {maxSize:N0} and will not be moved");
						continue;
					}

					// Move this CheckSum file
					MoveTheFile(checkSum, popsDbContext1);

					counter++;
				}
			}

			_stopwatch.Stop();
			Serilog.Log.Information($"PerceptualHash_Move2Hdrive - Total execution time: {_stopwatch.Elapsed.TotalMinutes:N0} mins.\n{new String('-', 150)}");

			//////////////////
			// Local functions
			//////////////////
			void MoveTheFile(CheckSum checkSum, PopsDbContext popsDbContext1)
            {
				// Generate the new folder and create if necessary
				DirectoryInfo directoryInfo = new(Path.Combine(@"H:\PerceptualHashes", checkSum.PerceptualHash.ToString()));
                if (!directoryInfo.Exists)
                {
					directoryInfo.Create();
                }

                // Move the file
                try
                {
					File.Move(checkSum.FileFullName, Path.Combine(directoryInfo.FullName, checkSum.TheFileName),true);
                }
                catch (FileNotFoundException fnf)    // source file not found
                {
					Serilog.Log.Error($"PerceptualHash_Move2Hdrive - File not found, checkSum.Id: {checkSum.Id}\n{fnf}");
                }

				// Update the checkSum row
				checkSum.Folder = directoryInfo.FullName;
				popsDbContext1.SaveChanges();

				if (verbose)
				{
					Serilog.Log.Information($"PerceptualHash_Move2Hdrive - checkSum.Id: {checkSum.Id}, checkSum.FileSize: {checkSum.FileSize:N0} was moved to: {checkSum.Folder}");
				}
			}

		}

		// TODO: ONLY WORKS FOR ShaHash & PerceptualHash
		public static void FindDupsUsingHash(string hash, bool verbose)
		{
			Serilog.Log.Information($"FindDupsUsingHash - Starting\n\thash: {hash}\n\tverbose: {verbose}\n");
			System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

			PopsDbContext popsDbContext = new ();
			int processedCount = 0, insertCheckSumDupsCount = 0, updateCheckSumDupsBasedOnCount = 0, insertCheckSumDupsBasedOnCount = 0;
			object anonymousHash;

            // get a collection of HashValues and count of each from CheckSum table where the count based on the hash column > 1 i.e. duplicates based on that hash type
            switch (hash)
            {
				case "ShaHash":
					anonymousHash = ShaHash();
					break;
				case "PerceptualHash":
					anonymousHash = PerCeptualHash();
					break;
                default:
					Serilog.Log.Error($"FindDupsUsingHash - Hash: {hash} not implemented, exiting.");
					return;
            }

			// calculate the number of rows returned in the anonymous type
			int anonymousCount = 0;
            foreach (var checkSumWithDup in (dynamic)anonymousHash)
			{
				anonymousCount += checkSumWithDup.Count;
			}
			Serilog.Log.Information($"FindDupsUsingHash - anonymousCount: {anonymousCount:N0}");


			foreach (var checkSumWithDup in (dynamic)anonymousHash)
            {
                if (verbose)
                {
					Serilog.Log.Information($"FindDupsUsingHash - checkSumWithDup.hashVal: {checkSumWithDup.hashVal}, {checkSumWithDup.Count}");
				}

				// get a collection of CheckSum.Ids from the rows with this hash value
				dynamic checkSums4Dup;
				switch (hash)
				{
					case "ShaHash":
						string shaHashVal = checkSumWithDup.hashVal;
						checkSums4Dup = from c in popsDbContext.CheckSums where c.Sha == shaHashVal select new { c.Id };
						break;
					case "PerceptualHash":
						decimal? hashVal = checkSumWithDup.hashVal;
						checkSums4Dup = from c in popsDbContext.CheckSums where c.PerceptualHash == hashVal select new { c.Id };
						break;
					default:
						Serilog.Log.Error($"FindDupsUsingHash - Hash: {hash} not implemented, exiting.");
						return;
				}


				// check that the count is correct
    //            if (checkSums4Dup.LongCount() != checkSumWithDup.Count)
    //            {
				//	Serilog.Log.Warning($"FindDupsUsingHash - checkSum4Dup.LongCount(): {checkSums4Dup.LongCount()} not equal to checkSumWithDup.Count: {checkSumWithDup.Count}.\n\tAnother process may be updating CheckSum.");
				//	continue;
				//}

                foreach (var checkSum4Dup in checkSums4Dup)
                {
					// move the projected values into a row from the class DupOnHash in order to pass to the method
					DupOnHash dupOnHash1 = new ()
					{
						CheckSumId = checkSum4Dup.Id,
						DupBasedOn = hash,
						BasedOnVal = checkSumWithDup.hashVal.ToString()
					};

					// Call CheckSumDup_upsert() to insert or update CheckSumDups and its child table CheckSumBasedOn
					CheckSumDup_upsert(dupOnHash1, verbose, ref insertCheckSumDupsCount, ref updateCheckSumDupsBasedOnCount, ref insertCheckSumDupsBasedOnCount);
				}

				if (++processedCount % 1000 == 0)
				{
					Serilog.Log.Information($"FindDupsUsingHash - {processedCount,6:N0}. Completed:{((processedCount * 100) / anonymousCount),3:N0}%.");
				}
			}

			_stopwatch.Stop();
			Serilog.Log.Information($"FindDupsUsingHash - insertCheckSumDupsCount: {insertCheckSumDupsCount:N0}, updateCheckSumDupsBasedOnCount: {updateCheckSumDupsBasedOnCount:N0}, execution time: {_stopwatch.Elapsed.TotalMinutes:N1} mins.\n{new String('=', 150)}");

			//////////////////////////////
			/// Local methods
			//////////////////////////////
			object ShaHash()
			{
				return	from c in popsDbContext.CheckSums
						where c.Sha != null
						group c by c.Sha
						into g
						where g.Count() > 1
						//orderby g.Count() descending
						select new { hashVal = g.Key, Count = g.Count() };
			}

			object PerCeptualHash()
            {
				return	from c in popsDbContext.CheckSums
						where c.PerceptualHash != null
						group c by c.PerceptualHash
						into g
						where g.Count() > 1
						//orderby g.Count() descending
						select new { hashVal = g.Key, Count = g.Count() };
			}
		}

        private static void CheckSumDup_upsert(DupOnHash dupOnHash, bool verbose, ref int insertCheckSumDupsCount, ref int updateCheckSumDupsBasedOnCount, ref int insertCheckSumDupsBasedOnCount)
        {
			PopsDbContext popsDbContext = new ();

			// check if a CheckSumDups row exists for this CheckSumId and hash values
			var checkSumDupPlusBasedOn = popsDbContext.CheckSumDups
											.Where(e => e.CheckSumId == dupOnHash.CheckSumId)
											.Select(e => new
											{
												e.Id,
												CheckSumDupsBasedOn = e.CheckSumDupsBasedOnRows.Select(b => new
												{
													b.CheckSumId,
													b.DupBasedOn,
													b.BasedOnVal
												})
											})
											.FirstOrDefault();


			if (checkSumDupPlusBasedOn is null)    // need to insert a new CheckSumDups row and its child row CheckSumDupsBasedOn
			{
                CheckSumDups checkSumDup1 = new()
                {
                    CheckSumId = dupOnHash.CheckSumId
                };

				// add the child row - CheckSumDupsBasedOn
				checkSumDup1.CheckSumDupsBasedOnRows.Add(
                    new CheckSumDupsBasedOn
                    {
                        CheckSumId = checkSumDup1.CheckSumId,
                        DupBasedOn = dupOnHash.DupBasedOn,
                        BasedOnVal = dupOnHash.BasedOnVal
                    });

				// save the parent ChecSumDup and child CheckSumDupsBasedOn
				popsDbContext.Add(checkSumDup1);

				insertCheckSumDupsCount++;

				if (verbose)
                {
                    Serilog.Log.Information($"FindDupsUsingHash - Added new CheckSumDup and CheckSumDupsBasedOn rows, checkSum.Id: {checkSumDup1.CheckSumId}, hash: {dupOnHash.DupBasedOn}.");
                }
            }
            else     // just add the new CheckSumDupsBasedOn row for this CheckSumDup. Delete any existing value first.
            {
				// Get the existing CheckSumDups row
				CheckSumDups checkSumDups = popsDbContext.CheckSumDups.Where(e => e.Id == checkSumDupPlusBasedOn.Id).FirstOrDefault();

				// Get the CheckSumDupsBasedOn row for this CheckSumDup and hash
				CheckSumDupsBasedOn checkSumDupsBasedOn1 = popsDbContext.CheckSumDupsBasedOn.Where(c => c.CheckSumId == checkSumDups.CheckSumId && c.DupBasedOn == dupOnHash.DupBasedOn).FirstOrDefault();
                if (checkSumDupsBasedOn1 is not null)
                {
					checkSumDupsBasedOn1.BasedOnVal = dupOnHash.BasedOnVal;

					if (verbose)
					{
						Serilog.Log.Information($"FindDupsUsingHash - Existing CheckSumDup, checkSum.Id: {checkSumDups.CheckSumId}, Updated CheckSumDupsBasedOn row with checkSum.Id: {checkSumDupsBasedOn1.CheckSumId}, " +
							$"hash: {dupOnHash.DupBasedOn}, checkSumDupsBasedOn1.BasedOnVal: {checkSumDupsBasedOn1.BasedOnVal}.");
					}
				}
				else
                {
					checkSumDups.CheckSumDupsBasedOnRows.Add(
						new CheckSumDupsBasedOn
						{
							CheckSumId = dupOnHash.CheckSumId,
							DupBasedOn = dupOnHash.DupBasedOn,
							BasedOnVal = dupOnHash.BasedOnVal
						});

					popsDbContext.Update(checkSumDups);
					updateCheckSumDupsBasedOnCount++;

					if (verbose)
					{
						Serilog.Log.Information($"FindDupsUsingHash - Existing CheckSumDup, checkSum.Id: {checkSumDups.CheckSumId}, added CheckSumDupsBasedOn row with checkSum.Id: {checkSumDups.CheckSumId}, " +
							$"hash: {dupOnHash.DupBasedOn}, dupOnHash.BasedOnVal: {dupOnHash.BasedOnVal}.");
					}
				}

			}
 
			popsDbContext.SaveChanges();
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
			PopsDbContext popsDbContext = new();
			List<CheckSum> checkSums = new();

			int _count = 0;
			FileInfo[] _files = folder.GetFiles("*.JPG", SearchOption.AllDirectories);
			Serilog.Log.Information($"ProcessFiles - Found {_files.Length:N0} files to process.");

            //Parallel.ForEach(_files, file =>
            //{
            //	// instantiate a new CheckSum object for the file
            //	CheckSum checkSum = new CheckSum
            //	{
            //		Folder = file.DirectoryName,
            //		TheFileName = file.Name,
            //		FileExt = file.Extension.ToUpper(),
            //		FileSize = (int)file.Length,
            //		FileCreateDt = file.CreationTime,
            //		TimerMs = 0,
            //	};

            //	checkSums.Add(checkSum);
            //	//popsDbContext.Add(checkSum);

            //	if (++_count % 1000 == 0)
            //	{
            //		process100Watch.Stop();
            //		Serilog.Log.Information($"ProcessFiles - {_count}. Last 100 in {process100Watch.Elapsed.Seconds} secs. Completed: {(_count * 100) / _files.Length}%. Processing folder: {file.DirectoryName}");
            //		process100Watch.Reset();
            //		process100Watch.Start();
            //	}
            //});

            // Process all the JPG files in the source directory tree
            foreach (FileInfo fileInfo in _files)
            {
                // calculate the SHA string for the file and return it with the time taken in ms in a tuple
                //(string SHA, int timerMs) = CalcSHA(fileInfo);

                // instantiate a new CheckSum object for the file
                CheckSum checkSum = new CheckSum
                {
                    //Sha = SHA,
                    Folder = fileInfo.DirectoryName,
                    TheFileName = fileInfo.Name,
                    FileExt = fileInfo.Extension.ToUpper(),
                    FileSize = (int)fileInfo.Length,
                    FileCreateDt = fileInfo.CreationTime,
                    TimerMs = 0,
                };

                popsDbContext.Add(checkSum);

                if (++_count % 1000 == 0)
                {
                    process100Watch.Stop();
                    Serilog.Log.Information($"ProcessFiles - {_count}. Last 100 in {process100Watch.Elapsed.Seconds} secs. Completed: {(_count * 100) / _files.Length}%. Processing folder: {fileInfo.DirectoryName}");
                    process100Watch.Reset();
                    process100Watch.Start();
                }
            }

            popsDbContext.SaveChanges();
			return _count;
		}


		// Process the duplicate rows in the CheckSum table and report files to delete or report AND delete.
		public static void DeleteDupes(bool delete)
		{
			throw new NotImplementedException();

			//System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

			//// 1. Make sure there are some duplicate rows in the CheckSum table
			//const string sql = @"select SHA,Count(*) as DupeCount from CheckSum group by SHA having Count(*)>1";

			//Serilog.Log.Information($"DeleteDupes - Starting DeleteDupes: --delete: {delete} sql: {sql}");

			//using var cnn = new SqlConnection(ConnectionString);
			//var _CheckSumHasDuplicates = cnn.Query<CheckSumHasDuplicates>(sql).AsList();

			//if (_CheckSumHasDuplicates.Count == 0)
			//{
			//	Serilog.Log.Error("DeleteDupes - Abort. No duplicates found in CheckSum{new String('-', 150)}\n");
			//	return;
			//}
			//Serilog.Log.Information($"DeleteDupes - {_CheckSumHasDuplicates.Count} duplicates found in the CheckSum table.");

			//// Main processing foreach loop local function
			//ProcessDeleteDupes();

			//_stopwatch.Stop();
			//Serilog.Log.Information($"INFO\t- Total execution time: {_stopwatch.Elapsed.Minutes} mins.{new String('-', 150)}\n");

			///////////////////
			//// local function
			///////////////////
			//void ProcessDeleteDupes()
			//{
			//	foreach (var aCheckSumHasDuplicates in _CheckSumHasDuplicates)
			//	{
			//		using var cnn = new SqlConnection(ConnectionString);
			//		var _CheckSum = cnn.Query<CheckSum>($"select * from CheckSum where SHA='{aCheckSumHasDuplicates.SHA}' order by LEN(TheFileName) desc").AsList();
			//		if (_CheckSum.Count < 2)
			//		{
			//			Serilog.Log.Error($"ProcessDeleteDupes - Only found {_CheckSum.Count} CheckSum rows with SHA: {aCheckSumHasDuplicates.SHA}, should be >= 2.");
			//			continue;
			//		}

			//		Serilog.Log.Information($"ProcessDeleteDupes - {_CheckSum.Count} CheckSum rows for duplicate SHA: {aCheckSumHasDuplicates.SHA}");

			//		// get the CheckSum row with the longest name NB could be the same
			//		var aCheckSum = _CheckSum[0];
			//		if (delete)
			//		{
			//			FileInfo deleteFileInfo = new FileInfo(Path.Combine(aCheckSum.Folder, aCheckSum.TheFileName));
			//			if (deleteFileInfo.Exists)
			//			{
			//				deleteFileInfo.Delete();
			//				using IDbConnection db = new SqlConnection(ConnectionString);
			//				db.Execute($"delete from dbo.CheckSum where Id={aCheckSum.Id}");
			//				Serilog.Log.Warning($"ProcessDeleteDupes - Deleted the SHA with the longest duplicate name was id: {aCheckSum.Id}\tThe name was: {aCheckSum.TheFileName}\tThe folder was: {aCheckSum.Folder}");
			//			}
			//			else
			//			{
			//				Serilog.Log.Error($"ProcessDeleteDupes - The duplicate to delete {aCheckSum.TheFileName}\tdoes not now exits in folder: {aCheckSum.Folder}");
			//			}
			//		}
			//		else
			//		{
			//			Serilog.Log.Information($"ProcessDeleteDupes - No delete. The SHA with the longest duplicate name is id: {aCheckSum.Id}\tThe name is: {aCheckSum.TheFileName}\tThe folder is: {aCheckSum.Folder}");
			//		}
			//	}
			//}
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
				FileStream fi = fileInfo.OpenRead();
				fi.Position = 0;
				return fi;
			}

			throw new FileNotFoundException(fileInfo.FullName);
		}


/*		static void BuildConfig(IConfigurationBuilder builder)
		{
			builder.SetBasePath(System.IO.Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true);
		}
*/
	}
}
