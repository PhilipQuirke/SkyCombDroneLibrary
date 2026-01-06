// Copyright SkyComb Limited 2025. All rights reserved.
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.Interfaces;

namespace SkyCombDrone.Interfaces
{
    /// <summary>
    /// Defines the types of drone data input available
    /// </summary>
    public enum DroneInputType
    {
        /// <summary>
        /// Single video file with associated flight log
        /// </summary>
        Video,
        
        /// <summary>
        /// Collection of thermal/optical image files
        /// </summary>
        Images
    }

    /// <summary>
    /// Represents configuration options for drone data processing
    /// </summary>
    public class DroneDataOptions
    {
        /// <summary>
        /// Gets or sets whether to perform full data loading (vs summary only)
        /// </summary>
        public bool FullDataLoad { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to automatically detect and use flight legs
        /// </summary>
        public bool AutoDetectLegs { get; set; } = true;

        /// <summary>
        /// Gets or sets the buffer distance in meters around flight path for ground data
        /// </summary>
        public float BufferDistanceM { get; set; } = 50;
    }

    /// <summary>
    /// Represents drone data boundaries in both global and relative coordinates
    /// </summary>
    public class DroneDataBounds
    {
        /// <summary>
        /// Gets the global geographical bounds of the flight
        /// </summary>
        public (GlobalLocation Southwest, GlobalLocation Northeast) GlobalBounds { get; init; }

        /// <summary>
        /// Gets the relative coordinate bounds in meters
        /// </summary>
        public (RelativeLocation Southwest, RelativeLocation Northeast) RelativeBounds { get; init; }

        /// <summary>
        /// Gets the altitude range of the flight
        /// </summary>
        public (float MinAltitudeM, float MaxAltitudeM) AltitudeRange { get; init; }

        /// <summary>
        /// Gets the time range of the flight
        /// </summary>
        public (DateTime StartTime, DateTime EndTime) TimeRange { get; init; }
    }

    /// <summary>
    /// Provides access to processed drone flight data
    /// </summary>
    public interface IDroneData : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether video data is available
        /// </summary>
        bool HasVideoData { get; }

        /// <summary>
        /// Gets a value indicating whether flight log data is available
        /// </summary>
        bool HasFlightLogData { get; }

        /// <summary>
        /// Gets a value indicating whether ground elevation data is integrated
        /// </summary>
        bool HasGroundData { get; }

        /// <summary>
        /// Gets a value indicating whether flight legs are detected and available
        /// </summary>
        bool HasFlightLegs { get; }

        /// <summary>
        /// Gets the spatial and temporal bounds of the drone data
        /// </summary>
        DroneDataBounds Bounds { get; }

        /// <summary>
        /// Gets the input type (video or images)
        /// </summary>
        DroneInputType InputType { get; }

        /// <summary>
        /// Gets flight statistics and summary information
        /// </summary>
        DroneFlightSummary FlightSummary { get; }

        /// <summary>
        /// Gets the drone configuration used for processing
        /// </summary>
        DroneConfigModel Configuration { get; }

        /// <summary>
        /// Gets elevation at a specific flight location and time
        /// </summary>
        /// <param name="location">Global location</param>
        /// <param name="elevationType">Type of elevation (DEM or DSM)</param>
        /// <returns>Elevation in meters, or NaN if not available</returns>
        float GetElevationAt(GlobalLocation location, ElevationType elevationType);

        /// <summary>
        /// Gets flight data at a specific timestamp
        /// </summary>
        /// <param name="timestampMs">Timestamp in milliseconds from start</param>
        /// <returns>Flight data at the specified time, or null if not available</returns>
        FlightDataPoint? GetFlightDataAt(int timestampMs);

        /// <summary>
        /// Gets all flight legs if available
        /// </summary>
        /// <returns>Collection of flight legs, empty if none detected</returns>
        IReadOnlyCollection<FlightLegSummary> GetFlightLegs();
    }

