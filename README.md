# SkyComb Drone Library

A .NET library for analysing and processing drone flight data
(longitude, latitude, altitude, speed, yaw, roll, camera down angle, etc).

Flight data is derived from either:
- A text-based flight log generally containing an entry per image
- Meta-data stored on each of the images taken in a flight.

The results are integrated with geographical elevation data,
and persisted in a spreadsheet (i.e. an xls) called a DataStore.

(Note that processing of the image pictures is NOT handled by this library.)

## Features

- 🚁 **Flight path analysis** - Calculate elevation profiles along drone flight paths
- 🎬 **Flight log Processing** - Load and process drone flight log data (SRT)
- 📷 **Image metadata Processing** - Load and process flight metadata from collections of thermal/optical drone images   
- 🛰️ **Flight Analysis** - Detect flight legs, and analyze flight patterns
- 🗺️ **Ground Integration** - Integrate with SkyCombGroundLibrary for elevation data
- 📊 **Flight Metrics** - Calculate speed, altitude, pitch, yaw, and other flight characteristics
- 🔧 **Extensible Design** - Clean interfaces for integration into larger applications

## Quick Start

### Installation

Add the project reference or package:

```bash
dotnet add reference ../SkyCombDroneLibrary/SkyCombDroneLibrary.csproj
```

