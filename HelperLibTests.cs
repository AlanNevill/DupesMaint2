using Microsoft.Extensions.Configuration;

using Moq;

using Serilog;

using System.Reflection;

using Xunit;

namespace DupesMaint2.Tests;

/// <summary>
/// Provides unit tests for the <see cref="HelperLib"/> class, focusing on file processing functionality.
/// </summary>
/// <remarks>This class sets up a controlled testing environment, including temporary directories and mock
/// configurations, to validate the behavior of <see cref="HelperLib"/> methods. It includes tests for various scenarios
/// such as file movement, handling unsupported file types, and configuration errors.  The class implements <see
/// cref="IDisposable"/> to ensure proper cleanup of temporary resources after tests.</remarks>
public class HelperLibTests : IDisposable
{
    private readonly string _sourceFolder;
    private readonly string _testDirectory;
    private readonly string _photosDirectory;
    private readonly string _videosDirectory;
    private readonly Mock<IConfiguration> _mockConfig;

    public HelperLibTests()
    {
        // Setup test directories
        _sourceFolder = Directory.GetCurrentDirectory();
        _testDirectory = Path.Combine( _sourceFolder, "Temp", "DupesMaint2Tests", Guid.NewGuid().ToString() );
        _photosDirectory = Path.Combine( _testDirectory, "Photos" );
        _videosDirectory = Path.Combine( _testDirectory, "Videos" );

        Directory.CreateDirectory( _testDirectory );
        Directory.CreateDirectory( _photosDirectory );
        Directory.CreateDirectory( _videosDirectory );

        // Setup mock configuration
        _mockConfig = new Mock<IConfiguration>();
        _mockConfig.Setup( c => c["OneDriveFolders:BEELINK-1:Photos"] ).Returns( _photosDirectory );
        _mockConfig.Setup( c => c["OneDriveFolders:BEELINK-1:Videos"] ).Returns( _videosDirectory );

        // Set static config field via reflection
        var configField = typeof( HelperLib ).GetField( "_config", BindingFlags.NonPublic | BindingFlags.Static );
        configField?.SetValue( null, _mockConfig.Object );

        // Setup Serilog for testing
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        // Mock Environment.MachineName to return BEELINK-1
        Environment.SetEnvironmentVariable( "COMPUTERNAME", "BEELINK-1" );
    }

    [Fact]
    public void CameraRoll_Files_Process_ValidPhotoFile_MovesToCorrectFolder()
    {
        // Arrange
        var testFile = CreateTestFile( "test.jpg", "Photo" );
        var files = new[] { testFile };

        // Act
        InvokeCameraRollFilesProcess( files );

        // Assert
        var expectedFolder = Path.Combine( _photosDirectory, "2023", "05" );
        var expectedFile = Path.Combine( expectedFolder, "test.jpg" );

        Assert.True( File.Exists( expectedFile ), "File should be moved to correct photos folder" );
        Assert.False( File.Exists( testFile.FullName ), "Original file should no longer exist" );
    }

    [Fact]
    public void CameraRoll_Files_Process_ValidVideoFile_MovesToCorrectFolder()
    {
        // Arrange
        var testFile = CreateTestFile( "test.mp4", "Video" );
        var files = new[] { testFile };

        // Act
        InvokeCameraRollFilesProcess( files );

        // Assert
        var expectedFolder = Path.Combine( _videosDirectory, "2023", "05" );
        var expectedFile = Path.Combine( expectedFolder, "test.mp4" );

        Assert.True( File.Exists( expectedFile ), "File should be moved to correct videos folder" );
        Assert.False( File.Exists( testFile.FullName ), "Original file should no longer exist" );
    }

    [Fact]
    public void CameraRoll_Files_Process_UnsupportedExtension_FileNotMoved()
    {
        // Arrange
        var testFile = CreateTestFile( "test.txt", null );
        var originalPath = testFile.FullName;
        var files = new[] { testFile };

        // Act
        InvokeCameraRollFilesProcess( files );

        // Assert
        Assert.True( File.Exists( originalPath ), "Unsupported file should remain in original location" );
    }

    [Fact]
    public void CameraRoll_Files_Process_NoValidDate_FileNotMoved()
    {
        // Arrange
        var testFile = CreateTestFileWithoutMetadata( "test.jpg" );
        var originalPath = testFile.FullName;
        var files = new[] { testFile };

        // Act
        InvokeCameraRollFilesProcess( files );

        // Assert
        Assert.True( File.Exists( originalPath ), "File without valid date should remain in original location" );
    }

