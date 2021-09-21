using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.ComponentModel;
using System.CommandLine.Builder;

namespace DupesMaint2
{
    internal class Program
	{
		private static int Main(string[] args)
		{
			// Uses System.CommandLine beta library
			// see https://github.com/dotnet/command-line-api/wiki/Your-first-app-with-System.CommandLine
			// PM> Install-Package System.CommandLine -Version 2.0.0-beta1.20104.2

			// Using Arity example:  new Option("--sport", argument: new Argument<string> { Arity = ArgumentArity.ExactlyOne })

			RootCommand rootCommand = new RootCommand("Load File Type")
				{
					new Option<DirectoryInfo>("--folder", "The root folder of the tree to scan which must exist, 'F:/Picasa backup/c/photos'.").ExistingOnly(),
					new Option<string>("--fileType",  "File media type to load, Photo or Video.").FromAmong("Photo","Video"),
					new Option<bool>("--replace", getDefaultValue: () => false,  "Replace default (true) or append (false) to the db tables CheckSum & CheckSumDupes.") {IsRequired = false },
					new Option<bool>("--verbose", getDefaultValue: () => false,  "Verbose logging.") {IsRequired = false }
				};
			// setup the root command handler
			rootCommand.Handler = CommandHandler.Create((DirectoryInfo folder, string fileType, bool replace, bool verbose) => 
				{ HelperLib.LoadFileType(folder, fileType, replace, verbose); });


			// sub command to extract EXIF date/time from all JPG image files in a folder tree
			#region "subcommand2 EXIF"
			Command command2 = new Command("EXIF", "extract EXIF date/time from all JPG image files in a folder tree")
			{
				new Option<DirectoryInfo>("--folder", "The root folder to scan image file, 'C:\\Users\\User\\OneDrive\\Photos").ExistingOnly(),
				new Option<bool>("--replace", getDefaultValue: () => true, "Replace default (true) or append (false) to the db tables CheckSum."){ IsRequired = true }
			};
			command2.Handler = CommandHandler.Create((DirectoryInfo folder, bool replace) => {HelperLib.ProcessEXIF(folder, replace); });
			rootCommand.AddCommand(command2);
			#endregion


			// Command3 - Log the EXIF directories for a photo or video media file
			#region "subcommand3 anEXIF"
			Command command3 = new Command("anEXIF","Log the EXIF data from a single file.")
			{
				new Option<FileInfo>("--image", "An image file, 'C:\\Users\\User\\OneDrive\\Photos\\2013\\02\\2013-02-24 12.34.54-3.jpg'").ExistingOnly()
			};
			command3.Handler = CommandHandler.Create((FileInfo image) => {HelperLib.ProcessAnEXIF(image); });
			rootCommand.AddCommand(command3);
			#endregion


			// Command4 - Delete duplicates
			#region "subcommand4 deleteDups"
			Command command4 = new Command("deleteDups","Delete the duplicate files.")
			{
				new Option<bool>("--delete", getDefaultValue: () => false, "Replace default (true) or append (false) to the db tables CheckSum.") { IsRequired = true }
			};
			command4.Handler = CommandHandler.Create((bool delete) => {HelperLib.DeleteDupes(delete); });
			rootCommand.AddCommand(command4);
			#endregion


			// Command5 - calculate and store up to 3 percuptual hashes in the CheckSum table
			#region "subcommand5 CalculateHashes - Calculate hashes and store in CheckSum"
			Command command5 = new ("CalculateHashes", "Calculate and store hashes in the CheckSum table.")
			{
				new Option<bool>("--ShaHash", getDefaultValue: () => false, "Calculate the ShaHash.") { IsRequired = true },
				new Option<bool>("--AverageHash", getDefaultValue: () => false, "Calculate the AverageHash.") { IsRequired = true },
				new Option<bool>("--DifferenceHash", getDefaultValue: () => false, "Calculate the DifferenceHash.") { IsRequired = true },
				new Option<bool>("--PerceptualHash", getDefaultValue: () => false, "Calculate the PerceptualHash.") { IsRequired = true },
				new Option<bool>("--verbose", getDefaultValue: () => false, "Verbose logging.")
			};
			command5.Handler = CommandHandler.Create((bool ShaHash, bool averageHash, bool differenceHash, bool perceptualHash, bool verbose) => { HelperLib.CalculateHashes(ShaHash, averageHash, differenceHash, perceptualHash, verbose); });
			rootCommand.AddCommand(command5);
			#endregion


			// Command6 - CheckSumDups insert or update based on hash from CheckSum
			#region "subcommand6 CheckSumDups - insert or update CheckSumDups based on hash from CheckSum"
			Command command6 = new ("FindDupsUsingHash", "CheckSumDups - insert or update CheckSumDups based on hash from CheckSum.")
			{
				new Option<string>("--hash", "Hash to use average, difference, perceptual.").FromAmong("ShaHash", "AverageHash", "DifferenceHash", "PerceptualHash"),
				new Option<bool>("--verbose", getDefaultValue: () => false, "Verbose logging.")
			};
			command6.Handler = CommandHandler.Create((string hash, bool verbose) => { HelperLib.FindDupsUsingHash(hash, verbose); });
			rootCommand.AddCommand(command6);
			#endregion

			// Command7 - PerceptualHash_Move2Hdrive
			#region "subcommand7 PerceptualHash_Move2Hdrive
			Command command7 = new("PerceptualHash_Move2Hdrive", "PerceptualHash_Move2Hdrive")
			{
				new Option<bool>("--verbose", getDefaultValue: () =>false, "Verbose logging.")
					.AddSuggestions("true","false")
			};
			command7.Handler = CommandHandler.Create((bool verbose) => { var hL = new HelperLib();  hL.PerceptualHash_Move2Hdrive(verbose); });
			rootCommand.AddCommand(command7);
			#endregion


			// set up common functionality like --help, --version, and dotnet-suggest support
			var commandLine = new CommandLineBuilder(rootCommand)
				.UseDefaults() // automatically configures dotnet-suggest
				.Build();

			// call the method defined in the handler
			return commandLine.InvokeAsync(args).Result;
		}


	}
}
