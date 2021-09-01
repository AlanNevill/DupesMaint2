using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;

namespace DupesMaint2
{
    internal class Program
	{
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
			rootCommand.Handler = CommandHandler.Create((DirectoryInfo folder, bool replace) => { HelperLib.Process(folder, replace); });

			// sub command to extract EXIF date/time from all JPG image files in a folder tree
			#region "subcommand EXIF"
			Command command2 = new Command("EXIF", "extract EXIF date/time from all JPG image files in a folder tree")
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
			command2.Handler = CommandHandler.Create((DirectoryInfo folder, bool replace) => {HelperLib.ProcessEXIF(folder, replace); });
			rootCommand.AddCommand(command2);
			#endregion

			// Command3 - Log the EXIF directories for a photo or video media file
			#region "subcommand anEXIF"
			Command command3 = new Command("anEXIF","Log the EXIF data from a single file.")
			{
				new Option("--image", "An image file, 'C:\\Users\\User\\OneDrive\\Photos\\2013\\02\\2013-02-24 12.34.54-3.jpg'")
					{
						Argument = new Argument<FileInfo>().ExistingOnly(),
						IsRequired = true,
					}
			};
			command3.Handler = CommandHandler.Create((FileInfo image) => {HelperLib.ProcessAnEXIF(image); });
			rootCommand.AddCommand(command3);
			#endregion

			// Command4 - Delete duplicates
			#region "subcommand deleteDups"
			Command command4 = new Command("deleteDups","Delete the duplicate files.")
			{
				new Option("--delete", "Replace default (true) or append (false) to the db tables CheckSum.")
					{
						Argument = new Argument<bool>(getDefaultValue: () => false),
						IsRequired = true,
					}
			};
			command4.Handler = CommandHandler.Create((bool delete) => {HelperLib.DeleteDupes(delete); });
			rootCommand.AddCommand(command4);
			#endregion

			// call the method defined in the handler
			return rootCommand.InvokeAsync(args).Result;
		}
	}
}
