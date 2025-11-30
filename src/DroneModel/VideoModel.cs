// Copyright SkyComb Limited 2025. All rights reserved.
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SkyCombGround.CommonSpace;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;


namespace SkyCombDrone.DroneModel
{
    // Some basic constant information about a video
    public class VideoModel : BaseConstants, IDisposable
    {
        // Drone and camera combinations we have some knowledge about
        public const string DjiGeneric = "DJI";
        public const string DjiM2E = "DJI M2E Dual";
        public const string DjiMavic3 = "DJI Mavic 3";
        public const string DjiM3T = "DJI M3T";
        public const string DjiM300XT2 = "DJI M300 XT2";
        public const string DjiM4T = "DJI M4T";
        public const string DjiH20T = "ZH20T"; // Z for ZenMuse
        public const string DjiH20N = "ZH20N"; // Z for ZenMuse. Matches DJI image property value
        public const string DjiH30T = "ZH30T"; // Z for ZenMuse


        // THERMAL / OPTICAL CAMERA SETTINGS


        // The file name containing the video
        public string FileName { get; set; }
        // The drone + camera type
        public string CameraType { get; set; } = "";


        // Frames per second. Drone physical implementation means it is not 100% accurate for each second of a drone video.
        // Example Fps seen with M2E Dual are 30 and 8.78
        public double Fps { get; set; } = UnknownValue;


        // Total number of frames in video
        public int FrameCount { get; set; } = UnknownValue;


        // Duration of video in milliseconds
        public int DurationMs { get; set; } = UnknownValue;


        // Focal length in mm. This is the focal length of the lens used to capture the video.
        public double FocalLength { get; set; } = UnknownValue;
        // Height of video frame in pixels
        public int ImageHeight { get; set; } = UnknownValue;
        // Width of video frame in pixels
        public int ImageWidth { get; set; } = UnknownValue;
        // Size of the video frame in pixels
        public Size ImageSize { get { return new Size(ImageWidth, ImageHeight); } }
        // Size of the video frame in pixels
        public int ImagePixels { get { return ImageWidth * ImageHeight; } }
        // The thermal camera sensor width in mm. This is the width of the sensor used to capture the video.
        public double SensorWidth { get; set; } = UnknownValue;
        // The thermal camera sensor height in mm. This is the height of the sensor used to capture the video.
        public double SensorHeight { get; set; } = UnknownValue;
        // Horizontal video image field of view in degrees. Differs per manufacturer's camera.
        public float HFOVDeg { get; set; } = 38.2f;
        // Vertical video image field of view in degrees. Differs per manufacturer's camera. Assumes pixels are square
        public float VFOVDeg { get { return HFOVDeg * ImageHeight / ImageWidth; } }


        // The UTC date/time the video file was encoded.
        // On the drone, the 4 Created and Modified datetimes for the two (thermal and optical) videos are identical.
        // When the 2 videos files are copied to a hard disk, the Created datetimes are updated (but the Modified datetimes are not changed), and may differ from each other.
        // So we rely on the video MediaModified datetimes
        public DateTime DateEncodedUtc { get; set; } = DateTime.MinValue;

        // The local date/time the video file was encoded.
        public DateTime DateEncoded { get; set; } = DateTime.MinValue;


        // The Color Palette name. Assumed constant through the video 
        public string ColorMd { get; set; } = "default";


        // The video capture object providing access to the video contents
        protected VideoCapture? DataAccess { get; set; } = null;


        // THERMAL CAMERA SETTINGS

        // Thermal camera minimum temperature in degrees Celcius. Default to Mavic 2 Enterprise "Gain Mode" = High value.
        public int ThermalMinTempC { get; set; } = -10;
        // Thermal camera maximum temperature in degrees Celcius. Default to Mavic 2 Enterprise "Gain Mode" = High value.
        public int ThermalMaxTempC { get; set; } = 140;


        // FStop. An FStop of 450 is same as f4.5
        // https://www.outdoorphotographyschool.com/aperture-and-f-stops-explained says:
        // An f-stop (or f-number) is the ratio of the lens focal length divided by the
        // diameter of the entrance pupil of the aperture. So an f-stop represents the
        // relative aperture of a lens
        public int MinFStop { get; set; } = UnknownValue;
        public int MaxFStop { get; set; } = UnknownValue;



        // When drawing text on video images, the best font size depends on the image resolution.
        public int FontScale { get { return ImageWidth < 1000 ? 1 : 2; } }


        public bool HasDataAccess { get { return DataAccess != null; } }
        public void AssertDataAccess()
        {
            Assert(HasDataAccess, "DataAccess is null");
        }


