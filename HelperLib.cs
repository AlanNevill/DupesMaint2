using CoenM.ImageHash.HashAlgorithms;
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
using MetadataExtractor.Formats;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Avi;
using MetadataExtractor.Formats.Bmp;
using MetadataExtractor.Formats.Eps;
using MetadataExtractor.Formats.FileSystem;
using MetadataExtractor.Formats.FileType;
using MetadataExtractor.Formats.Gif;
using MetadataExtractor.Formats.Heif;
using MetadataExtractor.Formats.Ico;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Mpeg;
using MetadataExtractor.Formats.Netpbm;
using MetadataExtractor.Formats.Pcx;
using MetadataExtractor.Formats.Photoshop;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.QuickTime;
using MetadataExtractor.Formats.Raf;
using MetadataExtractor.Formats.Tiff;
using MetadataExtractor.Formats.Tga;
using MetadataExtractor.Formats.Wav;
using MetadataExtractor.Formats.WebP;
using MetadataExtractor.Util;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Data.Common;
using System.Threading.Tasks;
using System.Threading;
using DirectoryList = System.Collections.Generic.IReadOnlyList<MetadataExtractor.Directory>;
using CoenM.ImageHash;

namespace DupesMaint2;

public sealed class HelperLib
{
	private static IConfiguration? _config;

	public static string? ConnectionString => Program._cnStr;

    private static PhotosDbContext? photosCtx;

    // constructor
    public HelperLib(IConfiguration config, PhotosDbContext photosDbContext)
    {
		_config = config;
		photosCtx = photosDbContext;
	}

	// map file extensions to media group
	static readonly List<FileExtensionTypes> _fileExtensionTypes = new()
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

	// list of file extensions that can be hashed by the metadata extractor library
	static readonly List<FileExtensionTypes> _fileExtensionTypes2Hashing = new()
	{
		new FileExtensionTypes { Type = ".BMP", Group = "Photo" },
		new FileExtensionTypes { Type = ".GIF", Group = "Photo" },
		new FileExtensionTypes { Type = ".JPEG", Group = "Photo" },
		new FileExtensionTypes { Type = ".JPG", Group = "Photo" },
		new FileExtensionTypes { Type = ".PNG", Group = "Photo" },
		new FileExtensionTypes { Type = ".MP4", Group = "Video" },
		new FileExtensionTypes { Type = ".MPG", Group = "Video" },
		new FileExtensionTypes { Type = ".3GP", Group = "Video" },
		new FileExtensionTypes { Type = ".AVI", Group = "Video" },
		new FileExtensionTypes { Type = ".MP", Group = "Video" },
		new FileExtensionTypes { Type = ".MOV", Group = "Video" },
		new FileExtensionTypes { Type = ".MTS", Group = "Video" },
        new FileExtensionTypes { Type = ".TIFF", Group = "Photo"},
		new FileExtensionTypes { Type = ".WMV", Group = "Video" },
		new FileExtensionTypes { Type = ".Webp", Group = "Photo"},
        };


