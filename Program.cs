using Dapper;
using ExifLibrary;
using MetadataExtractor;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Security.Cryptography;


namespace DupesMaint2
{
	internal class Program
	{
		private const string _connectionString = @"data source=SNOWBALL\MSSQLSERVER01;initial catalog=pops;integrated security=True;MultipleActiveResultSets=True";
		private static readonly StreamWriter _writer = File.AppendText(@"./logfile.txt");

		// List of Checksum rows where filenames are the same
		private static readonly List<CheckSum> Checksums = new List<CheckSum>();

		private static int Main(string[] args)
		{

			// Uses System.CommandLine beta library
			// see https://github.com/dotnet/command-line-api/wiki/Your-first-app-with-System.CommandLine
			// PM> Install-Package System.CommandLine -Version 2.0.0-beta1.20104.2

			RootCommand rootCommand = new RootCommand("DupesMaintConsole")
				{
					new Option("--folder", "The root folder of the tree to scan which must exist, 'F:/Picasa backup/c/photos'.")
						{
							Argument = new Argument<DirectoryInfo>().ExistingOnly(),
							IsRequired = true
						},

					new Option("--replace", "Replace default (true) or append (false) to the db tables CheckSum & CheckSumDupes.")
						{
							Argument = new Argument<bool>(getDefaultValue: () => true),
							IsRequired = false
						}

				};

			// setup the root command handler
			rootCommand.Handler = CommandHandler.Create((DirectoryInfo folder, bool replace) => { Process(folder, replace); });


			// sub command to extract EXIF date/time from all JPG image files in a folder tree
			#region "subcommand EXIF"
			Command command2 = new Command("EXIF")
			{
				new Option("--folder", "The root folder to scan image file, 'C:\\Users\\User\\OneDrive\\Photos")
					{
						Argument = new Argument<DirectoryInfo>().ExistingOnly(),
						IsRequired = true,
					},

				new Option("--replace", "Replace default (true) or append (false) to the db tables CheckSum.")
					{
						Argument = new Argument<bool>(getDefaultValue: () => true),
						IsRequired = true,
					}
			};

			command2.Handler = CommandHandler.Create((DirectoryInfo folder, bool replace) => { ProcessEXIF(folder, replace); });
			rootCommand.AddCommand(command2);
			#endregion

			#region "subcommand anEXIF"
			Command command3 = new Command("anEXIF")
			{
				new Option("--image", "An image file, 'C:\\Users\\User\\OneDrive\\Photos\\2013\\02\\2013-02-24 12.34.54-3.jpg'")
					{
						Argument = new Argument<FileInfo>().ExistingOnly(),
						IsRequired = true,
					}
			};

			command3.Handler = CommandHandler.Create((FileInfo image) => { ProcessAnEXIF(image); });
			rootCommand.AddCommand(command3);
			#endregion

			#region "subcommand deleteDups"
			Command command4 = new Command("deleteDups")
			{
				new Option("--delete", "Replace default (true) or append (false) to the db tables CheckSum.")
					{
						Argument = new Argument<bool>(getDefaultValue: () => false),
						IsRequired = true,
					}
			};

			command4.Handler = CommandHandler.Create((bool delete) => { DeleteDupes(delete); });
			rootCommand.AddCommand(command4);
			#endregion

			// call the method defined in the handler
			return rootCommand.InvokeAsync(args).Result;
		}

