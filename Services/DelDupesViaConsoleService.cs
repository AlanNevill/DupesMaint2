using DupesMaint2.Models;

using Serilog;

namespace DupesMaint2.Services;

internal class DelDupesViaConsoleService
{
    private readonly PhotosDbContext _db;

    public DelDupesViaConsoleService(PhotosDbContext db)
    {
        _db = db;
    }

    public void Execute()
    {
        Log.Information( "DelDupesViaConsoleService: querying duplicate SHA values..." );

        List<string> duplicateShas = _db.CheckSum
            .Where( c => c.Sha != null )
            .GroupBy( c => c.Sha )
            .Where( g => g.Count() > 1 )
            .Select( g => g.Key! )
            .ToList();

        Log.Information( "Found {Count} SHA values with more than one matching CheckSum row", duplicateShas.Count );

        int totalDeleted = 0;

        foreach ( var sha in duplicateShas )
        {
            bool skipToNext = false;
            while ( !skipToNext )
            {
                List<CheckSum> rows = _db.CheckSum
                    .Where( c => c.Sha == sha )
                    .OrderBy( c => c.Id )
                    .ToList();

                // Auto-advance once only one copy remains
                if ( rows.Count <= 1 )
                    break;

                Console.WriteLine();
                Console.WriteLine( $"SHA: {sha}" );
                for ( int i = 0; i < rows.Count; i++ )
                {
                    var r = rows[i];
                    Console.WriteLine( $"  {i + 1}: {r.FileFullName}  [{r.CreateDateTime:yyyy-MM-dd}  {r.FileSize:N0} bytes]" );
                }
                Console.Write( $"Enter 1-{rows.Count} to delete, F to skip: " );

                string? input = Console.ReadLine()?.Trim();

                if ( string.IsNullOrEmpty( input ) )
                    continue;

                if ( input.Equals( "F", StringComparison.OrdinalIgnoreCase ) )
                {
                    Log.Information( "Skipped SHA {Sha}", sha );
                    skipToNext = true;
                    continue;
                }

                if ( int.TryParse( input, out int choice ) && choice >= 1 && choice <= rows.Count )
                {
                    CheckSum row = rows[choice - 1];
                    string filePath = row.FileFullName;

                    try
                    {
                        if ( File.Exists( filePath ) )
                        {
                            File.Delete( filePath );
                            Log.Information( "Deleted file: {FilePath}", filePath );
                        }
                        else
                        {
                            Log.Warning( "File not found on disk (removing DB row anyway): {FilePath}", filePath );
                        }

                        _db.CheckSum.Remove( row );
                        _db.SaveChanges();
                        totalDeleted++;

                        Log.Information( "Removed CheckSum row Id={Id}  SHA={Sha}  File={FilePath}", row.Id, sha, filePath );
                        Console.WriteLine( $"  Deleted: {filePath}  (total deleted: {totalDeleted})" );
                    }
                    catch ( Exception ex )
                    {
                        Log.Error( ex, "Failed to delete {FilePath}", filePath );
                        Console.WriteLine( $"  Error: {ex.Message}" );
                    }
                }
                else
                {
                    Console.WriteLine( $"  Invalid input — enter a number between 1 and {rows.Count}, or F to skip." );
                }
            }
        }

        Log.Information( "DelDupesViaConsoleService complete. Total files deleted: {TotalDeleted}", totalDeleted );
        Console.WriteLine( $"\nFinished. Total files deleted: {totalDeleted}" );
    }
}