	/// <summary>
	/// Root Command - LoadFileType
	/// Read all the files in a folder root tree and process all the files of the selected media type
	/// </summary>
	/// <param name="folder">DirectoryInfo - root folder to the folder structure.</param>
	/// <param name="fileType">string - Either 'Photo' or 'Video'.</param>
	/// <param name="replace">Bool - If true truncate table CheckSum else just add rows.</param>
	/// <param name="verbose">Bool - If true then verbose logging.</param>
	public static void LoadFileType(DirectoryInfo folder, string fileType, bool replace, bool verbose)
	{
		System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
		Log.Information($"""
			LoadFileType - Starting
			root source folder:			{folder.FullName}
			fileType:					{fileType}
			Truncate table CheckSum:	{replace}
			Verbose logging:			{verbose}
		""");

		if (replace)
		{
			using IDbConnection db = new SqlConnection(ConnectionString);
			db.Execute("truncate table dbo.CheckSum");
		}

		// main processing
		List<CheckSum> checkSums = new();

		int processCount = 0, dropCount = 0;
		FileInfo[] _files = folder.GetFiles("*", SearchOption.AllDirectories);
		Log.Information($"LoadFileType - Found {_files.Length:N0} files under the root folder.");

		// LoadFileType all the JPG files in the source directory tree
		foreach (FileInfo fileInfo in _files)
		{
			var type = _fileExtensionTypes.Find(e => e.Type == fileInfo.Extension.ToUpper());
			if (type is null)
			{
				dropCount++;
				Log.Warning($"LoadFileType - File extension: {fileInfo.Extension} not found in fileExtensionTypes.");
				continue;
			}

			// check that the file is of the selected media type
            if (type.Group != fileType)
            {
				dropCount++;
				if (verbose) Log.Information($"LoadFileType - Ignored file extension: {fileInfo.Extension}, not of media type: {fileType}.");
				continue;
			}

			int existsCount = photosCtx!.CheckSum.Where(x => x.FileFullName == fileInfo.FullName).Count();

            if (existsCount != 0)
            {
				Log.Warning($"LoadFileType - fileInfo.FullName: {fileInfo.FullName} already loaded in CheckSum table.");
				continue;
			}

			// instantiate a new CheckSum object for the file
			CheckSum checkSum = new CheckSum
			{
				Folder = fileInfo.DirectoryName!,
				TheFileName = fileInfo.Name,
				FileExt = fileInfo.Extension.ToUpper(),
				FileSize = (int)fileInfo.Length,
				FileCreateDt = fileInfo.CreationTime,
				MediaFileType = fileType,
				TimerMs = 0,
			};

            photosCtx.Add(checkSum);
			if (verbose) Log.Information($"LoadFileType - File {checkSum.FileFullName}, was added to CheckSum table.");

			if (++processCount % 1000 == 0)
				Log.Information($"LoadFileType - {processCount,6:N0}. Completed: {(processCount * 100) / _files.Length}%. Processing folder: {fileInfo.DirectoryName}");
		}

        photosCtx!.SaveChanges();

		_stopwatch.Stop();
		Log.Information($"""
			LoadFileType - Total execution time: {_stopwatch.Elapsed.Minutes:N1} mins. 
				processCount:	{processCount:N0}, 
				dropCount:		{dropCount:N0}
			{new String('-', 135)}
		""");
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
        System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
		int processedCount = 0, dropCount = 0, totalCount = 0;
            
		Log.Information($"""
		CalculateHashes - Starting
			ShaHash:		{ShaHash}
			averageHash:	{averageHash}
			differenceHash: {differenceHash}
			perceptualHash: {perceptualHash}
			verbose:		{verbose}
		""");

		List<CheckSum> checkSums = photosCtx!.CheckSum.ToList();

        Log.Information($"CalculateHashes - Starting Parallel.ForEach, checkSums.Count: {checkSums.Count:N0}");
            
		var parallel = Parallel.ForEach(checkSums, checkSum =>
		{
            try    // Calculate the requested hashes
            {
                // drop where MediaFileType is not 'Unknown'
                if (checkSum.MediaFileType == "Unknown" )
				{
					Interlocked.Increment(ref dropCount);
					Log.Warning($"CalculateHashes - id: {checkSum.Id}, checkSum.MediaFileType == \"Unknown\"");
					return;
				}

				var type = _fileExtensionTypes2Hashing.Find(e => e.Type == checkSum.FileExt);
				if (type is null)
				{
					Interlocked.Increment(ref dropCount);
                    Log.Warning($"CalculateHashes - id: {checkSum.Id}, checkSum.FileExt not found in _fileExtensionTypes2Hashing, checkSum.FileExt: {checkSum.FileExt}");
                    return;
				}

                // type can have hashes calculated
                FileInfo fileInfo = new(checkSum.FileFullName!);

				// calculate the Sha theHash
				if (ShaHash && checkSum.Sha is null)
				{
					checkSum.Sha = calcShaHash2(fileInfo);
				}

				// image processor seems to have a limit on file size
				if (fileInfo.Length > 71_000_000)
				{
					Interlocked.Increment(ref dropCount);
                    Log.Warning($"CalculateHashes - id: {checkSum.Id}, fileInfo.Length > 71_000_000, fileInfo.Length: {fileInfo.Length:N0}");
                    return;
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
				checkSum.MediaFileType = "Unknown";
				checkSum.FormatValid = "N";
				Log.Fatal(exc, $"CalculateHashes - id: {checkSum.Id}\n{new String('-', 150)}\n");
			}
			catch (SixLabors.ImageSharp.InvalidImageContentException iIcE)
			{
				checkSum.FormatValid = "N";
				Log.Fatal(iIcE,$"CalculateHashes - id: {checkSum.Id}\n{new String('-', 150)}\n");
			}
			catch (FileNotFoundException fnfEx)
			{
				Log.Fatal(fnfEx,$"CalculateHashes - id: {checkSum.Id}\n{new String('-', 150)}\n");
			}
			catch (Exception ex)
			{
				Log.Fatal(ex,$"CalculateHashes - id: {checkSum.Id}\n{new String('-', 150)}\n");
			}

			if (verbose) Log.Information($"CalculateHashes - id: {checkSum.Id}, checkSum.AverageHash: {checkSum.AverageHash}, checkSum.DifferenceHash: {checkSum.DifferenceHash}, checkSum.PerceptualHash: {checkSum.PerceptualHash}.");

			Interlocked.Increment(ref processedCount);
			totalCount = 0; 
			Interlocked.Add(ref totalCount, processedCount);
			Interlocked.Add(ref totalCount, dropCount);

			if ((totalCount) % 1000 == 0)
				Log.Information($"CalculateHashes - {totalCount,6:N0}. Completed:{(((totalCount) * 100) / checkSums.Count),3:N0}%.");

		}); // end of Parallel.ForEach


        // update the database
        photosCtx!.SaveChanges();

		_stopwatch.Stop();

        Log.Information($"""
		CalculateHashes - Finished processing
				parallel.ToString:	{parallel}
				checkSums.Count:	{photosCtx.CheckSum.LongCount():N0}
				processedCount:		{processedCount:N0}
				dropCount:			{dropCount:N0}
				execution time:		{_stopwatch.Elapsed.TotalMinutes:N1} mins
		{new String('-', 135)}
		""");


        ////////////////
        // local methods
        ////////////////
        ulong calcAverageHash(FileInfo fileInfo)
        {

            var averageHash = new CoenM.ImageHash.HashAlgorithms.AverageHash();    // instaniate the CoenM.ImageHash.HashAlgorithms;
                                                                                   //using var stream = File.OpenRead(fileInfo.FullName);
            return averageHash.Hash(File.OpenRead(fileInfo.FullName));
        }

        ulong calcDifferenceHash(FileInfo fileInfo)
        {
            var differenceHash = new CoenM.ImageHash.HashAlgorithms.DifferenceHash();    // instaniate the CoenM.ImageHash.HashAlgorithms;
                                                                                         //using var stream = File.OpenRead(fileInfo.FullName);
            return differenceHash.Hash(File.OpenRead(fileInfo.FullName));
        }

        ulong calcPerceptualHash(FileInfo fileInfo)
        {
            var perceptualHash = new CoenM.ImageHash.HashAlgorithms.PerceptualHash();    // instaniate the CoenM.ImageHash.HashAlgorithms;
                                                                                         //using var stream = File.OpenRead(fileInfo.FullName);
            return perceptualHash.Hash(File.OpenRead(fileInfo.FullName));
        }

    }

	public static string calcShaHash2(FileInfo fileInfo)
	{
		// calculate the SHA256 checkSum for the file and return it with the elapsed processing time using a tuple

	FileStream fs = fileInfo.OpenRead();
	//fs.Position = 0;

	// ComputeHash - returns byte array  
	byte[] bytes = SHA256.Create().ComputeHash(fs);

	// BitConverter used to put all bytes into one string, hyphen delimited  
	return BitConverter.ToString(bytes);
}

	/// <summary>
	/// Move all but the largest CheckSum files with the same PerceptualHash to a folder on the H drive
	/// </summary>
	/// <param name="verbose"></param>
	public static void PerceptualHash_Move2Hdrive(bool verbose)
        {
		System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
		int counter = 0;

		var perceptualHashes = from p in photosCtx!.CheckSum
							   where p.PerceptualHash != null
							   group p by p.PerceptualHash into g
							   where g.Count() > 1
							   orderby g.Count() descending
							   select new { g.Key, Count = g.Count() };

		Log.Information($"perceptualHashes.LongCount(): {perceptualHashes.LongCount():N0}");
  
		foreach (var perceptualHash in perceptualHashes)
		{
            photosCtx.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));

			List<CheckSum> checkSums = photosCtx.CheckSum
				.Where(y => y.PerceptualHash == perceptualHash.Key && y.Folder.StartsWith(@"C:\Users\User"))
				.ToList();

            if (checkSums.Count == 0)
			continue;

			int maxSize = checkSums.Max(y => (int)y.FileSize!);
			Log.Information($"perceptualHash.Key: {perceptualHash.Key}, checkSums.Count: {checkSums.Count}, maxSize: {maxSize:N0}");

                foreach (var checkSum in checkSums.OrderByDescending(v => v.FileSize))
                {
                    if (checkSum.FileSize == maxSize)
                    {
						Log.Information($"PerceptualHash_Move2Hdrive - checkSum.Id: {checkSum.Id}, has maxSize: {maxSize:N0} and will not be moved");
						continue;
				}

				// Move this CheckSum file
				MoveTheFile(checkSum, photosCtx);

				counter++;
			}
		}

