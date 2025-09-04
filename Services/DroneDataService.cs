// Copyright SkyComb Limited 2025. All rights reserved.
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombDrone.Interfaces;
using SkyCombDrone.PersistModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.Interfaces;

namespace SkyCombDrone.Services
{
    /// <summary>
    /// Implementation of drone data service providing easy access to drone flight data processing
    /// </summary>
    public class DroneDataService : IDroneDataService
    {
        private readonly DroneDataOptions _options;

        /// <summary>
        /// Initializes a new instance of the DroneDataService
        /// </summary>
        /// <param name="options">Configuration options for data processing</param>
        public DroneDataService(DroneDataOptions? options = null)
        {
            _options = options ?? new DroneDataOptions();
        }

        /// <summary>
        /// Creates a new instance of DroneDataService with default options
        /// </summary>
        /// <param name="options">Optional configuration options</param>
        /// <returns>New DroneDataService instance</returns>
        public static DroneDataService Create(DroneDataOptions? options = null)
        {
            return new DroneDataService(options);
        }

        /// <inheritdoc />
        public async Task<IDroneData> LoadVideoDataAsync(string videoFilePath, string groundDataDirectory, string? outputDirectory = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoFilePath))
                throw new ArgumentException("Video file path cannot be null or empty", nameof(videoFilePath));

            if (string.IsNullOrWhiteSpace(groundDataDirectory))
                throw new ArgumentException("Ground data directory cannot be null or empty", nameof(groundDataDirectory));

            if (!File.Exists(videoFilePath))
                throw new FileNotFoundException($"Video file not found: {videoFilePath}");

            if (!Directory.Exists(groundDataDirectory))
                throw new DirectoryNotFoundException($"Ground data directory not found: {groundDataDirectory}");

            return await LoadDroneDataInternalAsync(
                DroneInputType.Video,
                Path.GetDirectoryName(videoFilePath) ?? "",
                videoFilePath,
                groundDataDirectory,
                outputDirectory,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IDroneData> LoadImageDataAsync(string imageDirectory, string groundDataDirectory, string? outputDirectory = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imageDirectory))
                throw new ArgumentException("Image directory cannot be null or empty", nameof(imageDirectory));

            if (string.IsNullOrWhiteSpace(groundDataDirectory))
                throw new ArgumentException("Ground data directory cannot be null or empty", nameof(groundDataDirectory));

            if (!Directory.Exists(imageDirectory))
                throw new DirectoryNotFoundException($"Image directory not found: {imageDirectory}");

            if (!Directory.Exists(groundDataDirectory))
                throw new DirectoryNotFoundException($"Ground data directory not found: {groundDataDirectory}");

