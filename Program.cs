using DupesMaint2.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace DupesMaint2;

internal class Program
{
    public static IConfiguration? _config;
    public static string? _cnStr;

    public static string? OneDrivePhotos { get; set; }
    public static string? OneDriveVideos { get; set; }

    private static int Main(string[] args)
	{
		BuildConfig();

        // dependency injection
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<PhotosDbContext>(options =>
                {
                    options.UseSqlServer(_cnStr);
                });
                //services.AddTransient<IGreetingService, GreetingService>();
                services.AddTransient<HelperLib, HelperLib>();
            })
            .Build();


        // set up the HelperLib service
        var svcHelperLib = ActivatorUtilities.CreateInstance<HelperLib>(host.Services);


		// Uses System.CommandLine beta library
		// see https://github.com/dotnet/command-line-api/wiki/Your-first-app-with-System.CommandLine

		// define command options
		var folder = new Option<DirectoryInfo>("--folder", "The root folder of the tree to scan which must exist, 'F:/Picasa backup/c/photos'.").ExistingOnly();
		var mediaType = new Option<string>("--mediaType", "File media type to load, Photo or Video.").FromAmong("Photo", "Video");
		var replace = new Option<bool>("--replace", getDefaultValue: () => false, "Replace default (true) or append (false) to the db tables CheckSum & CheckSumDupes.") { IsRequired = false };
		var verbose = new Option<bool>("--verbose", getDefaultValue: () => false, "Verbose logging.") { IsRequired = false };
		var image = new Option<FileInfo>("--image", "An image file, 'C:\\Users\\User\\OneDrive\\Photos\\2013\\02\\2013-02-24 12.34.54-3.jpg'").ExistingOnly();
        var ShaHash = new Option<bool>("--ShaHash", getDefaultValue: () => false, "Calculate the ShaHash.") { IsRequired = true };
        var AverageHash = new Option<bool>("--AverageHash", getDefaultValue: () => false, "Calculate the AverageHash.") { IsRequired = true };
        var DifferenceHash = new Option<bool>("--DifferenceHash", getDefaultValue: () => false, "Calculate the DifferenceHash.") { IsRequired = true };
        var PerceptualHash = new Option<bool>("--PerceptualHash", getDefaultValue: () => false, "Calculate the PerceptualHash.") { IsRequired = true };
        var Hash = new Option<string>("--hash", "Hash to use: SHA, average, difference, perceptual.").FromAmong("Sha", "Average", "Difference", "Perceptual");
        var CSVfile = new Option<FileInfo>("--CSVfile", "A CSV file holding details of the CheckSum Ids to delete").ExistingOnly();
        
        #region Root command	
        RootCommand rootCommand = new("Load File Type")
		{
            folder, mediaType,  replace, verbose
        };
		
		// setup the root command handler
		rootCommand.SetHandler((folder, mediaType, replace, verbose) => { HelperLib.LoadFileType(folder, mediaType, replace, verbose); }, folder, mediaType,  replace, verbose);
        #endregion

        // sub command to extract EXIF date/time from media type Photo files in a folder tree
        #region "subcommand2 EXIF"
        Command command2 = new("EXIF", "extract EXIF date/time from media type Photo files in a folder tree")
		{
			folder, replace        
		};
		command2.SetHandler((folder, replace) => {HelperLib.ProcessEXIF(folder, replace);}, folder, replace);
		rootCommand.AddCommand(command2);
		#endregion

		// Command3 - Log the internal EXIF directories for a photo or video media file
		#region "subcommand3 anEXIF"
		Command command3 = new("anEXIF","Log the EXIF data from a single file.")
		{
			image
		};
		command3.SetHandler((image) => {HelperLib.ProcessAnEXIF(image); }, image);
		rootCommand.AddCommand(command3);
		#endregion


		// Command4 - CameraRoll_Move
		#region "subcommand4 CameraRoll_Move"
		Command command4 = new("CameraRoll_Move", "Move media file types from CameraRoll folder to date folders.")
		{
            mediaType, verbose
        };
		command4.SetHandler((mediaType, verbose) => {HelperLib.CameraRoll_Move(mediaType, verbose);}, mediaType, verbose);
		rootCommand.AddCommand(command4);
		#endregion


		// Command5 - calculate and store up to 3 hashes and the SHA256 hash in the CheckSum table
		#region "subcommand5 CalculateHashes - Calculate hashes and store in CheckSum"
		Command command5 = new ("CalculateHashes", "Calculate and store hashes in the CheckSum table.")
		{
            ShaHash, AverageHash, DifferenceHash, PerceptualHash, verbose
        };
		command5.SetHandler((ShaHash, AverageHash, DifferenceHash, PerceptualHash, verbose) => 
            { HelperLib.CalculateHashes(ShaHash, AverageHash, DifferenceHash, PerceptualHash, verbose);},
            ShaHash, AverageHash, DifferenceHash, PerceptualHash, verbose);
		rootCommand.AddCommand(command5);
		#endregion

        
		// Command6 - CheckSumDups insert or update based on hash from CheckSum
		#region "subcommand6 CheckSumDupsBasedOn - insert or update CheckSumDupsBasedOn based on 1 of the hashes from CheckSum"
		Command command6 = new ("FindDupsUsingHash", "CheckSumDupsBasedOn - insert or update CheckSumDupsBasedOn based on hash from CheckSum.")
		{
			Hash, verbose
		};
		command6.SetHandler((Hash, verbose) => {svcHelperLib.FindDupsUsingHash(Hash, verbose);}, Hash, verbose);
		rootCommand.AddCommand(command6);
		#endregion

		// Command7 - PerceptualHash_Move2Hdrive
		#region "subcommand7 PerceptualHash_Move2Hdrive
		Command command7 = new("PerceptualHash_Move2Hdrive", "PerceptualHash_Move2Hdrive")
		{
			verbose
		};
		command7.SetHandler((verbose) => { HelperLib.PerceptualHash_Move2Hdrive(verbose);}, verbose);
		rootCommand.AddCommand(command7);
        #endregion

        // Command8 - Create a CSV file of SHA hashes where duplicate count is 2. This is to create a training CSV file for the ML.NET model.
        #region "subcommand8 Training CSV
        Command command8 = new("TrainingCSV", "Create a CSV file of SHA hashes where duplicate count is 2. This is to create a training CSV file for the ML.NET model")
        {
            verbose                
        };
        command8.SetHandler((verbose) => { HelperLib.TrainingCSV(verbose); }, verbose);
        rootCommand.AddCommand(command8);
        #endregion

        // Command9 - Read a CSV file of SHA hashes where duplicate count is 2 and delete the CheckSum based on the ToDelete column.
        #region "subcommand9 Read a CSV file of SHA hashes where duplicate count is 2 and delete the CheckSum based on the ToDelete column.
        Command command9 = new("ShaDelete", "Read a CSV file of SHA hashes where duplicate count is 2 and delete the CheckSum based on the ToDelete column.")
        {
            verbose, CSVfile
        };
        command9.SetHandler((verbose, CSVfile) => { HelperLib.ShaDelete(verbose, CSVfile); }, verbose, CSVfile);
        rootCommand.AddCommand(command9);
        #endregion

        // Command10 - Create a CSV file of perceptual hashes where duplicate count is 2.
        #region "subcommand10 PerceptualHashCSV
        Command command10 = new("PerceptualHashCSV", "Create a CSV file of perceptual hashes where duplicate count is 2.")
        {
            verbose
        };
        command10.SetHandler((verbose) => { HelperLib.PerceptualHashCSV(verbose); }, verbose);
        rootCommand.AddCommand(command10);
        #endregion


        // call the method defined in the handler
        try
        {
			return rootCommand.InvokeAsync(args).Result;
		}
		catch (Exception exc)
		{
			Log.Fatal(exc, "Unhandled exception");
		}
		finally
		{
            Log.Information( "Finished");
            Log.CloseAndFlush();
        }
		return 0;
    }	// end of method Main

    public static void BuildConfig()
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            //.AddUserSecrets(Assembly.GetExecutingAssembly(), false)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(_config)
            .CreateLogger();


        if (Environment.MachineName.ToUpper() == "WILLBOT")
        {
            _cnStr = _config.GetConnectionString("WILLBOT_Photos")!;
            OneDrivePhotos = _config["Willbot_OneDrivePhotos"]!;
            OneDriveVideos = _config["Willbot_OneDriveVideos"]!;
        }
        else
        {
            _cnStr = _config.GetConnectionString("SNOWBALL_Photos")!;
            OneDrivePhotos = _config["Snowball_OneDrivePhotos"]!;
            OneDriveVideos = _config["Snowball_OneDriveVideos"]!;
        }

        // Ensure the log & console prominently shows the database and server being used
        var CnStr = new SqlConnectionStringBuilder(_cnStr);

        Log.Information($"""
        {new String('-', 106)}
                {typeof(Program).Assembly.FullName}     
                Assembly version:       {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}
                Server:                 {CnStr.DataSource.ToUpper()}
                Database:               {CnStr.InitialCatalog.ToUpper()}
                OneDrivePhotos:         {OneDrivePhotos}
                OneDriveVideos:         {OneDriveVideos}
        {new String('-', 130)}
        """);
    }
}