		_stopwatch.Stop();
		Log.Information($"PerceptualHash_Move2Hdrive - Total execution time: {_stopwatch.Elapsed.TotalMinutes:N0} mins.\n{new String('-', 150)}");

		//////////////////
		// Local functions
		//////////////////
		void MoveTheFile(CheckSum checkSum, PhotosDbContext photoCtx)
        {
			// Generate the new folder and create if necessary
			DirectoryInfo directoryInfo = new(Path.Combine(@"H:\PerceptualHashes", checkSum.PerceptualHash.ToString()!));
			if (!directoryInfo.Exists)
				directoryInfo.Create();

            // Move the file
            try
            {
				File.Move(checkSum.FileFullName, Path.Combine(directoryInfo.FullName, checkSum.TheFileName),true);
            }
			catch (FileNotFoundException fnf)    // source file not found
            {
				Log.Error($"PerceptualHash_Move2Hdrive - File not found, checkSum.Id: {checkSum.Id}\n{fnf}");
            }

			// Update the checkSum row
			checkSum.Folder = directoryInfo.FullName;
			photosCtx!.SaveChanges();

			if (verbose) Log.Information($"PerceptualHash_Move2Hdrive - checkSum.Id: {checkSum.Id}, checkSum.FileSize: {checkSum.FileSize:N0} was moved to: {checkSum.Folder}");
		}

	}

	/// <summary>
	/// ONLY WORKS FOR ShaHash & PerceptualHash
	/// Command6 - FindDupsUsingHash
	/// </summary>
	/// <param name="hashType"></param>
	/// <param name="verbose"></param>
	public void FindDupsUsingHash(string hashType, bool verbose)
	{
		System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
		Log.Information($"""
		FindDupsUsingHash - Starting
			theHash:	{hashType}
			verbose:	{verbose}
		""");

		int processedCount = 0, insertCheckSumDupsBasedOnCount = 0;
		object anonymousHash;

            // get a collection of HashValues and count of each from CheckSum table where the count based on the theHash column > 1 i.e. duplicates based on that theHash type
            switch (hashType)
            {
			case "Sha":
				anonymousHash = ShaHash();
				break;
			case "Perceptual":
				anonymousHash = PerCeptualHash();
				break;
            default:
				Log.Error($"FindDupsUsingHash - Hash: {hashType} not implemented, exiting.");
				return;
            }

		// calculate the number of rows returned in the anonymous type
		int anonymousCount = 0;
        foreach (var theHash in (dynamic)anonymousHash)
		{
			anonymousCount += theHash.Count;
		}
		Log.Information($"FindDupsUsingHash - anonymousCount: {anonymousCount:N0}");

		// process each hashType value
		foreach (var theHash in (dynamic)anonymousHash)
        {
            if (verbose) Log.Information($"FindDupsUsingHash - theHash.hashVal: {theHash.hashVal}, {theHash.Count}");

			// get a collection of CheckSums from the rows with this theHash value
			List<CheckSum> checkSums;
			switch (hashType)
			{
				case "Sha":
					string shaHashVal = theHash.hashVal;
					checkSums = photosCtx!.CheckSum.Where(a => a.Sha == shaHashVal).Include(z => z.CheckSumDupsBasedOn).ToList();
					break;
				case "Perceptual":
					decimal? hashVal = theHash.hashVal;
					checkSums = photosCtx!.CheckSum.Where(a => a.PerceptualHash == hashVal).Include(z => z.CheckSumDupsBasedOn).ToList();
					break;
				default:
					Log.Error($"FindDupsUsingHash - Hash: {hashType} not implemented, exiting.");
					return;
			}

			// process the collection of CheckSum ids
            foreach (CheckSum checkSum in checkSums)
            {
				CheckSumDupsBasedOn checkSumDupsBasedOn_Exists = checkSum.CheckSumDupsBasedOn.FirstOrDefault(a => a.BasedOnVal == theHash.hashVal)!;

				// if the CheckSumDupsbasedOn does not exist then add it.
				if (checkSumDupsBasedOn_Exists is null)
				{
					CheckSumDupsBasedOn checkSumDupsBasedOn = new ()
					{
						CheckSumId = checkSum.Id,
						DupBasedOn = hashType,
						BasedOnVal = theHash.hashVal.ToString()
					};

					photosCtx.CheckSumDupsBasedOn.Add(checkSumDupsBasedOn);
					insertCheckSumDupsBasedOnCount++;
                }
			}

			if (++processedCount % 1000 == 0)
				Log.Information($"FindDupsUsingHash - {processedCount,6:N0}. Completed:{((processedCount * 100) / anonymousCount),3:N0}%.");
		}

		photosCtx!.SaveChanges();

		_stopwatch.Stop();
		Log.Information($"""
		FindDupsUsingHash - Finished
			hashType:							{hashType}
			insertCheckSumDupsBasedOnCount: {insertCheckSumDupsBasedOnCount:N0}
			execution time:					{_stopwatch.Elapsed.TotalMinutes:N1} mins.
		{new String('=', 135)}
		""");

		//////////////////////////////
		/// Local methods
		//////////////////////////////
		object ShaHash() => 
				from c in photosCtx!.CheckSum
				where c.Sha != null
				group c by c.Sha
				into g
				where g.Count() > 1
				//orderby g.Count() descending
				select new { hashVal = g.Key, Count = g.Count() };

		object PerCeptualHash() =>
				from c in photosCtx!.CheckSum
				where c.PerceptualHash != null
				group c by c.PerceptualHash
				into g
				where g.Count() > 1
				//orderby g.Count() descending
				select new { hashVal = g.Key, Count = g.Count() };
	}


        private static void CheckSumDupsBasedOn_upsert(CheckSumDupsBasedOn checkSumDupsBasedOn, bool verbose, ref int insertCheckSumDupsCount, ref int updateCheckSumDupsBasedOnCount, ref int insertCheckSumDupsBasedOnCount)
        {
		//PopsDbContext photosDbContext = new ();

		//// check if a CheckSumDups row exists for this CheckSumId and theHash values
		//var checkSumDupPlusBasedOn = photosDbContext.CheckSumDups
		//								.Where(e => e.ChecksumId == checkSumDupsBasedOn.CheckSumId)
		//								.Select(e => new
		//								{
		//									e.Id,
		//									CheckSumDupsBasedOn = e.CheckSumDupsBasedOn.Select(b => new
		//									{
		//										b.CheckSumId,
		//										b.DupBasedOn,
		//										b.BasedOnVal
		//									})
		//								})
		//								.FirstOrDefault();


		//if (checkSumDupPlusBasedOn is null)    // need to insert a new CheckSumDups row and its child row CheckSumDupsBasedOn
		//{
  //              CheckSumDups checkSumDup1 = new()
  //              {
  //                  ChecksumId = checkSumDupsBasedOn.CheckSumId
  //              };

		//	// add the child row - CheckSumDupsBasedOn
		//	checkSumDup1.CheckSumDupsBasedOn.Add(
  //                  new CheckSumDupsBasedOn
  //                  {
  //                      CheckSumId = checkSumDup1.ChecksumId,
  //                      DupBasedOn = checkSumDupsBasedOn.DupBasedOn,
  //                      BasedOnVal = checkSumDupsBasedOn.BasedOnVal
  //                  });

		//	// save the parent ChecSumDup and child CheckSumDupsBasedOn
		//	photosDbContext.Add(checkSumDup1);

		//	insertCheckSumDupsCount++;

		//	if (verbose)
  //              {
  //                  Log.Information($"FindDupsUsingHash - Added new CheckSumDup and CheckSumDupsBasedOn rows, checkSum.Id: {checkSumDup1.ChecksumId}, theHash: {checkSumDupsBasedOn.DupBasedOn}.");
  //              }
  //          }
  //          else     // just add the new CheckSumDupsBasedOn row for this CheckSumDup. Delete any existing value first.
  //          {
		//	// Get the existing CheckSumDups row
		//	CheckSumDups checkSumDups = photosDbContext.CheckSumDups.Where(e => e.Id == checkSumDupPlusBasedOn.Id).FirstOrDefault();

		//	// Get the CheckSumDupsBasedOn row for this CheckSumDup and theHash
		//	CheckSumDupsBasedOn checkSumDupsBasedOn1 = photosDbContext.CheckSumDupsBasedOn.Where(c => c.CheckSumId == checkSumDups.ChecksumId && c.DupBasedOn == checkSumDupsBasedOn.DupBasedOn).FirstOrDefault();
  //              if (checkSumDupsBasedOn1 is not null)
  //              {
		//		checkSumDupsBasedOn1.BasedOnVal = checkSumDupsBasedOn.BasedOnVal;

		//		if (verbose)
		//			Log.Information($"FindDupsUsingHash - Existing CheckSumDup, checkSum.Id: {checkSumDups.ChecksumId}, Updated CheckSumDupsBasedOn row with checkSum.Id: {checkSumDupsBasedOn1.CheckSumId}, " +
		//				$"theHash: {checkSumDupsBasedOn.DupBasedOn}, checkSumDupsBasedOn1.BasedOnVal: {checkSumDupsBasedOn1.BasedOnVal}.");
		//	}
		//	else
  //              {
		//		checkSumDups.CheckSumDupsBasedOn.Add(
		//			new CheckSumDupsBasedOn
		//			{
		//				CheckSumId = checkSumDupsBasedOn.CheckSumId,
		//				DupBasedOn = checkSumDupsBasedOn.DupBasedOn,
		//				BasedOnVal = checkSumDupsBasedOn.BasedOnVal
		//			});

		//		photosDbContext.Update(checkSumDups);
		//		updateCheckSumDupsBasedOnCount++;

		//		if (verbose)
		//			Log.Information($"FindDupsUsingHash - Existing CheckSumDup, checkSum.Id: {checkSumDups.ChecksumId}, added CheckSumDupsBasedOn row with checkSum.Id: {checkSumDups.ChecksumId}, " +
		//				$"theHash: {checkSumDupsBasedOn.DupBasedOn}, checkSumDupsBasedOn.BasedOnVal: {checkSumDupsBasedOn.BasedOnVal}.");
		//	}

		//}
 
		//photosDbContext.SaveChanges();
	}

	/// <summary>
	/// Command2 - LoadFileType all the files in the folder tree passed in and add rows to CheckSum table
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
				Folder = fi.DirectoryName!,
				TheFileName = fi.Name,
				FileExt = fi.Extension,
				FileSize = (int)fi.Length,
				FileCreateDt = fi.CreationTime,
				CreateDateTime = _CreateDateTime
			};

			// insert into DB table
			HelperLib.CheckSum_ins2(checkSum);


			_count++;

			if (_count % 1000 == 0)
			{
				Log.Information($"ProcessEXIF - {_count,6:N0}. Completed: {((_count * 100) / _files.Length)}%. Processing folder: {fi.DirectoryName}");
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

		Log.Information($"ProcessAnEXIF - image: {image.FullName}");

		foreach (MetadataExtractor.Directory _directory in directories)
		{
			foreach (Tag tag in _directory.Tags)
			{
				Log.Information($"[{_directory.Name}]\t - [{tag.Name}] = [{tag.Description}]");
			}
		}
		Serilog.Log.Information($"ProcessAnEXIF - Finished: {image.FullName}\n{new String('-', 150)}");
	}


	/// <summary>
	/// Move files from Pictures/CameraRoll folder to the correct date based folder under Photos root folder.
	/// Assumes that the CheckSum table has been loaded with the 'C:\Users\User\OneDrive\Pictures\Camera Roll' folder.
	/// Command4
	/// </summary>
	/// <param name="mediaFileType">Either 'Photo' or 'Video' </param>
	/// <param name="verbose">Verbose logging</param>
	public static void CameraRoll_Move(string mediaFileType, bool verbose)
	{
		Serilog.Log.Information($"CameraRoll_Move - Starting\n\tmediaFileType: {mediaFileType}\n\tverbose: {verbose}\n");
		System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

		int processedCount=0, dropCount = 0;

		// Get all the CheckSum rows where the folder is 'C:\Users\User\OneDrive\Pictures\Camera Roll' and the MediaFileType = parameter
		List<CheckSum> checkSum = photosCtx!.CheckSum.Where(a => a.Folder == @"C:\Users\User\OneDrive\Pictures\Camera Roll" && a.MediaFileType == mediaFileType)
									.ToList();
            if (checkSum.Count == 0)
            {
                Serilog.Log.Warning($"CameraRoll_Move - Abort. No rows found for MediaFileType: {mediaFileType}\n{new String('-', 150)}\n");
                return;
            }
		Serilog.Log.Information($"CameraRoll_Move - {checkSum.Count:N0} rows found.");

            switch (mediaFileType)
            {
			case "Photo":
				Photos_Process();
			break;
			case "Video":
				throw new NotImplementedException("Videos not yet implemented");
			default:
				break;
		}

        _stopwatch.Stop();
		Log.Information($"CameraRoll_Move- processedCount: {processedCount:N0}, dropCount: {dropCount:N0}");
		Log.Information($"CameraRoll_Move- Total execution time: {_stopwatch.Elapsed.TotalSeconds} secs.\n{new String('-', 150)}\n");

        ///////////////////
        //// local methods
        ///////////////////
        void Photos_Process()
		{
			string _sCreateDateTime;

			foreach (var row in checkSum)
                {
				// get the EXIF date					
				_sCreateDateTime = CreateDate_Extract(row.FileFullName);
                    if (string.IsNullOrEmpty(_sCreateDateTime))
                    {
					Serilog.Log.Warning($"CameraRoll_Move - No EXIF date for {row.Id}, {row.FileFullName}");
					dropCount++;
					continue;
				}
                    if (!DateTime.TryParse(_sCreateDateTime, out DateTime createDateTime))
                    {
                        Serilog.Log.Warning($"CameraRoll_Move - _sCreateDateTime: {_sCreateDateTime} - not valid date, row: {row.Id}");
					dropCount++;
					continue;
                    }

                    // format the target folder
                    string targetFile = Path.Combine(@"C:\Users\User\OneDrive\Photos", 
					createDateTime.Year.ToString(), 
					createDateTime.Month.ToString("00"),
					row.TheFileName);

				FileInfo fileInfo = new(row.FileFullName);

				try
				{
					fileInfo.MoveTo(targetFile);

					// if the file was successfully moved then update the CheckSum row Folder column
					row.Folder = Path.Combine(@"C:\Users\User\OneDrive\Photos",
												createDateTime.Year.ToString(),
												createDateTime.Month.ToString("00"));
					processedCount++;

					if (verbose) Log.Information($"CameraRoll_Move - file: {row.FileFullName} was moved to {targetFile}");
				}
				catch (IOException ioEXC)
                    {
					dropCount++;
					Log.Error($"CameraRoll_Move - IO exception moving file id: {row.Id}, {row.FileFullName}\nto {targetFile}\n{ioEXC}\n");
				}
				catch (Exception exc)
                    {
					Log.Error($"CameraRoll_Move - Exception moving file id: {row.Id}, {row.FileFullName}\nto {targetFile}\n{exc}\n");
					throw;
                    }
			}
            photosCtx!.SaveChanges();
		}

		// get the EXIF TagDateTimeOriginal date from the photo's EXIF data
		string CreateDate_Extract(string fileFullName)
		{
			var directories = GetMetadata(fileFullName);
			var _ExifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

			if (_ExifSubIfdDirectory is not null)
			{
				string _sCreateDateTime = _ExifSubIfdDirectory.GetDescription(ExifSubIfdDirectory.TagDateTimeOriginal)!;
				if (!string.IsNullOrEmpty(_sCreateDateTime) && !_sCreateDateTime.Equals("0000:00:00 00:00:00"))
				{
					if (_sCreateDateTime[0..10].IndexOf(':') > -1)
					{
						return _sCreateDateTime[0..10].Replace(':', '-') + _sCreateDateTime[10..];
					}
				}
			}
			return string.Empty;
		}
	}


	// calculate the SHA256 checkSum for the file and return it with the elapsed processing time using a tuple
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

		// call the stored procedure
		using IDbConnection db = new SqlConnection(ConnectionString);
		db.Execute("dbo.spCheckSum_ins", p, commandType: CommandType.StoredProcedure);
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

		// Ensure the log promintently shows the database being used
		var CnStr = new SqlConnectionStringBuilder(ConnectionString);

		// Log the assembly version number
		string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version!.ToString();
		Log.Information(new String('=', 60));
		Log.Information($"DupesMaint2 v{assemblyVersion} - starting using DATABASE: {CnStr.InitialCatalog.ToUpper()}");
		Log.Information(new String('=', 60));
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


	public static DirectoryList GetMetadata(string filePath)
	{
		var directories = new List<MetadataExtractor.Directory>();

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                directories.AddRange(ReadMetadata(stream));
            }

            directories.Add(new FileMetadataReader().Read(filePath));

		return directories;
	}


	/// <summary>Reads metadata from an <see cref="Stream"/>.</summary>
	/// <param name="stream">A stream from which the file data may be read.  The stream must be positioned at the beginning of the file's data.</param>
	/// <returns>A list of <see cref="Directory"/> instances containing the various types of metadata found within the file's data.</returns>
	/// <exception cref="ImageProcessingException">The file type is unknown, or processing errors occurred.</exception>
	/// <exception cref="Exception"/>
	public static DirectoryList ReadMetadata(Stream stream)
	{
		// get the media file type from the file
		var fileType = FileTypeDetector.DetectFileType(stream);

		var directories = new List<MetadataExtractor.Directory>();

#pragma warning disable format

		try 
		{					
			directories.AddRange(fileType switch
			{
				FileType.Arw       => TiffMetadataReader.ReadMetadata(stream),
				FileType.Avi       => AviMetadataReader.ReadMetadata(stream),
				FileType.Bmp       => BmpMetadataReader.ReadMetadata(stream),
				FileType.Crx       => QuickTimeMetadataReader.ReadMetadata(stream),
				FileType.Cr2       => TiffMetadataReader.ReadMetadata(stream),
				FileType.Eps       => EpsMetadataReader.ReadMetadata(stream),
				FileType.Gif       => GifMetadataReader.ReadMetadata(stream),
				FileType.Ico       => IcoMetadataReader.ReadMetadata(stream),
				FileType.Jpeg      => JpegMetadataReader.ReadMetadata(stream),
				FileType.Mp3       => Mp3MetadataReader.ReadMetadata(stream),
				FileType.Nef       => TiffMetadataReader.ReadMetadata(stream),
				FileType.Netpbm    => new MetadataExtractor.Directory[] { NetpbmMetadataReader.ReadMetadata(stream) },
				FileType.Orf       => TiffMetadataReader.ReadMetadata(stream),
				FileType.Pcx       => new MetadataExtractor.Directory[] { PcxMetadataReader.ReadMetadata(stream) },
				FileType.Png       => PngMetadataReader.ReadMetadata(stream),
				FileType.Psd       => PsdMetadataReader.ReadMetadata(stream),
				FileType.QuickTime => QuickTimeMetadataReader.ReadMetadata(stream),
				FileType.Mp4       => QuickTimeMetadataReader.ReadMetadata(stream),
				FileType.Raf       => RafMetadataReader.ReadMetadata(stream),
				FileType.Rw2       => TiffMetadataReader.ReadMetadata(stream),
				FileType.Tga       => TgaMetadataReader.ReadMetadata(stream),
				FileType.Tiff      => TiffMetadataReader.ReadMetadata(stream),
				FileType.Wav       => WavMetadataReader.ReadMetadata(stream),
				FileType.WebP      => WebPMetadataReader.ReadMetadata(stream),
				FileType.Heif      => HeifMetadataReader.ReadMetadata(stream),

				FileType.Unknown   => throw new ImageProcessingException("File format could not be determined"),
				_                  => Enumerable.Empty<MetadataExtractor.Directory>()
			});
	
		}
		catch (MetadataExtractor.ImageProcessingException ipx)
        {
			Log.Error($"ReadMetadata - {ipx}");
        }
		catch (Exception exc)
		{
			Log.Error($"ReadMetadata - {exc}");
		}

#pragma warning restore format

        directories.Add(new FileTypeDirectory(fileType));

		return directories;
	}

    internal static void TrainingCSV(bool verbose)
    {
        // Open a CSV file for writing
        string csvFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"DupesMaint2", "Training.csv");

        Log.Information($"TrainingCSV - Writing CSV file: {csvFile}");

        using (StreamWriter sw = new StreamWriter(csvFile))
        {
            // Write the header
            sw.WriteLine("HashValue,CheckSumId1,Filename1,CheckSumId2,Filename2,1or2");

            // Get the list of files to process
            List<VCheckSumBasedOnGroup> vCheckSumBasedOnGroup = photosCtx!.VCheckSumBasedOnGroup.Where(a => a.TheCount == 2 && a.DupBasedOn=="Sha").ToList();
            Log.Information($"TrainingCSV - vCheckSumBasedOnGroup.Count: {vCheckSumBasedOnGroup.Count:N0}");

            // Loop through the duplicate Sha values getting the CheckSum rows
            foreach (var ShaDup in vCheckSumBasedOnGroup)
            {
                // Get the CheckSum rows for the Sha value
                List<CheckSum> checkSums = photosCtx!.CheckSum.Where(a => a.Sha == ShaDup.BasedOnVal).ToList();

                // this should return a list of 2 CheckSum rows
                if (checkSums.Count != 2)
                {
                    Log.Fatal($"TrainingCSV - CheckSums count is {checkSums.Count} should be 2, Sha {ShaDup.BasedOnVal}");
                    return;
                }

                // Get the Id and FileFullName for each CheckSum row
                int checkSumId1 = checkSums[0].Id;
                string filename1 = checkSums[0].FileFullName;
                int checkSumId2 = checkSums[1].Id;
                string filename2 = checkSums[1].FileFullName;

                if (verbose) Log.Information($"TrainingCSV - {ShaDup.BasedOnVal} - {checkSumId1} - {filename1} - {checkSumId2} - {filename2}");
				
                // Write the CSV row
                sw.WriteLine($"{ShaDup.BasedOnVal},{checkSumId1},{filename1},{checkSumId2},{filename2}");
            }
        }

        Log.Information($"TrainingCSV - Finished writing CSV file: {csvFile}");
    }

    /// <summary>
    /// Command 9 Read a CSV file of SHA hashes where duplicate count is 2 and delete the CheckSum based on the ToDelete column.
    /// </summary>
    /// <param name="verbose"></param>
    /// <param name="CSVfile"></param>
    internal static void ShaDelete(bool verbose, FileInfo CSVfile)
    {
        // Open the CSV file for reading
        Log.Information($"ShaDelete - Reading CSV file: {CSVfile.FullName}");

        // read all the lines into a list
        List<string> lines = File.ReadAllLines(CSVfile.FullName).ToList();
		
		foreach (string line in lines)
		{
            string[] fields = line.Split(',');
			if (fields is [string CheckSumId1, _, string CheckSumId2, _, string ToDelete])   // 5 fields in the CSV file
			{
                int checkSumId = (ToDelete == "1") ? int.Parse(CheckSumId1) : int.Parse(CheckSumId2);
 
                // Get the CheckSum row
                CheckSum checkSum = photosCtx!.CheckSum.Find(checkSumId)!;
                if (checkSum is null)
                {
                    Log.Fatal($"ShaDelete - CheckSumId {checkSumId} not found");
                    return;
                }

				// Format the file name depending on which machine is running the code
                string fileFullName = Environment.MachineName == "WILLBOT" ? checkSum.FileFullName.Replace("\\User\\", "\\Pops\\") : checkSum.FileFullName;
				
                // Delete the file
                if (verbose) Log.Information($"ShaDelete - Deleting, CheckSum id: {checkSum.Id}: fileFullName: {fileFullName}");
                File.Delete(fileFullName);

                // Delete the CheckSum row
                photosCtx.CheckSum.Remove(checkSum);
                photosCtx.SaveChanges();
            }
			else  // bad line
			{
                Log.Fatal($"ShaDelete - Invalid line in CSV file: {line}");
                return;
            }
        }
        Log.Information($"ShaDelete - Finished, lines.Count: {lines.Count:N0}");
    }
}