            // Check for image files
            var imageFiles = Directory.GetFiles(imageDirectory, "*.jpg", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(imageDirectory, "*.jpeg", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(imageDirectory, "*.png", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(imageDirectory, "*.tiff", SearchOption.AllDirectories))
                .ToArray();

            if (imageFiles.Length == 0)
                throw new InvalidOperationException($"No supported image files found in directory: {imageDirectory}");

            return await LoadDroneDataInternalAsync(
                DroneInputType.Images,
                imageDirectory,
                "",
                groundDataDirectory,
                outputDirectory,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<DroneFlightSummary> GetFlightSummaryAsync(string inputPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                throw new ArgumentException("Input path cannot be null or empty", nameof(inputPath));

            // Create temporary options for summary-only loading
            var summaryOptions = new DroneDataOptions
            {
                FullDataLoad = false,
            };

            DroneInputType inputType = File.Exists(inputPath) ? DroneInputType.Video : DroneInputType.Images;
            string inputDirectory = inputType == DroneInputType.Video ? 
                Path.GetDirectoryName(inputPath) ?? "" : inputPath;
            string inputFileName = inputType == DroneInputType.Video ? inputPath : "";

            try
            {
                using var droneData = await LoadDroneDataInternalAsync(
                    inputType,
                    inputDirectory,
                    inputFileName,
                    "", // No ground directory needed for summary
                    null,
                    cancellationToken,
                    summaryOptions);

                return droneData.FlightSummary;
            }
            catch (DirectoryNotFoundException)
            {
                // Ground directory not found is OK for summary
                var config = CreateDroneConfig();
                var drone = await CreateDroneForSummaryAsync(inputType, inputDirectory, inputFileName, config, cancellationToken);
                
                using (drone)
                {
                    return CreateFlightSummary(drone);
                }
            }
        }

        private async Task<IDroneData> LoadDroneDataInternalAsync(
            DroneInputType inputType,
            string inputDirectory,
            string inputFileName,
            string groundDataDirectory,
            string? outputDirectory,
            CancellationToken cancellationToken,
            DroneDataOptions? overrideOptions = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = overrideOptions ?? _options;

            // Create configuration
            var config = CreateDroneConfig();
            config.UseLegs = options.AutoDetectLegs;

            // Create or open datastore
            var dataStore = CreateDataStore(inputDirectory, inputFileName, outputDirectory ?? inputDirectory, inputType);
            if (dataStore == null)
                throw new InvalidOperationException("Failed to create or open data store");

            // Create drone object
            var drone = await CreateDroneAsync(inputType, dataStore, config, groundDataDirectory, options.FullDataLoad, cancellationToken);

            // Return wrapped drone data
            return new DroneDataWrapper(drone, dataStore, inputType, config);
        }

        private DroneConfigModel CreateDroneConfig()
        {
            return new DroneConfigModel();
        }

        private DroneDataStore? CreateDataStore(string inputDirectory, string inputFileName, string outputDirectory, DroneInputType inputType)
        {
            return DataStoreFactory.OpenOrCreate(
                inputDirectory,
                inputFileName,
                outputDirectory,
                true, // doCreate
                inputType == DroneInputType.Video);
        }

        private async Task<Drone> CreateDroneAsync(
            DroneInputType inputType,
            DroneDataStore dataStore,
            DroneConfigModel config,
            string groundDataDirectory,
            bool fullLoad,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return DroneDataFactory.Create(
                    _ => { }, // Progress callback - could be enhanced
                    _ => DateTime.Now, // Simple date reader
                    dataStore,
                    config,
                    groundDataDirectory,
                    fullLoad);
            }, cancellationToken);
        }

        private async Task<Drone> CreateDroneForSummaryAsync(
            DroneInputType inputType,
            string inputDirectory,
            string inputFileName,
            DroneConfigModel config,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var drone = new Drone(config);
                
                // For summary, we just need basic file information
                if (inputType == DroneInputType.Video && File.Exists(inputFileName))
                {
                    drone.InputVideo = new VideoData(inputFileName, _ => DateTime.Now);
                    drone.CalculateSettings_Video();
                    drone.CalculateSettings_FlightSections_InputIsVideo();
                }
                else if (inputType == DroneInputType.Images)
                {
                    var metadata = drone.CalculateSettings_FlightSections_InputIsImages(inputDirectory);
                    DroneDataFactory.CalculateCameraSpecifics_InputIsImages(drone, metadata);
                }

                return drone;
            }, cancellationToken);
        }

        private DroneFlightSummary CreateFlightSummary(Drone drone)
        {
            var summary = new DroneFlightSummary();

            if (drone.HasInputVideo)
            {
                summary.FlightDateTime = drone.InputVideo.DateEncoded;
                summary.Duration = TimeSpan.FromMilliseconds(drone.InputVideo.DurationMs);
                summary.CameraType = drone.InputVideo.CameraType;
            }

            if (drone.HasFlightSections)
            {
                summary.FlightDateTime = drone.FlightSections.MinDateTime;
                summary.Duration = drone.FlightSections.MaxDateTime - drone.FlightSections.MinDateTime;
                summary.DistanceM = drone.FlightSections.Sections.LastOrDefault().Value?.SumLinealM ?? 0;
                
                // Get center location from bounds
                var minLocation = drone.FlightSections.MinGlobalLocation;
                var maxLocation = drone.FlightSections.MaxGlobalLocation;
                if (minLocation != null && maxLocation != null)
                {
                    summary.CenterLocation = new GlobalLocation(
                        (minLocation.Latitude + maxLocation.Latitude) / 2,
                        (minLocation.Longitude + maxLocation.Longitude) / 2);
                }
            }

            if (drone.HasFlightLegs)
            {
                summary.NumLegsDetected = drone.FlightLegs.Legs.Count;
            }

            summary.HasGimbalData = drone.DroneConfig.GimbalDataAvail != GimbalDataEnum.ManualNo;

            return summary;
        }
    }

    /// <summary>
    /// Internal wrapper that implements IDroneData interface
    /// </summary>
    internal class DroneDataWrapper : IDroneData
    {
        private readonly Drone _drone;
        private readonly DroneDataStore _dataStore;
        private readonly DroneInputType _inputType;
        private readonly DroneConfigModel _config;
        private bool _disposed = false;

        public DroneDataWrapper(Drone drone, DroneDataStore dataStore, DroneInputType inputType, DroneConfigModel config)
        {
            _drone = drone ?? throw new ArgumentNullException(nameof(drone));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _inputType = inputType;
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool HasVideoData => _drone.HasInputVideo;
        public bool HasFlightLogData => _drone.HasFlightSections;
        public bool HasGroundData => _drone.HasGroundData;
        public bool HasFlightLegs => _drone.HasFlightLegs;
        public DroneInputType InputType => _inputType;
        public DroneConfigModel Configuration => _config;

        public DroneDataBounds Bounds
        {
            get
            {
                GlobalLocation swGlobal = new(0, 0);
                GlobalLocation neGlobal = new(0, 0);
                RelativeLocation swRelative = new(0, 0);
                RelativeLocation neRelative = new(0, 0);
                float minAlt = 0, maxAlt = 0;
                DateTime startTime = DateTime.MinValue, endTime = DateTime.MinValue;

                if (_drone.HasFlightSections)
                {
                    swGlobal = _drone.FlightSections.MinGlobalLocation ?? new GlobalLocation(0, 0);
                    neGlobal = _drone.FlightSections.MaxGlobalLocation ?? new GlobalLocation(0, 0);
                    minAlt = _drone.FlightSections.MinAltitudeM;
                    maxAlt = _drone.FlightSections.MaxAltitudeM;
                    startTime = _drone.FlightSections.MinDateTime;
                    endTime = _drone.FlightSections.MaxDateTime;
                }

                if (_drone.HasFlightSections)
                {
                    swRelative = _drone.FlightSections.MinCountryLocation ?? new RelativeLocation(0, 0);
                    neRelative = _drone.FlightSections.MaxCountryLocation ?? new RelativeLocation(0, 0);
                }

                return new DroneDataBounds
                {
                    GlobalBounds = (swGlobal, neGlobal),
                    RelativeBounds = (swRelative, neRelative),
                    AltitudeRange = (minAlt, maxAlt),
                    TimeRange = (startTime, endTime)
                };
            }
        }

        public DroneFlightSummary FlightSummary
        {
            get
            {
                var summary = new DroneFlightSummary();

                if (_drone.HasInputVideo)
                {
                    summary.FlightDateTime = _drone.InputVideo.DateEncoded;
                    summary.Duration = TimeSpan.FromMilliseconds(_drone.InputVideo.DurationMs);
                    summary.CameraType = _drone.InputVideo.CameraType;
                }

                if (_drone.HasFlightSections)
                {
                    summary.FlightDateTime = _drone.FlightSections.MinDateTime;
                    summary.Duration = _drone.FlightSections.MaxDateTime - _drone.FlightSections.MinDateTime;
                    summary.DistanceM = _drone.FlightSections.Sections.LastOrDefault().Value?.SumLinealM ?? 0;
                    
                    // Get center location from bounds
                    var minLocation = _drone.FlightSections.MinGlobalLocation;
                    var maxLocation = _drone.FlightSections.MaxGlobalLocation;
                    if (minLocation != null && maxLocation != null)
                    {
                        summary.CenterLocation = new GlobalLocation(
                            (minLocation.Latitude + maxLocation.Latitude) / 2,
                            (minLocation.Longitude + maxLocation.Longitude) / 2);
                    }
                }

                if (_drone.HasFlightLegs)
                {
                    summary.NumLegsDetected = _drone.FlightLegs.Legs.Count;
                }

                summary.HasGimbalData = _drone.DroneConfig.GimbalDataAvail != GimbalDataEnum.ManualNo;

                return summary;
            }
        }

        public float GetElevationAt(GlobalLocation location, ElevationType elevationType)
        {
            if (!_drone.HasGroundData || location == null)
                return float.NaN;

            // For now, return NaN - proper conversion would require coordinate transformation
            // This would need to be implemented properly based on the existing ground data transformation logic
            return float.NaN;
        }

        public FlightDataPoint? GetFlightDataAt(int timestampMs)
        {
            if (!_drone.HasFlightSteps)
                return null;

            var step = _drone.MsToNearestFlightStep(timestampMs);
            if (step == null)
                return null;

            // Create a GlobalLocation from the relative drone location
            // For now using a simple approximation - proper conversion would need the origin
            var globalLocation = new GlobalLocation(
                step.DroneLocnM?.NorthingM / 111132.0 ?? 0, // Rough conversion meters to degrees
                step.DroneLocnM?.EastingM / 111132.0 ?? 0);

            return new FlightDataPoint
            {
                TimestampMs = step.SumTimeMs,
                Location = globalLocation,
                AltitudeM = step.AltitudeM,
                SpeedMps = step.SpeedMps,
                PitchDeg = step.PitchDeg,
                YawDeg = step.YawDeg,
                GroundElevationM = step.DemM,
                SurfaceElevationM = step.DsmM
            };
        }

        public IReadOnlyCollection<FlightLegSummary> GetFlightLegs()
        {
            if (!_drone.HasFlightLegs)
                return Array.Empty<FlightLegSummary>();

            return _drone.FlightLegs.Legs.Select(leg =>
            {
                return new FlightLegSummary
                {
                    LegId = leg.FlightLegId,
                    LegName = BaseConstants.IdToLetter(leg.FlightLegId),
                    StartTimeMs = leg.MinSumTimeMs,
                    EndTimeMs = leg.MaxSumTimeMs,
                    DistanceM = leg.MaxSumLinealM - leg.MinSumLinealM,
                    AverageAltitudeM = (leg.MinAltitudeM + leg.MaxAltitudeM) / 2,
                    AverageSpeedMps = (leg.MinSpeedMps + leg.MaxSpeedMps) / 2
                };
            }).ToArray();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _drone?.Dispose();
                _dataStore?.Dispose();
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}