		// Process the duplicate rows in the CheckSum table and report files to delete or report AND delete.
        private static void DeleteDupes(bool delete)
        {
			System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

			// 1. Make sure there are some duplicate rows in the CheckSum table
			const string sql = @"select SHA,Count(*) as DupeCount from Checksum group by SHA having Count(*)>1";

			Log($"INFO\t- Starting DeleteDupes: --delete: {delete} sql: {sql}");

			using var cnn = new SqlConnection(_connectionString);
			var _CheckSumHasDuplicates = cnn.Query<CheckSumHasDuplicates>(sql).AsList();

            if (_CheckSumHasDuplicates.Count==0)
            {
				Log("ERROR\t- Abort. No duplicates found in CheckSum");
				Log($"{new String('-', 150)}\r\n");
				return;
			}
			Log($"INFO\t- {_CheckSumHasDuplicates.Count} duplicates found in the CheckSum table.");

			// Main processing foreach loop local function
			ProcessDeleteDupes();

			_stopwatch.Stop();
			Log($"INFO\t- Total execution time: {_stopwatch.ElapsedMilliseconds / 60000} mins.");
			Log($"{new String('-', 150)}\r\n");

			// local function
			void ProcessDeleteDupes()
			{
				foreach (var aCheckSumHasDuplicates in _CheckSumHasDuplicates)
				{
					using var cnn = new SqlConnection(_connectionString);
					var _CheckSum = cnn.Query<CheckSum>($"select * from CheckSum where SHA='{aCheckSumHasDuplicates.SHA}' order by LEN(TheFileName) desc").AsList();
					if (_CheckSum.Count < 2)
					{
						Log($"ERROR\t- Only found {_CheckSum.Count} CheckSum rows with SHA: {aCheckSumHasDuplicates.SHA}, should be >= 2.");
						continue;
					}

					Log($"INFO\t- {_CheckSum.Count} CheckSum rows for duplicate SHA: {aCheckSumHasDuplicates.SHA}");

					// get the CheckSum row with the longest name NB could be the same
					var aCheckSum = _CheckSum[0];
					if (delete)
					{
						FileInfo deleteFileInfo = new FileInfo(Path.Combine(aCheckSum.Folder, aCheckSum.TheFileName));
						if (deleteFileInfo.Exists)
						{
							deleteFileInfo.Delete();
							using IDbConnection db = new SqlConnection(_connectionString);
							db.Execute($"delete from dbo.CheckSum where Id={aCheckSum.Id}");
							Log($"WARN\t- Deleted the SHA with the longest duplicate name was id: {aCheckSum.Id}\tThe name was: {aCheckSum.TheFileName}\tThe folder was: {aCheckSum.Folder}");
						}
						else
						{
							Log($"ERROR\t- The duplicate to delete {aCheckSum.TheFileName}\tdoes not now exits in folder: {aCheckSum.Folder}");
						}
					}
					else
					{
						Log($"INFO\t- No delete. The SHA with the longest duplicate name is id: {aCheckSum.Id}\tThe name is: {aCheckSum.TheFileName}\tThe folder is: {aCheckSum.Folder}");
					}
				}
			}
		}




        // subCommand3
        private static void ProcessAnEXIF(FileInfo image)
		{
			IEnumerable<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(image.FullName);

			Log($"{ DateTime.Now}, ProcessAnEXIF, INFO - image: {image.FullName}");

			foreach (MetadataExtractor.Directory _directory in directories)
			{
				foreach (Tag tag in _directory.Tags)
				{
					Log($"[{_directory.Name}] - [{tag.Name}] = [{tag.Description}]");
				}
			}

		}


		private static void ProcessEXIF(DirectoryInfo folder, bool replace)
		{
			int _count = 0;
			Log($"\tINFO- ProcessEXIF: target folder is {folder.FullName}\tTruncate CheckSum is: {replace}.");

			if (replace)
			{
                using IDbConnection db = new SqlConnection(_connectionString);
                db.Execute("truncate table dbo.CheckSum");
            }

			// get an array of FileInfo objects from the folder tree
			FileInfo[] _files = folder.GetFiles("*.JPG", SearchOption.AllDirectories);

			foreach (FileInfo fi in _files)
			{
				// get the EXIF date/time 
				(DateTime _CreateDateTime, string _sCreateDateTime) = ImageEXIF(fi);


				// instantiate a new CheckSum object for the file
				CheckSum checkSum = new CheckSum
				{
					SHA = "",
					Folder = fi.DirectoryName,
					TheFileName = fi.Name,
					FileExt = fi.Extension,
					FileSize = (int)fi.Length,
					FileCreateDt = fi.CreationTime,
					CreateDateTime = _CreateDateTime,
					SCreateDateTime = _sCreateDateTime
				};

				// insert into DB table
				CheckSum_ins2(checkSum);


				_count++;

				if (_count % 1000 == 0)
				{
					Log($"INFO\t- {_count:N0}. Completed: {((_count * 100) / _files.Length)}%. Processing folder: {fi.DirectoryName}");
				}
			}

		}


		private static (DateTime CreateDateTime, string sCreateDateTime) ImageEXIF(FileInfo fileInfo)
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
				Log($"ERROR\t- File: {fileInfo.FullName}, _sCreateDateTime: {_sCreateDateTime}, _CreateDateTime: {_CreateDateTime}");