### EXIF Tool
If you are processing images, you will need the [ExifTool](https://exiftool.org/) to extract metadata from the images.  
EXIF understands and if necessary decrypts the metadata formats used by many drone cameras.
Ensure it is installed and available in your system PATH.


### Basic Usage - Video Metadata Processing

```csharp
using SkyCombDrone.Interfaces;
using SkyCombDrone.Services;

// Create the service
var droneService = DroneDataService.Create();

// Process a drone video with associated flight log
string videoPath = @"C:\DroneVideos\flight_20241201_143022.mp4";
string groundDataPath = @"C:\GroundData";

using var droneData = await droneService.LoadVideoDataAsync(videoPath, groundDataPath);

// Get flight summary
var summary = droneData.FlightSummary;
Console.WriteLine($"Flight Duration: {summary.Duration:hh\\:mm\\:ss}");
Console.WriteLine($"Distance Flown: {summary.DistanceM:F0}m");
Console.WriteLine($"Flight Legs: {summary.NumLegsDetected}");
Console.WriteLine($"Camera Type: {summary.CameraType}");

// Get flight bounds
var bounds = droneData.Bounds;
Console.WriteLine($"Altitude Range: {bounds.AltitudeRange.MinAltitudeM:F1}m - {bounds.AltitudeRange.MaxAltitudeM:F1}m");
```

### Basic Usage - Image Metadata Processing

```csharp
// Process a directory of drone images
string imageDirectory = @"C:\DroneImages\ThermalSurvey";
string groundDataPath = @"C:\GroundData";

using var droneData = await droneService.LoadImageDataAsync(imageDirectory, groundDataPath);

Console.WriteLine($"Images processed: Survey duration {droneData.FlightSummary.Duration:hh\\:mm\\:ss}");

// Get flight data at specific times
var flightPoint = droneData.GetFlightDataAt(30000); // 30 seconds in
if (flightPoint != null)
{
    Console.WriteLine($"At 30s: {flightPoint.Location}, Alt: {flightPoint.AltitudeM:F1}m");
}
```

### Quick Flight Summary

```csharp
// Get quick summary without full processing
var summary = await droneService.GetFlightSummaryAsync(@"C:\DroneVideos\flight.mp4");

Console.WriteLine($"Quick Summary:");
Console.WriteLine($"  Date: {summary.FlightDateTime:yyyy-MM-dd HH:mm}");
Console.WriteLine($"  Duration: {summary.Duration:hh\\:mm\\:ss}");
Console.WriteLine($"  Distance: {summary.DistanceM:F0}m");
Console.WriteLine($"  Center: {summary.CenterLocation}");
```

## Data Requirements

### Video Files
- **Format**: MP4 video files from drone cameras
- **Flight Logs**: Associated SRT files containing GPS and IMU data per video frame
- **Location**: SRT file should be in the same directory as the video
- **Naming**: SRT file should have the same base name as video file

### Image Files  
- **Formats**: JPG, JPEG 
- **Metadata**: Image files must contain GPS etc metadata - accessed using the EXIF tool
- **Organization**: All images in a single directory or subdirectories

### Ground Data (Optional)
- **Integration**: Uses SkyCombGroundLibrary for elevation data
- **Formats**: GeoTIFF elevation files (DEM/DSM)
- **Coverage**: Elevation data should cover the flight area
- **Setup**: Requires running `GroundDataService.RebuildElevationIndexes()` after adding new elevation files

## Advanced Configuration

```csharp
var options = new DroneDataOptions
{
    FullDataLoad = true,
    AutoDetectLegs = true,
    BufferDistanceM = 50  // Ground data buffer around flight path
};

var droneService = DroneDataService.Create(options);
```

## API Reference

### Core Interfaces

- **`IDroneDataService`** - Main service for loading drone data
- **`IDroneData`** - Provides access to processed drone flight data
- **`DroneDataOptions`** - Configuration options for processing
- **`DroneFlightSummary`** - Summary information about a flight

### Key Methods

```csharp
// Load video with flight log
Task<IDroneData> LoadVideoDataAsync(string videoFilePath, string groundDataDirectory, 
                                   string? outputDirectory = null, CancellationToken cancellationToken = default);

// Load directory of images  
Task<IDroneData> LoadImageDataAsync(string imageDirectory, string groundDataDirectory,
                                   string? outputDirectory = null, CancellationToken cancellationToken = default);

// Get quick flight summary
Task<DroneFlightSummary> GetFlightSummaryAsync(string inputPath, CancellationToken cancellationToken = default);

// Query processed data
float GetElevationAt(GlobalLocation location, ElevationType elevationType);
FlightDataPoint? GetFlightDataAt(int timestampMs);
IReadOnlyCollection<FlightLegSummary> GetFlightLegs();
```

## Data Models

### Flight Data Point
```csharp
public class FlightDataPoint
{
    public int TimestampMs { get; set; }
    public GlobalLocation Location { get; set; }
    public float AltitudeM { get; set; }
    public float SpeedMps { get; set; }
    public float PitchDeg { get; set; }
    public float YawDeg { get; set; }
    public float GroundElevationM { get; set; }
    public float SurfaceElevationM { get; set; }
}
```

### Flight Leg Summary
```csharp
public class FlightLegSummary  
{
    public int LegId { get; set; }
    public string LegName { get; set; } // A, B, C, etc.
    public int StartTimeMs { get; set; }
    public int EndTimeMs { get; set; }
    public float DistanceM { get; set; }
    public float AverageAltitudeM { get; set; }
    public float AverageSpeedMps { get; set; }
}
```

## Examples

See the [Examples](Examples/) directory for comprehensive usage examples:

- `BasicUsageExamples.cs` - Getting started with video and image processing
- Flight data analysis
- Error handling  
- Advanced configuration
- Integration with ground data

## Error Handling

The library provides specific exception types:

- **`UnsupportedVideoFormatException`** - Video format not supported
- **`FlightLogProcessingException`** - Issues processing flight log data
- **`InsufficientDroneDataException`** - Not enough data for processing
- **`CameraCalibrationException`** - Camera calibration issues
- **`DroneMetadataException`** - Metadata processing problems

```csharp
try
{
    using var droneData = await droneService.LoadVideoDataAsync(videoPath, groundPath);
    // Process data...
}
catch (UnsupportedVideoFormatException ex)
{
    Console.WriteLine($"Video format not supported: {ex.FilePath}");
}
catch (FlightLogProcessingException ex) 
{
    Console.WriteLine($"Flight log issue: {ex.Message}");
}
```

## Performance Considerations

- **Memory Usage**: Large videos and image sets require substantial memory
- **Processing Time**: Full processing can take several minutes for long flights  
- **Storage**: Processed data is cached in Excel files for faster subsequent access

## File Organization

```
SkyCombDroneLibrary/
├── Interfaces/          # Public interfaces (IDroneDataService, IDroneData)
├── Services/           # Service implementations (DroneDataService)
├── Examples/           # Usage examples and documentation
├── Exceptions/         # Custom exception types
├── src/
│   ├── CommonSpace/    # Constants and shared utilities
│   ├── DroneModel/     # Data models for drone flights
│   ├── DroneLogic/     # Core flight processing logic
│   ├── DrawSpace/      # Visualization and drawing utilities
│   └── PersistModel/   # Data persistence and caching
```

## Related Projects

Part of the SkyComb ecosystem:

- **[SkyComb Analyst](../../SkyCombAnalyst/)** - Complete drone thermal analysis application
- **[SkyComb Flights](../../SkyCombFlights/)** - Batch drone data processing
- **[SkyComb Ground Library](../../SkyCombGroundLibrary/)** - Ground elevation data processing
- **[SkyComb Image Library](../../SkyCombImageLibrary/)** - Computer vision and object detection

## License

MIT License - see [LICENSE](LICENSE) for details.

## Support

- 📖 **Documentation**: See [Examples](Examples/) directory
- 🐛 **Issues**: Report via GitHub Issues  
- 📧 **Contact**: Through GitHub

---

**Note**: This library processes drone video flight log files and images meta-data. The code handles all known cases at time of writing. However, when new drones and cameras are released, the format of data in the log/images sometimes changes. The code may then need updating to handle the changed format