    /// <summary>
    /// Service for loading and processing drone flight data
    /// </summary>
    public interface IDroneDataService
    {
        /// <summary>
        /// Loads drone data from a video file and associated flight logs
        /// </summary>
        /// <param name="videoFilePath">Path to the drone video file</param>
        /// <param name="groundDataDirectory">Directory containing elevation data files</param>
        /// <param name="outputDirectory">Directory for saving processed data (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Processed drone data</returns>
        /// <exception cref="ArgumentException">Thrown when file paths are invalid</exception>
        /// <exception cref="FileNotFoundException">Thrown when video file is not found</exception>
        /// <exception cref="UnsupportedVideoFormatException">Thrown when video format is not supported</exception>
        Task<IDroneData> LoadVideoDataAsync(string videoFilePath, string groundDataDirectory, string? outputDirectory = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads drone data from a collection of image files
        /// </summary>
        /// <param name="imageDirectory">Directory containing drone image files</param>
        /// <param name="groundDataDirectory">Directory containing elevation data files</param>
        /// <param name="outputDirectory">Directory for saving processed data (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Processed drone data</returns>
        /// <exception cref="ArgumentException">Thrown when directory paths are invalid</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when image directory is not found</exception>
        /// <exception cref="InvalidOperationException">Thrown when no valid images are found</exception>
        Task<IDroneData> LoadImageDataAsync(string imageDirectory, string groundDataDirectory, string? outputDirectory = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets basic flight information without full data loading
        /// </summary>
        /// <param name="inputPath">Path to video file or image directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Basic flight summary information</returns>
        Task<DroneFlightSummary> GetFlightSummaryAsync(string inputPath, string groundDataDirectory, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Summary information about a drone flight
    /// </summary>
    public class DroneFlightSummary
    {
        /// <summary>
        /// Gets or sets the flight date and time
        /// </summary>
        public DateTime FlightDateTime { get; set; }

        /// <summary>
        /// Gets or sets the total flight duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the total distance flown in meters
        /// </summary>
        public float DistanceM { get; set; }

        /// <summary>
        /// Gets or sets the number of flight legs detected
        /// </summary>
        public int NumLegsDetected { get; set; }

        /// <summary>
        /// Gets or sets the geographical center of the flight
        /// </summary>
        public GlobalLocation CenterLocation { get; set; } = new(0, 0);

        /// <summary>
        /// Gets or sets the camera type detected
        /// </summary>
        public string CameraType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether gimbal data is available
        /// </summary>
        public bool HasGimbalData { get; set; }
    }

    /// <summary>
    /// Represents flight data at a specific point in time
    /// </summary>
    public class FlightDataPoint
    {
        /// <summary>
        /// Gets or sets the timestamp in milliseconds from start
        /// </summary>
        public int TimestampMs { get; set; }

        /// <summary>
        /// Gets or sets the drone location
        /// </summary>
        public GlobalLocation Location { get; set; } = new(0, 0);

        /// <summary>
        /// Gets or sets the altitude in meters above sea level
        /// </summary>
        public float AltitudeM { get; set; }

        /// <summary>
        /// Gets or sets the drone speed in meters per second
        /// </summary>
        public float SpeedMps { get; set; }

        /// <summary>
        /// Gets or sets the camera pitch angle in degrees
        /// </summary>
        public float PitchDeg { get; set; }

        /// <summary>
        /// Gets or sets the drone yaw angle in degrees
        /// </summary>
        public float YawDeg { get; set; }

        /// <summary>
        /// Gets or sets the ground elevation at this location
        /// </summary>
        public float GroundElevationM { get; set; }

        /// <summary>
        /// Gets or sets the surface elevation at this location
        /// </summary>
        public float SurfaceElevationM { get; set; }
    }

    /// <summary>
    /// Summary information about a flight leg
    /// </summary>
    public class FlightLegSummary
    {
        /// <summary>
        /// Gets or sets the leg identifier
        /// </summary>
        public int LegId { get; set; }

        /// <summary>
        /// Gets or sets the leg name (A, B, C, etc.)
        /// </summary>
        public string LegName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start time in milliseconds
        /// </summary>
        public int StartTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the end time in milliseconds
        /// </summary>
        public int EndTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the distance flown in this leg
        /// </summary>
        public float DistanceM { get; set; }

        /// <summary>
        /// Gets or sets the average altitude for this leg
        /// </summary>
        public float AverageAltitudeM { get; set; }

        /// <summary>
        /// Gets or sets the average speed for this leg
        /// </summary>
        public float AverageSpeedMps { get; set; }
    }

    /// <summary>
    /// Exception types specific to drone data processing
    /// </summary>
    public class UnsupportedVideoFormatException : Exception
    {
        public UnsupportedVideoFormatException(string message) : base(message) { }
        public UnsupportedVideoFormatException(string message, Exception innerException) : base(message, innerException) { }
    }
}