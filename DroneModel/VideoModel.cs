// Copyright SkyComb Limited 2024. All rights reserved.
using Emgu.CV;
using Emgu.CV.CvEnum;
using SkyCombGround.CommonSpace;
using System.Diagnostics;
using System.Drawing;


namespace SkyCombDrone.DroneModel
{
    // Some basic constant information about a video
    public class VideoModel : BaseConstants, IDisposable
    {
        // Drone and camera combinations for which we have specific settings
        public const string DjiGeneric = "DJI";
        public const string DjiM2E = "DJI M2E Dual";
        public const string DjiMavic3 = "DJI Mavic 3";
        public const string DjiM3T = "DJI M3T";
        public const string DjiM300XT2 = "DJI M300 XT2";
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


        // Height of video frame in pixels
        public int ImageHeight { get; set; } = UnknownValue;
        // Width of video frame in pixels
        public int ImageWidth { get; set; } = UnknownValue;
        // Size of the video frame in pixels
        public Size ImageSize { get { return new Size(ImageWidth, ImageHeight); } }
        // Size of the video frame in pixels
        public int ImagePixels { get { return ImageWidth * ImageHeight; } }


        // Horizontal video image field of view in degrees. Differs per manufacturer's camera.
        public float HFOVDeg { get; set; } = 38.2f;
        // Vertical video image field of view in degrees. Differs per manufacturer's camera. Assumes pixels are square
        public float VFOVDeg { get { return HFOVDeg * (float)ImageHeight / ImageWidth; } }


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
            if(FileName == "")
                return;

            try
            {
                if (!System.IO.File.Exists(FileName))
                    // This sometimes happens when files are transferred between laptops
                    // when one laptop uses C: and the other uses D:
                    // Check the file name locations in the xls very carefully.
                    throw new Exception( "VideoModel: File does not exist: " +  FileName );

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
                { "Image Width", ImageWidth },
                { "Image Height", ImageHeight },
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
            ImageWidth = ConfigBase.StringToInt(settings[i++]);
            ImageHeight = ConfigBase.StringToInt(settings[i++]);
            HFOVDeg = ConfigBase.StringToFloat(settings[i++]);
            i++; // Skip VFOVDeg 

            if (settings[i] != "")
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


        public static string ShortFileName(string filename)
        {
            var index = filename.LastIndexOf('\\');
            if (index < 0)
                return "";

            var answer = filename.Substring(index + 1);

            // Uppercase filename and lowercase suffix for consistency
            return
                answer.Substring(0, answer.LastIndexOf('.')).ToUpper() +
                answer.Substring(answer.LastIndexOf('.')).ToLower();
        }
        public string ShortFileName()
        {
            return ShortFileName(FileName);
        }


        public static string RemoveFileNameSuffix(string filename)
        {
            if (filename.Length < 4)
                return filename;

            return filename.Substring(0, filename.Length - 4);
        }
        public string ShortFilePrefix()
        {
            return RemoveFileNameSuffix(ShortFileName());
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