        public VideoModel(string videoFileName, Func<string, DateTime> readDateEncodedUtc)
        {
            FileName = videoFileName;
            if (FileName == "")
                return;

            try
            {
                if (!System.IO.File.Exists(FileName))
                    // This sometimes happens when files are transferred between laptops
                    // when one laptop uses C: and the other uses D:
                    // Check the file name locations in the xls very carefully.
                    throw new Exception("VideoModel: File does not exist: " + FileName);

                try
                {
                    DataAccess = new VideoCapture(FileName);
                }
                catch (Emgu.CV.Util.CvException ex)
                {
                    Debug.Print("Exception: " + ex.Message);
                }
                if (!DataAccess.IsOpened)
                    Debug.Print("Failed to open video file.");
                else
                    Debug.Print("Backend Name: " + DataAccess.BackendName);
                AssertDataAccess();

                Fps = DataAccess.Get(CapProp.Fps); // e.g. 29.97 or 8.7151550960118165
                // Round to defined NDP so first run and second run (after reloading data from DataStore) use the same value.
                Fps = Math.Round(Fps, FpsNdp);

                FrameCount = (int)DataAccess.Get(CapProp.FrameCount);
                ImageWidth = (int)DataAccess.Get(CapProp.FrameWidth);
                ImageHeight = (int)DataAccess.Get(CapProp.FrameHeight);

                // Slow to calculate so left uncalculated here
                DurationMs = UnknownValue;

                if (readDateEncodedUtc != null)
                    DateEncodedUtc = readDateEncodedUtc(FileName);
            }
            catch
            {
                FreeResources();
                throw;
            }
        }


        // Convert from video frame number into video frame's offset in milliseconds. No offsets applied.
        // Approximate only (as Fps is approximate, especially for drone videos)
        public int FrameIdToApproxMs(int videoFrameId)
        {
            if (videoFrameId <= 0)
                return 0;

            return (int)(1000.0 * videoFrameId / Fps);
        }


        public void CalculateApproxDurationMs()
        {
            // Approximate method (as Fps is approximate, especially for drone videos)
            DurationMs = FrameIdToApproxMs(FrameCount);
        }


        // Convert the duration to a string in the format "9:45" or "9:45.33" or "45" or "45.33"
        public static string DurationSecToString(double durationSec, int ndp = 2)
        {
            try
            {
                if (durationSec < 0)
                    return "";

                var format = (ndp == 2 ? @"mm\:ss\.ff" : (ndp == 1 ? @"mm\:ss\.f" : @"mm\:ss"));

                var answer = TimeSpan.FromSeconds(Math.Round(durationSec, ndp)).ToString(format);

                if (answer.StartsWith("00:0"))
                    answer = answer.Substring(4);
                else if (answer.StartsWith("00:"))
                    answer = answer.Substring(3);
                else if (answer.StartsWith("0"))
                    answer = answer.Substring(1);

                answer = answer.Replace(".00", "");

                return answer;
            }
            catch (Exception ex)
            {
                throw ThrowException("VideoModel.DurationSecToString", ex);
            }
        }
        public static string DurationMsToString(double durationMs, int ndp)
        {
            if (durationMs < 0)
                return "";

            return DurationSecToString(durationMs / 1000.0, ndp);
        }
        public string DurationMsToString(int ndp)
        {
            return DurationMsToString(DurationMs, ndp);
        }


        // Parse durations strings "145", "145.12", "2:25", "2:25.12" into seconds
        public static float DurationStringtoSecs(string durationStr)
        {
            float answer = 0;

            try
            {
                durationStr = durationStr.ToLower().Trim();
                if (durationStr.Length > 0)
                {
                    // If the string contains a ":" then extract the minutes value
                    var pos = durationStr.IndexOf(":");
                    if (pos > 0)
                    {
                        var minutes = durationStr.Substring(0, pos);
                        answer += float.Parse(minutes) * 60;

                        // Could see string "2:" or "2:25" or "2:25.12"
                        if (durationStr.Length > pos)
                            durationStr = durationStr.Substring(pos + 1);
                        else
                            durationStr = "";
                    }

                    if (durationStr.Length > 0)
                        answer += float.Parse(durationStr);
                }

                return answer;
            }
            catch
            {
                return UnknownValue;
            }
        }


        public string DescribeSelf()
        {
            return
                "Thermal video: " +
                ShortFileName() + ", " +
                ImageWidth.ToString() + "x" +
                ImageHeight + "pxs, " +
                DurationMsToString(MillisecondsNdp) + "s, " +
                Fps.ToString("0.0").TrimEnd('0').TrimEnd('.') + "fps";
        }


