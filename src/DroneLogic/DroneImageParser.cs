// Copyright SkyComb Limited 2025. All rights reserved. 
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;


namespace SkyCombDrone.DroneLogic
{
    public class DroneImageMetadata
    {
        public string FullName { get; set; }
        public string FileName { get; set; }
        public string CameraModelName { get; set; }
        public string Software { get; set; }
        public DateTime CreateDate { get; set; }    // Does not store milliseconds so accuracy is +/-0.5s
        public double? FNumber { get; set; }
        public double? FocalLength { get; set; }
        public int? ImageWidth { get; set; }
        public int? ImageHeight { get; set; }
        public double? DigitalZoomRatio { get; set; }
        public double? FocalLength35mm { get; set; }
        public string SerialNumber { get; set; }
        public string LensInfo { get; set; }
        public double? AbsoluteAltitude { get; set; }
        public double? RelativeAltitude { get; set; }
        public double? GpsAltitude { get; set; }
        public double? GimbalRollDegree { get; set; }
        public double? GimbalYawDegree { get; set; }
        public double? GimbalPitchDegree { get; set; }
        public double? FlightRollDegree { get; set; }
        public double? FlightYawDegree { get; set; }
        public double? FlightPitchDegree { get; set; }
        public string DroneModel { get; set; }
        public string DroneSerialNumber { get; set; }
        public string LRFStatus { get; set; }
        public double? LRFDistance { get; set; } // Target
        public double? LRFLon { get; set; } // Target
        public double? LRFLat { get; set; } // Target
        public double? LRFAlt { get; set; } // Target
        public double? LRFAbsAlt { get; set; } // Target
        public double? ScaleFactor35mm { get; set; }
        public double? CircleOfConfusionMM { get; set; }
        public double? DepthOfFieldM { get; set; }
        public double? FieldOfViewDegree { get; set; }
        public double? HyperfocalDistanceM { get; set; }
    }


    public class DroneImageMetadataReader
    {
        // ExifTool is a command line exe that reads the encrypted properties from a DJI drone image (jpg).
        // Removed (-k) from the exe name to run it in batch mode (not interactively)
        // User must add the "C:\SkyComb\exiftool" or similar to the Windows path.
        public const string ExifToolPath = "exiftool.exe";


        public static DroneImageMetadata ReadMetadataFromFilePath(string filePath)
        {
            string output = RunExifTool(filePath);
            return ParseExifOutput(output);
        }


        public static List<DroneImageMetadata> ReadMetadataFromFolder(string folderPath, bool all = true)
        {
            var files = Directory.GetFiles(folderPath, "*_T*.JPG");
            var metadataList = new List<DroneImageMetadata>();

            foreach (var filePath in files)
            {
                var metadata = ReadMetadataFromFilePath(filePath);
                if (metadata != null)
                {
                    metadata.FullName = filePath;
                    metadata.FileName = Path.GetFileName(filePath);
                    metadataList.Add(metadata);

                    if (!all)
                        // Only read the first file
                        break;
                }
            }

            return metadataList
                .OrderBy(m => m.CreateDate)
                .ThenBy(m => m.FileName)
                .ToList();
        }


