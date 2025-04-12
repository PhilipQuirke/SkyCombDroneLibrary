// Copyright SkyComb Limited 2025. All rights reserved. 
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;


namespace SkyCombDrone.DroneLogic
{
    public class DroneImageMetadata
    {
        public string FileName { get; set; }
        public string CameraModelName { get; set; }
        public string Software { get; set; }
        public DateTime CreateDate { get; set; }
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
        public static List<DroneImageMetadata> ReadMetadataFromFolder(string folderPath, string exifToolPath)
        {
            var files = Directory.GetFiles(folderPath, "*_T*.JPG");
            var metadataList = new List<DroneImageMetadata>();

            foreach (var file in files)
            {
                string output = RunExifTool(exifToolPath, file);
                var metadata = ParseExifOutput(output);
                if (metadata != null)
                {
                    metadata.FileName = Path.GetFileName(file);
                    metadataList.Add(metadata);
                }
            }

            return metadataList
                .OrderBy(m => m.CreateDate)
                .ThenBy(m => m.FileName)
                .ToList();
        }

        private static string RunExifTool(string exifToolPath, string filePath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exifToolPath,
                    Arguments = $"\"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        private static DroneImageMetadata ParseExifOutput(string output)
        {
            var data = new DroneImageMetadata();

            string GetValue(string label)
            {
                var match = Regex.Match(output, @$"{Regex.Escape(label)}\s*:\s*(.+)");
                return match.Success ? match.Groups[1].Value.Trim() : null;
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

                return double.TryParse(value, out var result) ? result : (double?)null;
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

            return data;
        }

        public const string UnitTestDirectory = @"D:\SkyComb\Data_Input\CC\TLPossum\DJI_202502062106_005_TL3\";
        //public const string UnitTestImage = @"D:\SkyComb\Data_Input\CC\TLPossum\DJI_202502062106_005_TL3\DJI_20250206214525_0016_T.JPG";
        public const string exifToolPath = @"D:\SkyComb\exiftool-13.26_64\exiftool.exe"; // Removed (-k) from the exe name to run it in batch mode (not interactively)

        public static void UnitTest1()
        {
            var metadataList = DroneImageMetadataReader.ReadMetadataFromFolder(UnitTestDirectory, exifToolPath);

            foreach (var item in metadataList)
            {
                Debug.WriteLine($"{item.CreateDate}, {item.FileName}, {item.AbsoluteAltitude}, {item.GimbalPitchDegree}, {item.FlightYawDegree}, {item.LRFLon}, {item.LRFLat}");
            }

        }
    }

}