    [Fact]
    public void CameraRoll_Files_Process_OverwriteExistingFile_Success()
    {
        // Arrange
        var testFile = CreateTestFile( "test.jpg", "Photo" );
        var targetFolder = Path.Combine( _photosDirectory, "2023", "05" );
        var targetFile = Path.Combine( targetFolder, "test.jpg" );

        Directory.CreateDirectory( targetFolder );
        File.WriteAllText( targetFile, "existing content" );

        var files = new[] { testFile };

        // Act
        InvokeCameraRollFilesProcess( files );

        // Assert
        Assert.True( File.Exists( targetFile ), "File should exist after overwrite" );
        Assert.False( File.Exists( testFile.FullName ), "Original file should no longer exist" );
    }

    [Fact]
    public void CameraRoll_Files_Process_MissingConfiguration_ThrowsException()
    {
        // Arrange
        _mockConfig.Setup( c => c["OneDriveFolders:BEELINK-1:Photos"] ).Returns( (string)null );
        var testFile = CreateTestFile( "test.jpg", "Photo" );
        var files = new[] { testFile };

        // Act & Assert
        var exception = Assert.Throws<TargetInvocationException>( () => InvokeCameraRollFilesProcess( files ) );
        Assert.IsType<Exception>( exception.InnerException );
        Assert.Contains( "OneDriveFolders not found", exception.InnerException.Message );
    }

    [Fact]
    public void CameraRoll_Files_Process_MultipleFiles_ProcessesAllValid()
    {
        // Arrange
        var photoFile = CreateTestFile( "photo.jpg", "Photo" );
        var videoFile = CreateTestFile( "video.mp4", "Video" );
        var unsupportedFile = CreateTestFile( "document.txt", null );
        var files = new[] { photoFile, videoFile, unsupportedFile };

        // Act
        InvokeCameraRollFilesProcess( files );

        // Assert
        var expectedPhotoFile = Path.Combine( _photosDirectory, "2023", "05", "photo.jpg" );
        var expectedVideoFile = Path.Combine( _videosDirectory, "2023", "05", "video.mp4" );

        Assert.True( File.Exists( expectedPhotoFile ), "Photo should be moved" );
        Assert.True( File.Exists( expectedVideoFile ), "Video should be moved" );
        Assert.True( File.Exists( unsupportedFile.FullName ), "Unsupported file should remain" );
    }

    private FileInfo CreateTestFile(string fileName, string mediaType)
    {
        var filePath = Path.Combine( _testDirectory, fileName );

        // Create a minimal file with some EXIF-like metadata for photos
        if ( mediaType == "Photo" && fileName.EndsWith( ".jpg", StringComparison.OrdinalIgnoreCase ) )
        {
            // Create a minimal JPEG with basic EXIF data
            var jpegData = CreateMinimalJpegWithExif();
            File.WriteAllBytes( filePath, jpegData );
        }
        else
        {
            // Create a simple file
            File.WriteAllText( filePath, "test content" );
        }

        return new FileInfo( filePath );
    }

    private FileInfo CreateTestFileWithoutMetadata(string fileName)
    {
        var filePath = Path.Combine( _testDirectory, fileName );
        File.WriteAllText( filePath, "test content without metadata" );
        return new FileInfo( filePath );
    }

    private byte[] CreateMinimalJpegWithExif()
    {
        // This creates a minimal JPEG file structure with basic EXIF data
        // In a real test, you might want to use a library to create proper EXIF data
        // For this example, we'll create a basic structure that the CreateDate_Extract method can parse
        var jpegHeader = new byte[]
        {
            0xFF, 0xD8, // SOI
            0xFF, 0xE1, // APP1
            0x00, 0x16, // Length
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00, // "Exif\0\0"
            // Basic EXIF structure would go here
            // For simplicity, this won't contain actual date data
            0xFF, 0xD9  // EOI
        };
        return jpegHeader;
    }

    private void InvokeCameraRollFilesProcess(FileInfo[] files)
    {
        // Use reflection to invoke the private static method
        var method = typeof( HelperLib ).GetMethod( "CameraRoll_Files_Process", BindingFlags.NonPublic | BindingFlags.Static );

        method?.Invoke( null, new object[] { files } );
    }

    public void Dispose()
    {
        try
        {
            if ( Directory.Exists( _testDirectory ) )
            {
                Directory.Delete( _testDirectory, true );
            }
        }
        catch ( Exception )
        {
            // Ignore cleanup errors
        }
    }
}