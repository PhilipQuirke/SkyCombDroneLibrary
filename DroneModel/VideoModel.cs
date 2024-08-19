// Copyright SkyComb Limited 2024. All rights reserved.
using Emgu.CV;
using Emgu.CV.CvEnum;
using SkyCombGround.CommonSpace;
using System.Drawing;


namespace SkyCombDrone.DroneModel
{
    // Some basic constant information about a video
    public class VideoModel : BaseConstants
    {
        // Drone and camera combinations for which we have specific settings
        public const string DjiPrefix = "SRT";
        public const string DjiGeneric = "SRT (DJI)";
        public const string DjiM2E = "SRT (DJI M2E Dual)";
        public const string DjiMavic3 = "SRT (DJI Mavic 3)";
        public const string DjiM3T = "SRT (DJI M3T)";
        public const string DjiM300XT2 = "SRT (DJI M300 XT2)";
        public const string DjiH20T = "SRT (DJI H20T)";
        public const string DjiH20N = "SRT (DJI H20N)";


        // THERMAL / OPTICAL CAMERA SETTINGS


        // The file name containing the video
        public string FileName { get; set; }
        // The drone + camera type
        public string CameraType { get; set; }


        // Frames per second. Drone physical implementation means it is not 100% accurate for each second of a drone video.
        // Example Fps seen with M2E Dual are 30 and 8.78
        public double Fps { get; set; }


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
        public int HFOVDeg { get; set; } = 57;
        // Horizontal video image field of view in radians. 
        public double HFOVRad { get { return HFOVDeg * DegreesToRadians; } }
        // Vertical video image field of view in degrees. Differs per manufacturer's camera. Assumes pixels are square
        public double VFOVDeg { get { return HFOVDeg * (double)ImageHeight / ImageWidth; } }
        // Vertical video image field of view in radians. 
        public double VFOVRad { get { return VFOVDeg * DegreesToRadians; } }


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


        // Is it a thermal (aka IR) video? Else an optical (aka visible-light) video.
        // Given the purpose/focus of SkyComb Analyst, we default to true.
        public bool Thermal { get; set; } = true;

        // Thermal camera minimum temperature in degrees Celcius. Default to Mavic 2 Enterprise "Gain Mode" = High value.
        public int ThermalMinTempC { get; set; } = -10;
        // Thermal camera maximum temperature in degrees Celcius. Default to Mavic 2 Enterprise "Gain Mode" = High value.
        public int ThermalMaxTempC { get; set; } = 140;


        // OPTICAL CAMERA SETTINGS


        // FStop. An FStop of 450 is same as f4.5
        // https://www.outdoorphotographyschool.com/aperture-and-f-stops-explained says:
        // An f-stop (or f-number) is the ratio of the lens focal length divided by the
        // diameter of the entrance pupil of the aperture. So an f-stop represents the
        // relative aperture of a lens
        public int MinFStop { get; set; } = UnknownValue;
        public int MaxFStop { get; set; } = UnknownValue;



        // When drawing text on video images, the best font size depends on the image resolution.
        public int FontScale { get { return ImageWidth < 1000 ? 1 : 2; } }
        

        public VideoModel(string fileName, bool thermal, Func<string,DateTime> readDateEncodedUtc)
        {
            FileName = fileName;

            DataAccess = new VideoCapture(FileName);

            Fps = DataAccess.Get(CapProp.Fps); // e.g. 29.97 or 8.7151550960118165
            // Round to defined NDP so first run and second run (after reloading data from DataStore) use the same value.
            Fps = Math.Round(Fps, FpsNdp);

            FrameCount = (int)DataAccess.Get(CapProp.FrameCount);
            ImageWidth = (int)DataAccess.Get(CapProp.FrameWidth);
            ImageHeight = (int)DataAccess.Get(CapProp.FrameHeight);

            Thermal = thermal;

            // Slow to calculate so left uncalculated here
            DurationMs = UnknownValue;

            if(readDateEncodedUtc!= null)
                DateEncodedUtc = readDateEncodedUtc(FileName);
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
            if( durationMs < 0 )
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
                        if(durationStr.Length>pos)
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
                (Thermal ? "Thermal" : "Optical") +
                " video: " +
                ShortFileName() + ", " +
                ImageWidth.ToString() + "x" +
                ImageHeight + "pxs, " +
                DurationMsToString(MillisecondsNdp) + "s, " +
                Fps.ToString("0.0").TrimEnd('0').TrimEnd('.') + "fps";
        }


        // Get the class's settings as datapairs (e.g. for saving to a spreadsheet)
        public DataPairList GetSettings()
        {
            var answer = new DataPairList()
            {
                { "File Name", ShortFileName() },
                { "Camera Type", CameraType },
                { "Fps", Fps, FpsNdp },
                { "Frame Count", FrameCount },
                { "Time Ms", DurationMs },
                { "Image Width", ImageWidth },
                { "Image Height", ImageHeight },
                { "HFOV Deg", HFOVDeg },
                { "VFOV Deg", VFOVDeg, 2 },
                { "Date Encoded Utc", DateEncodedUtc == DateTime.MinValue ? "" : DateEncodedUtc.ToString(BaseConstants.DateFormat) },
                { "Date Encoded", DateEncoded == DateTime.MinValue ? "" : DateEncoded.ToString(BaseConstants.DateFormat) },
                { "Color Md", (ColorMd == "" ? "default" : ColorMd ) },
                { "Thermal", Thermal },
            };

            if (Thermal)
            {
                answer.Add("Thermal Min Temp C", ThermalMinTempC);
                answer.Add("Thermal Max Temp C", ThermalMaxTempC);
            }
            else
            {
                answer.Add("Min FStop", MinFStop);
                answer.Add("Max FStop", MaxFStop);
            }

            return answer;
        }


        // Load this object's settings from strings (loaded from a spreadsheet)
        // This function must align to the above GetSettings function.
        public void LoadSettings(List<string> settings)
        {
            int i = 0;
            FileName = settings[i++];
            CameraType = settings[i++];
            Fps = double.Parse(settings[i++]);
            FrameCount = ConfigBase.StringToInt(settings[i++]);
            DurationMs = ConfigBase.StringToInt(settings[i++]);
            ImageWidth = ConfigBase.StringToInt(settings[i++]);
            ImageHeight = ConfigBase.StringToInt(settings[i++]);
            HFOVDeg = ConfigBase.StringToInt(settings[i++]);
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
            Thermal = ConfigBase.StringToBool(settings[i++]);

            if (Thermal)
            {
                ThermalMinTempC = ConfigBase.StringToInt(settings[i++]);
                ThermalMaxTempC = ConfigBase.StringToInt(settings[i++]);
            }
            else
            {
                MinFStop = ConfigBase.StringToInt(settings[i++]);
                MaxFStop = ConfigBase.StringToInt(settings[i++]);
            }
        }


        // Clear video file handle. More immediate than waiting for garbage collection
        public void Close()
        {
            DataAccess?.Dispose();
            DataAccess = null;
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
    }
}
