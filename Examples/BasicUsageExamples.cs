// Copyright SkyComb Limited 2025. All rights reserved.
using SkyCombDrone.Interfaces;
using SkyCombDrone.Services;
using SkyCombGround.CommonSpace;
using SkyCombGround.Interfaces;

namespace SkyCombDrone.Examples
{
    /// <summary>
    /// Examples showing how to use the SkyCombDroneLibrary for common scenarios
    /// </summary>
    public static class BasicUsageExamples
    {
        /// <summary>
        /// Basic usage example - process a drone video file with flight log
        /// </summary>
        public static async Task BasicVideoExample()
        {
            Console.WriteLine("=== Basic Drone Video Processing Example ===");

            string videoPath = @"C:\DroneVideos\flight_video.mp4";
            string groundDataPath = @"C:\ElevationData";
            string outputPath = @"C:\ProcessedDroneData";

            Console.WriteLine($"Processing video: {videoPath}");

            var droneService = DroneDataService.Create();

            try
            {
                // Load and process drone video with flight log
                using var droneData = await droneService.LoadVideoDataAsync(videoPath, groundDataPath, outputPath);

                Console.WriteLine($"Video data processed successfully!");
                Console.WriteLine($"  Flight duration: {droneData.FlightSummary.Duration:hh\\:mm\\:ss}");
                Console.WriteLine($"  Distance flown: {droneData.FlightSummary.DistanceM:F0}m");
                Console.WriteLine($"  Flight legs detected: {droneData.FlightSummary.NumLegsDetected}");
                Console.WriteLine($"  Camera type: {droneData.FlightSummary.CameraType}");
                Console.WriteLine($"  Center location: {droneData.FlightSummary.CenterLocation}");

                // Get flight data bounds
                var bounds = droneData.Bounds;
                Console.WriteLine($"  Altitude range: {bounds.AltitudeRange.MinAltitudeM:F1}m - {bounds.AltitudeRange.MaxAltitudeM:F1}m");
                Console.WriteLine($"  Flight time: {bounds.TimeRange.StartTime:yyyy-MM-dd HH:mm} - {bounds.TimeRange.EndTime:HH:mm}");

                // Query elevation at flight center
                if (droneData.HasGroundData)
                {
                    var centerLocation = droneData.FlightSummary.CenterLocation;
                    var groundElevation = droneData.GetElevationAt(centerLocation, ElevationType.DEM);
                    var surfaceElevation = droneData.GetElevationAt(centerLocation, ElevationType.DSM);
                    
                    Console.WriteLine($"  Ground elevation at center: {groundElevation:F1}m");
                    Console.WriteLine($"  Surface elevation at center: {surfaceElevation:F1}m");
                }

                // Show flight legs if detected
                if (droneData.HasFlightLegs)
                {
                    Console.WriteLine($"\nFlight Legs:");
                    var legs = droneData.GetFlightLegs();
                    foreach (var leg in legs)
                    {
                        Console.WriteLine($"  Leg {leg.LegName}: {leg.DistanceM:F0}m, " +
                                        $"Alt: {leg.AverageAltitudeM:F1}m, " +
                                        $"Speed: {leg.AverageSpeedMps:F1}m/s");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing video: {ex.Message}");
                Console.WriteLine("Note: Ensure you have:");
                Console.WriteLine("1. A valid drone video file (MP4 format)");
                Console.WriteLine("2. Associated SRT flight log file in the same directory");
                Console.WriteLine("3. Ground elevation data in the specified directory");
            }
        }

        /// <summary>
        /// Example showing how to process a directory of drone images
        /// </summary>
        public static async Task ImageDirectoryExample()
        {
            Console.WriteLine("\n=== Drone Image Directory Processing Example ===");

            string imageDirectory = @"C:\DroneImages\ThermalSurvey";
            string groundDataPath = @"C:\ElevationData";

            Console.WriteLine($"Processing images from: {imageDirectory}");

            var droneService = DroneDataService.Create(new DroneDataOptions
            {
                AutoDetectLegs = true,
                FullDataLoad = true
            });

            try
            {
                // Load and process drone images
                using var droneData = await droneService.LoadImageDataAsync(imageDirectory, groundDataPath);

                Console.WriteLine($"Image data processed successfully!");
                Console.WriteLine($"  Survey duration: {droneData.FlightSummary.Duration:hh\\:mm\\:ss}");
                Console.WriteLine($"  Distance covered: {droneData.FlightSummary.DistanceM:F0}m");
                Console.WriteLine($"  Center location: {droneData.FlightSummary.CenterLocation}");

                // Get flight data at specific timestamps
                Console.WriteLine($"\nSample flight data points:");
                var totalMs = (int)droneData.FlightSummary.Duration.TotalMilliseconds;
                
                for (int i = 0; i <= 4; i++)
                {
                    int timestampMs = i * (totalMs / 4);
                    var flightPoint = droneData.GetFlightDataAt(timestampMs);
                    
                    if (flightPoint != null)
                    {
                        Console.WriteLine($"  T+{TimeSpan.FromMilliseconds(timestampMs):mm\\:ss}: " +
                                        $"{flightPoint.Location}, " +
                                        $"Alt: {flightPoint.AltitudeM:F1}m, " +
                                        $"Speed: {flightPoint.SpeedMps:F1}m/s");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing images: {ex.Message}");
                Console.WriteLine("Note: Image directory should contain JPG, JPEG, PNG, or TIFF files with GPS metadata");
            }
        }

        /// <summary>
        /// Example showing how to get quick flight summary without full processing
        /// </summary>
        public static async Task QuickFlightSummaryExample()
        {
            Console.WriteLine("\n=== Quick Flight Summary Example ===");

            string[] testPaths = {
                @"C:\DroneVideos\flight_001.mp4",
                @"C:\DroneVideos\flight_002.mp4",
                @"C:\DroneImages\Survey001",
                @"C:\DroneImages\Survey002"
            };

            var droneService = DroneDataService.Create();

            Console.WriteLine("Getting quick summaries for multiple flights...");

            foreach (var path in testPaths)
            {
                try
                {
                    var summary = await droneService.GetFlightSummaryAsync(path);
                    
                    Console.WriteLine($"\n{Path.GetFileName(path)}:");
                    Console.WriteLine($"  Date: {summary.FlightDateTime:yyyy-MM-dd HH:mm}");
                    Console.WriteLine($"  Duration: {summary.Duration:hh\\:mm\\:ss}");
                    Console.WriteLine($"  Distance: {summary.DistanceM:F0}m");
                    Console.WriteLine($"  Camera: {summary.CameraType}");
                    Console.WriteLine($"  Location: {summary.CenterLocation}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n{Path.GetFileName(path)}: Error - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Example showing advanced configuration options
        /// </summary>
        public static async Task AdvancedConfigurationExample()
        {
            Console.WriteLine("\n=== Advanced Configuration Example ===");

            var advancedOptions = new DroneDataOptions
            {
                EnableCaching = true,
                MaxConcurrentOperations = 2,
                FullDataLoad = true,
                AutoDetectLegs = true,
                BufferDistanceM = 75 // Larger buffer for ground data
            };

            var droneService = DroneDataService.Create(advancedOptions);

            string videoPath = @"C:\DroneVideos\complex_flight.mp4";
            string groundDataPath = @"C:\ElevationData";

            try
            {
                using var droneData = await droneService.LoadVideoDataAsync(videoPath, groundDataPath);

                Console.WriteLine("Advanced processing completed:");
                Console.WriteLine($"  Input type: {droneData.InputType}");
                Console.WriteLine($"  Has video: {droneData.HasVideoData}");
                Console.WriteLine($"  Has flight log: {droneData.HasFlightLogData}");
                Console.WriteLine($"  Has ground data: {droneData.HasGroundData}");
                Console.WriteLine($"  Has flight legs: {droneData.HasFlightLegs}");

                // Configuration details
                Console.WriteLine($"\nConfiguration used:");
                Console.WriteLine($"  Gimbal data: {droneData.Configuration.UseGimbalData}");
                Console.WriteLine($"  Use legs: {droneData.Configuration.UseLegs}");
                Console.WriteLine($"  Fixed camera down angle: {droneData.Configuration.FixedCameraDownDeg}°");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Advanced processing error: {ex.Message}");
            }
        }

        /// <summary>
        /// Example showing error handling for various scenarios
        /// </summary>
        public static async Task ErrorHandlingExample()
        {
            Console.WriteLine("\n=== Error Handling Example ===");

            var droneService = DroneDataService.Create();

            // Test various error conditions
            var testCases = new[]
            {
                new { Path = @"C:\NonExistent\video.mp4", Type = "Missing video file" },
                new { Path = @"C:\DroneVideos\corrupted.mp4", Type = "Corrupted video file" },
                new { Path = @"C:\EmptyDirectory", Type = "Empty image directory" },
                new { Path = @"", Type = "Empty path" }
            };

            foreach (var testCase in testCases)
            {
                try
                {
                    Console.WriteLine($"\nTesting: {testCase.Type}");
                    var summary = await droneService.GetFlightSummaryAsync(testCase.Path);
                    Console.WriteLine($"  Unexpected success for {testCase.Type}");
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"  Expected ArgumentException: {ex.Message}");
                }
                catch (FileNotFoundException ex)
                {
                    Console.WriteLine($"  Expected FileNotFoundException: {ex.Message}");
                }
                catch (DirectoryNotFoundException ex)
                {
                    Console.WriteLine($"  Expected DirectoryNotFoundException: {ex.Message}");
                }
                catch (UnsupportedVideoFormatException ex)
                {
                    Console.WriteLine($"  Expected UnsupportedVideoFormatException: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Unexpected error: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Comprehensive example showing flight data analysis
        /// </summary>
        public static async Task FlightAnalysisExample()
        {
            Console.WriteLine("\n=== Flight Data Analysis Example ===");

            string videoPath = @"C:\DroneVideos\survey_flight.mp4";
            string groundDataPath = @"C:\ElevationData";

            var droneService = DroneDataService.Create();

            try
            {
                using var droneData = await droneService.LoadVideoDataAsync(videoPath, groundDataPath);

                Console.WriteLine("=== FLIGHT ANALYSIS REPORT ===");
                
                var summary = droneData.FlightSummary;
                Console.WriteLine($"\nFLIGHT OVERVIEW:");
                Console.WriteLine($"  Date & Time: {summary.FlightDateTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  Duration: {summary.Duration:hh\\:mm\\:ss}");
                Console.WriteLine($"  Distance: {summary.DistanceM:F0} meters ({summary.DistanceM/1000:F1} km)");
                Console.WriteLine($"  Average Speed: {summary.DistanceM / summary.Duration.TotalSeconds:F1} m/s");
                Console.WriteLine($"  Camera: {summary.CameraType}");
                Console.WriteLine($"  Gimbal Data: {(summary.HasGimbalData ? "Available" : "Not Available")}");

                var bounds = droneData.Bounds;
                Console.WriteLine($"\nFLIGHT BOUNDS:");
                Console.WriteLine($"  Southwest: {bounds.GlobalBounds.Southwest}");
                Console.WriteLine($"  Northeast: {bounds.GlobalBounds.Northeast}");
                Console.WriteLine($"  Altitude Range: {bounds.AltitudeRange.MinAltitudeM:F1}m - {bounds.AltitudeRange.MaxAltitudeM:F1}m");
                Console.WriteLine($"  Height Above Ground: {bounds.AltitudeRange.MaxAltitudeM - bounds.AltitudeRange.MinAltitudeM:F1}m variation");

                if (droneData.HasFlightLegs)
                {
                    Console.WriteLine($"\nFLIGHT LEGS ({droneData.FlightSummary.NumLegsDetected} detected):");
                    var legs = droneData.GetFlightLegs();
                    foreach (var leg in legs)
                    {
                        var duration = TimeSpan.FromMilliseconds(leg.EndTimeMs - leg.StartTimeMs);
                        Console.WriteLine($"  Leg {leg.LegName}: {duration:mm\\:ss}, " +
                                        $"{leg.DistanceM:F0}m, " +
                                        $"{leg.AverageAltitudeM:F1}m alt, " +
                                        $"{leg.AverageSpeedMps:F1}m/s");
                    }
                }

                // Sample flight data analysis
                Console.WriteLine($"\nSAMPLE FLIGHT DATA POINTS:");
                var totalMs = (int)summary.Duration.TotalMilliseconds;
                for (int i = 0; i <= 10; i++)
                {
                    int timestampMs = i * (totalMs / 10);
                    var point = droneData.GetFlightDataAt(timestampMs);
                    
                    if (point != null)
                    {
                        var timespan = TimeSpan.FromMilliseconds(timestampMs);
                        var heightAboveGround = point.AltitudeM - point.GroundElevationM;
                        
                        Console.WriteLine($"  T+{timespan:mm\\:ss}: " +
                                        $"{point.Location}, " +
                                        $"Alt:{point.AltitudeM:F0}m, " +
                                        $"AGL:{heightAboveGround:F0}m, " +
                                        $"Spd:{point.SpeedMps:F1}m/s, " +
                                        $"Pitch:{point.PitchDeg:F0}°");
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Flight analysis error: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs all the examples
        /// </summary>
        public static async Task RunAllExamples()
        {
            Console.WriteLine("SkyCombDroneLibrary Usage Examples");
            Console.WriteLine("=================================");

            await BasicVideoExample();
            await ImageDirectoryExample();
            await QuickFlightSummaryExample();
            await AdvancedConfigurationExample();
            await ErrorHandlingExample();
            await FlightAnalysisExample();

            Console.WriteLine("\n=== Examples Complete ===");
            Console.WriteLine("Note: To run these examples successfully, you need:");
            Console.WriteLine("1. Drone video files (MP4) with associated SRT flight logs");
            Console.WriteLine("2. Or directories containing drone images with GPS metadata");
            Console.WriteLine("3. Ground elevation data files (from SkyCombGroundLibrary)");
            Console.WriteLine("4. Sufficient disk space for processed data output");
        }
    }

    /// <summary>
    /// Program entry point for running the examples
    /// </summary>
    internal class Program
    {
        static async Task Main(string[] args)
        {
            await BasicUsageExamples.RunAllExamples();
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}