        private static string RunExifTool(string filePath)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = ExifToolPath,
                    Arguments = $"\"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Check if process exited successfully
                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"ExifTool exited with code {process.ExitCode}");
                    }

                    return output;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to run ExifTool on {filePath}: {ex.Message}", ex);
                }
            }
        }

        static double ParseDmsCoordinate(string input)
        {
            // Match format like: 175 deg 36' 20.81" E
            var regex = new Regex(@"(\d+)\s*deg\s*(\d+)'\s*([\d.]+)""\s*([NSEW])", RegexOptions.IgnoreCase);
            var match = regex.Match(input.Trim());

            if (!match.Success)
            {
                throw new FormatException("Input string is not in the correct DMS format.");
            }

            double degrees = double.Parse(match.Groups[1].Value);
            double minutes = double.Parse(match.Groups[2].Value);
            double seconds = double.Parse(match.Groups[3].Value);
            char direction = char.ToUpper(match.Groups[4].Value[0]);

            double decimalDegrees = degrees + (minutes / 60.0) + (seconds / 3600.0);

            // Apply negative sign for South or West
            if (direction == 'S' || direction == 'W')
            {
                decimalDegrees *= -1;
            }

            return decimalDegrees;
        }

        private static DroneImageMetadata ParseExifOutput(string output)
        {
            var data = new DroneImageMetadata();

            string GetValue(string label)
            {
                var match = Regex.Match(output, @$"{Regex.Escape(label)}\s*:\s*(.+)");
                return match.Success ? match.Groups[1].Value.Trim() : "";
            }

            double? GetDouble(string label)
            {
                string value = GetValue(label);
                if (string.IsNullOrWhiteSpace(value))
                    return null;

                value = value.TrimStart();

                int spaceIndex = value.IndexOf(' ');
                if (spaceIndex > 0)
                    value = value.Substring(0, spaceIndex);

                return double.TryParse(value, out var result) ? result : null;
            }

            data.CameraModelName = GetValue("Camera Model Name");
            data.Software = GetValue("Software");

            try
            {
                var dateStr = GetValue("Create Date");
                DateTime parsedDate = DateTime.ParseExact(dateStr, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                data.CreateDate = parsedDate;
            }
            catch { }

            data.FNumber = GetDouble("F Number");
            data.FocalLength = GetDouble("Focal Length");
            data.ImageWidth = (int?)GetDouble("Image Width");
            data.ImageHeight = (int?)GetDouble("Image Height");
            data.DigitalZoomRatio = GetDouble("Digital Zoom Ratio");
            data.FocalLength35mm = GetDouble("Focal Length In 35mm Format");
            data.SerialNumber = GetValue("Serial Number");
            data.LensInfo = GetValue("Lens Info");
            data.AbsoluteAltitude = GetDouble("Absolute Altitude");
            data.RelativeAltitude = GetDouble("Relative Altitude");
            data.GpsAltitude = GetDouble("GPS Altitude");
            data.GimbalRollDegree = GetDouble("Gimbal Roll Degree");
            data.GimbalYawDegree = GetDouble("Gimbal Yaw Degree");
            data.GimbalPitchDegree = GetDouble("Gimbal Pitch Degree");
            data.FlightRollDegree = GetDouble("Flight Roll Degree");
            data.FlightYawDegree = GetDouble("Flight Yaw Degree");
            data.FlightPitchDegree = GetDouble("Flight Pitch Degree");
            data.DroneModel = GetValue("Drone Model");
            data.DroneSerialNumber = GetValue("Drone Serial Number");
            data.LRFStatus = GetValue("LRF Status");
            data.LRFDistance = GetDouble("LRF Target Distance"); 
            data.LRFLon = GetDouble("LRF Target Lon"); 
            data.LRFLat = GetDouble("LRF Target Lat"); 
            data.LRFAlt = GetDouble("LRF Target Alt"); 
            data.LRFAbsAlt = GetDouble("LRF Target Abs Alt"); 
            data.ScaleFactor35mm = GetDouble("Scale Factor To 35 mm Equivalent");
            data.CircleOfConfusionMM = GetDouble("Circle Of Confusion");
            data.DepthOfFieldM = GetDouble("Depth Of Field");
            data.FieldOfViewDegree = GetDouble("Field Of View");
            data.HyperfocalDistanceM = GetDouble("Hyperfocal Distance");

            // Hamish Kendal's (HK) drone has values like this:
            // Latitude   36 deg 45' 4.52" S
            // Longitude  175 deg 36' 20.81" E
            // Altitude   94.18
            bool haveLong = (data.LRFLon != null) && (Math.Abs((double)data.LRFLon) > 0.001);
            bool haveLat  = (data.LRFLat != null) && (Math.Abs((double)data.LRFLat) > 0.001);
            bool haveAlt  = (data.LRFAlt != null) && (Math.Abs((double)data.LRFAlt) > 0.001);
            string longStr = GetValue("Longitude");
            string latStr = GetValue("Latitude");
            string altStr = GetValue("Altitude"); 
            if (!haveLong && longStr.Contains(" deg ") )
                data.LRFLon = ParseDmsCoordinate(longStr);
            if (!haveLat && latStr.Contains(" deg "))
                data.LRFLat = ParseDmsCoordinate(latStr);
            if ((!haveAlt) && altStr.Contains('.'))
                data.LRFAlt = GetDouble("Altitude");

            return data;
        }

        // Returns FocalLength, ImageWidth, ImageHeight, SensorWidth, SensorHeight
        public static (double fl, int iw, int ih, double sw, double sh) GetCameraIntrinsicParams(DroneImageMetadata firstItem)
        {
            double fl = firstItem.FocalLength ?? 0;
            double fl35 = firstItem.FocalLength35mm ?? 0;
            double sf = fl35 / fl;

            int iw = firstItem.ImageWidth ?? 1;
            int ih = firstItem.ImageHeight ?? 1;

            double esd = Math.Sqrt(36 * 36 + 24 * 24);
            double sd = esd / sf;

            double r = ((double)iw) / ih;

            double sh = sd / Math.Sqrt(1 + r * r);
            double sw = r * sh;

            return (fl, iw, ih, sw, sh);
        }
    }


    public class DroneImageMetadataReader_UnitTest : DroneImageMetadataReader
    {
        public const string UnitTestDirectoryA = @"D:\SkyComb\Data_Input\CC\TLPossum\DJI_202502062106_005_TL3\";
        public const string UnitTestDirectoryB = @"D:\SkyComb\Data_Input\PV\DJI_202504101900_007_PP-Ortho-4-HG\";


        // Unit test sample output
        // 6/02/2025 9:54:15 pm, DJI_20250206215415_0240_T.JPG, 531.899, -47.5, -79.4, 176.6495209, -38.3320808
        // 6/02/2025 9:54:17 pm, DJI_20250206215417_0241_T.JPG, 532.114, -66.1, -79.5, 176.649765, -38.3320999
        // 6/02/2025 9:54:18 pm, DJI_20250206215418_0242_T.JPG, 532.338, -85.5, -79.3, 176.6499481, -38.3321114
        // 6/02/2025 9:54:20 pm, DJI_20250206215420_0243_T.JPG, 532.637, -104.4, -78.8, 176.6500854, -38.3321152
        // 6/02/2025 9:54:23 pm, DJI_20250206215423_0244_T.JPG, 532.956, -99, -78.5, 176.6498108, -38.332077
        // 6/02/2025 9:54:25 pm, DJI_20250206215425_0245_T.JPG, 533.211, -93.4, -78.8, 176.6495514, -38.3320427
        // 6/02/2025 9:54:26 pm, DJI_20250206215426_0246_T.JPG, 533.417, -90.1, -79.1, 176.6493683, -38.3320198
        // 6/02/2025 9:54:29 pm, DJI_20250206215429_0247_T.JPG, 533.693, -90.1, -79.3, 176.6492004, -38.3319931
        // 6/02/2025 9:54:31 pm, DJI_20250206215431_0248_T.JPG, 533.995, -90.1, -79.2, 176.6490326, -38.3319664
        // 6/02/2025 9:54:33 pm, DJI_20250206215433_0249_T.JPG, 534.259, -90.1, -79.5, 176.6488647, -38.3319435
        public static void ReadAll()
        {
            var metadataList = DroneImageMetadataReader.ReadMetadataFromFolder(UnitTestDirectoryA, true);

            foreach (var item in metadataList)
            {
                Debug.Print($"{item.CreateDate}, {item.FileName}, {item.GpsAltitude}, {item.GimbalPitchDegree}, {item.FlightYawDegree}, {item.LRFLon}, {item.LRFLat}");
            }
        }

        public static (double, double, double, double, double) ReadOne(string foldername)
        {
            var metadataList = DroneImageMetadataReader.ReadMetadataFromFolder(foldername, false);

            // Check the first item
            var firstItem = metadataList.FirstOrDefault();
            if (firstItem != null)
            {
                Debug.Print($"FocalLength35mm: {firstItem.FocalLength35mm}");
                Debug.Print($"FocalLength: {firstItem.FocalLength}");
                Debug.Print($"ScaleFactor35mm: {firstItem.ScaleFactor35mm}");
                Debug.Print($"ImageWidth: {firstItem.ImageWidth}");
                Debug.Print($"ImageHeight: {firstItem.ImageHeight}");

                (var fl, var iw, var ih, var sw, var sh) = GetCameraIntrinsicParams(firstItem);

                var sd1 = Math.Sqrt(iw * iw + ih * ih);
                Debug.Print($"sd1: {sd1}");

                var r = iw / ih;
                Debug.Print($"r: {r}");

                Debug.Print($"sw: {sw}");
                Debug.Print($"sh: {sh}");

                var sd2 = Math.Sqrt(sw * sw + sh * sh);
                Debug.Print($"sd2: {sd2}");

                return (fl, iw, ih, sw, sh);
            }

            return (0, 0, 0, 0, 0);
        }
    }
}