        // Get the class's settings as datapairs (e.g. for saving to a spreadsheet)
        public DataPairList GetSettings()
        {
            return new DataPairList()
            {
                { "File Name", ShortFileName() },
                { "Camera Type", CameraType },
                { "Fps", Fps, FpsNdp },
                { "Frame Count", FrameCount },
                { "Time Ms", DurationMs },
                { "Focal Length", FocalLength, 1 },
                { "Image Width", ImageWidth },
                { "Image Height", ImageHeight },
                { "Sensor Width", SensorWidth, 1 },
                { "Sensor Height", SensorHeight, 1 },
                { "HFOV Deg", HFOVDeg, 1 },
                { "VFOV Deg", VFOVDeg, 1 },
                { "Date Encoded Utc", DateEncodedUtc == DateTime.MinValue ? "" : DateEncodedUtc.ToString(BaseConstants.DateFormat) },
                { "Date Encoded", DateEncoded == DateTime.MinValue ? "" : DateEncoded.ToString(BaseConstants.DateFormat) },
                { "Color Md", (ColorMd == "" ? "default" : ColorMd ) },
                { "Thermal Min Temp C", ThermalMinTempC },
                { "Thermal Max Temp C", ThermalMaxTempC },
            };
        }


        // Load this object's settings from strings (loaded from a spreadsheet)
        // This function must align to the above GetSettings function.
        public void LoadSettings(List<string> settings)
        {
            if (settings == null)
                return;

            int i = 0;
            FileName = settings[i++];
            CameraType = settings[i++];
            Fps = double.Parse(settings[i++]);
            FrameCount = ConfigBase.StringToInt(settings[i++]);
            DurationMs = ConfigBase.StringToInt(settings[i++]);
            FocalLength = ConfigBase.StringToFloat(settings[i++]);
            ImageWidth = ConfigBase.StringToInt(settings[i++]);
            ImageHeight = ConfigBase.StringToInt(settings[i++]);
            SensorWidth = ConfigBase.StringToFloat(settings[i++]);
            SensorHeight = ConfigBase.StringToFloat(settings[i++]);
            HFOVDeg = ConfigBase.StringToFloat(settings[i++]);
            i++; // Skip VFOVDeg 

            if((settings[i] != "") && (settings[i] != UnknownString))
                DateEncodedUtc = DateTime.Parse(settings[i++]);
            else
            {
                DateEncodedUtc = DateTime.MinValue;
                i++;
            }
            if (settings[i] != "")
                DateEncoded = DateTime.Parse(settings[i++]);
            else
            {
                DateEncoded = DateTime.MinValue;
                i++;
            }
            ColorMd = settings[i++].ToLower();

            ThermalMinTempC = ConfigBase.StringToInt(settings[i++]);
            ThermalMaxTempC = ConfigBase.StringToInt(settings[i++]);
        }


        // Clear video file handle. More immediate than waiting for garbage collection
        public void FreeResources()
        {
            if (DataAccess != null)
            {
                DataAccess.Dispose();
                DataAccess = null;
            }
        }


        // Calculate % overlap of two VideoDatas, based on video DateEncodedUtc datetime and durationMs
        // Used to determine if the two VideoDatas relate to same physical flight (one optical and one thermal).
        public static int PercentOverlap(VideoModel? video1, VideoModel? video2)
        {
            if (video1 == null || video2 == null)
                return UnknownValue;

            if (video1.DateEncodedUtc.Date != video2.DateEncodedUtc.Date)
                return 0; // Different days

            if (video1.DateEncodedUtc > video2.DateEncodedUtc || video2.DateEncodedUtc > video1.DateEncodedUtc)
                return 0;

            var f1min = video1.DateEncodedUtc.ToFileTime();
            var f1max = video1.DateEncodedUtc.AddMilliseconds(video1.DurationMs).ToFileTime();
            var f2min = video2.DateEncodedUtc.ToFileTime();
            var f2max = video2.DateEncodedUtc.AddMilliseconds(video2.DurationMs).ToFileTime();

            var maxMin = Math.Max(f1min, f2min);
            var minMax = Math.Min(f1max, f2max);
            var minMin = Math.Min(f1min, f2min);
            var maxMax = Math.Max(f1max, f2max);

            double maxDuration = maxMax - minMin;
            double overlap = 100.0 * (minMax - maxMin) / maxDuration;
            return (int)overlap;
        }