				return (CreateDateTime: _CreateDateTime, sCreateDateTime: _sCreateDateTime);
			}
			catch (Exception exc)
			{
				_sCreateDateTime = "ERROR -see log";
				Log($"ERROR\t- File: {fileInfo.FullName}\r\n{exc}\r\n");

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


		public static void LogImageEXIF(IEnumerable<MetadataExtractor.Directory> directory, FileInfo fileInfo)
		{
			Console.WriteLine($"\r\nfileInfo: {fileInfo.FullName}");

			foreach (MetadataExtractor.Directory _directory in directory)
			{
				if (_directory.Name.Equals("Exif SubIFD"))
				{
					foreach (Tag tag in _directory.Tags)
					{
						Console.WriteLine($"[{_directory.Name}] - [{tag.Name}] = [{tag.Description}]");
					}
				}
			}

		}


		public static void Process(DirectoryInfo folder, bool replace)
		{
			Log($"INFO\t- Starting find duplicates in target folder is {folder.FullName}\tTruncate table CheckSum is: {replace}.");
			System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

			if (replace)
			{
                using IDbConnection db = new SqlConnection(_connectionString);
                db.Execute("truncate table dbo.CheckSum");
            }

			// main processing
			int fileCount = ProcessFiles(folder);

			_stopwatch.Stop();
			Log($"INFO\t- Total execution time: {_stopwatch.ElapsedMilliseconds / 60000} mins. # of files processed: {fileCount}.");
			Log($"{new String('-', 150)}\r\n");

		}


		private static int ProcessFiles(DirectoryInfo folder)
		{
			int _count = 0;

			System.Diagnostics.Stopwatch process100Watch = System.Diagnostics.Stopwatch.StartNew();

			FileInfo[] _files = folder.GetFiles("*.JPG", SearchOption.AllDirectories);
			Log($"INFO\t- Found {_files.Length:N0} to process.");

			// Process all the JPG files in the source directory tree
			foreach (FileInfo fi in _files)
			{
				// calculate the SHA string for the file and return with the time taken in ms in a tuple
				(string SHA, int timerMs) = CalcSHA(fi);

				// instantiate a new CheckSum object for the file
				CheckSum checkSum = new CheckSum
				{
					SHA = SHA,
					Folder = fi.DirectoryName,
					TheFileName = fi.Name,
					FileExt = fi.Extension.ToUpper(),
					FileSize = (int)fi.Length,
					FileCreateDt = fi.CreationTime,
					TimerMs = timerMs
				};

				// see if the file name already exists in the Checksums list
				CheckSum alreadyExists = Checksums.Find(x => x.SHA == SHA);

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
					Log($"INFO\t- {_count}. Last 100 in {process100Watch.ElapsedMilliseconds / 1000} secs. " +
						$"Completed: {(_count * 100) / _files.Length}%. " +
						$"Processing folder: {fi.DirectoryName}");
					process100Watch.Reset();
					process100Watch.Start();
				}
			}
			return _count;
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
			p.Add("@SHA", checkSum.SHA);
			p.Add("@Folder", checkSum.Folder);
			p.Add("@TheFileName", checkSum.TheFileName);
			p.Add("@FileExt", checkSum.FileExt);
			p.Add("@FileSize", checkSum.FileSize);
			p.Add("@FileCreateDt", checkSum.FileCreateDt);
			p.Add("@TimerMs", checkSum.TimerMs);
			p.Add("@Notes", "");

			// call the stored procedure
			using (IDbConnection db = new SqlConnection(_connectionString))
			{
				db.Execute("dbo.spCheckSum_ins", p, commandType: CommandType.StoredProcedure);
			}

		}

		private static void CheckSum_ins2(CheckSum checkSum)
		{
			// create the SqlParameters for the stored procedure
			DynamicParameters p = new DynamicParameters();
			p.Add("@SHA", checkSum.SHA);
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
			using (IDbConnection db = new SqlConnection(_connectionString))
			{
				db.Execute("dbo.spCheckSum_ins2", p, commandType: CommandType.StoredProcedure);
			}

		}
		private static void Log(string mess)
		{
			Console.WriteLine(mess);
			_writer.WriteLine($"{DateTime.Now} {mess}");
			_writer.Flush();
		}

	}
}