        // Return the file name (if any) else the last folder name
        public static string ShortFolderFileName(string filename)
        {
            var index = filename.LastIndexOf('\\');
            if (index < 0)
                return "";

            var answer = filename.Substring(index + 1);

            // Use Path.GetFileNameWithoutExtension and Path.GetExtension for robust extension handling
            var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(answer);
            var ext = System.IO.Path.GetExtension(answer).ToLower();
            if (!string.IsNullOrEmpty(nameWithoutExt))
                return nameWithoutExt.ToUpper() + ext;
            return answer;
        }
        public string ShortFileName()
        {
            return ShortFolderFileName(FileName);
        }


        public static string RemoveFileNameSuffix(string filename)
        {
            // Use Path.GetFileNameWithoutExtension for robust extension handling
            var index = filename.LastIndexOf('\\');
            var name = (index < 0) ? filename : filename.Substring(index + 1);
            return System.IO.Path.GetFileNameWithoutExtension(name);
        }
        public string ShortFilePrefix()
        {
            return RemoveFileNameSuffix(ShortFileName());
        }


        // Read a single optical image from disk into memory based on regex
        // Try file names with the timestamp numerically +1 or -1 than the specified file name
        public static (string opticalImagePath, Image<Bgr, byte>? opticalImage) 
            GetOpticalImageRegex(string opticalImagePath, Regex regex)
        {
            var fileNameOnly = System.IO.Path.GetFileName(opticalImagePath);
            var match = regex.Match(fileNameOnly);
            if (match.Success)
            {
                long timestamp;
                if (long.TryParse(match.Groups[1].Value, out timestamp))
                {
                    string baseName = opticalImagePath.Replace(match.Groups[1].Value, "{TIMESTAMP}");

                    // Try +1
                    var plusTimestamp = (timestamp + 1).ToString().PadLeft(14, '0');
                    string altFileNamePlus = baseName.Replace("{TIMESTAMP}", plusTimestamp);
                    if (File.Exists(altFileNamePlus))
                        return (altFileNamePlus, new Image<Bgr, byte>(altFileNamePlus));

                    // Try -1
                    var minusTimestamp = (timestamp - 1).ToString().PadLeft(14, '0');
                    string altFileNameMinus = baseName.Replace("{TIMESTAMP}", minusTimestamp);
                    if (File.Exists(altFileNameMinus))
                        return (altFileNameMinus, new Image<Bgr, byte>(altFileNameMinus));
                }
            }

            return ("", null);
        }


        // Given the thermalImageFileName, read the corresponding single optical image from disk into memory
        public static (string opticalImagePath, Image<Bgr, byte>? opticalImage)  
            GetOpticalImage(string thermalImagePath)
        {
            // For example: D:\SkyComb\Data_Input\HK\9Oct25\DJI_20251011071259_0008_T_point0.jpg
            var opticalImagePath1 = thermalImagePath.Replace("_T_", "_V_");
            if (opticalImagePath1 != thermalImagePath)
            {
                if (File.Exists(opticalImagePath1))
                    return (opticalImagePath1, new Image<Bgr, byte>(opticalImagePath1));

                // Sometimes get: D:\SkyComb\Data_Input\HK\9Oct25\DJI_20251011071260_0008_V_point0.jpg
                // Try file names with the timestamp numerically +1 or -1, and any pointN suffix
                var regex1 = new System.Text.RegularExpressions.Regex(
                    @"DJI_(\d{14})_(\d{4})_V_point(\d+)\.jpg$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                (var opticalImagePath2, var opticalFile2) = VideoModel.GetOpticalImageRegex(opticalImagePath1, regex1);
                if (opticalFile2 != null)
                    return (opticalImagePath2, opticalFile2);
            }

            // For example: D:\SkyComb\Data_Input\HK\31OctPrincess\DJI_20251031065732_0001_T.jpg
            var opticalImagePath3 = thermalImagePath.Replace("_T.", "_V.");
            if (opticalImagePath3 != thermalImagePath)
            {
                if (File.Exists(opticalImagePath3))
                    return (opticalImagePath3, new Image<Bgr, byte>(opticalImagePath3));

                var regex3 = new System.Text.RegularExpressions.Regex(
                    @"DJI_(\d{14})_(\d{4})_V\.jpg$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                (var opticalImagePath4, var opticalFile4) = VideoModel.GetOpticalImageRegex(opticalImagePath3, regex3);
                if (opticalFile4 != null)
                    return (opticalImagePath4, opticalFile4);
            }

            return ("", null);
        }


        private bool disposed = false;


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                }

                // Dispose unmanaged resources
                FreeResources();

                disposed = true;
            }
        }

        ~VideoModel()
        {
            Dispose(false);
        }
    }
}
