// Copyright SkyComb Limited 2023. All rights reserved. 
using SkyCombGround.CommonSpace;



// Models are used in-memory and to persist/load data to/from the datastore
namespace SkyCombDrone.DroneModel
{
    public enum GimbalDataEnum { AutoYes, ManualYes, ManualNo };

    public enum OnGroundAtEnum { Start, End, Both, Neither, Auto };


    // Configuration settings related to flight data.  
    //
    // When processing new drone flight data for the first time:
    //      - the below member data default values are used, with a few override default values loaded from App.Config
    //      - the member data values are saved to the DataStore (spreadsheet) specific to this drone flight.
    // When processing that same drone flight for a second or subsequent time:
    //      - ALL member data values are loaded from the DataStore specific to this drone flight. 
    //      - So if you want to trial different values for the same drone flight, alter the setting in the DATASTORE.
    //      - WARNING: Changing the values below will have no effect.
    public class DroneConfigModel : ConfigBase
    {
        // Start the video processing from this point (if specified) in seconds
        public float RunVideoFromS { get; set; } = 5;


        // Stop the video processing at this point (if specified) in seconds
        public float RunVideoToS { get; set; } = 10;


        // Sometimes the thermal and optical videos are out of sync by a few frames. 
        // A ThermalToOpticalVideoDelayS value of 0.5 means the optical video starts 0.5 seconds after the thermal video.
        // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md
        // section Video Start Time Accuracy for more detail. 
        public float ThermalToOpticalVideoDelayS { get; set; } = 0;


        // Older drones provide the pitch, yaw, roll based on the drone's orientation.
        // Newer drones provide the pitch, yaw, roll based on the camera gimbal orientation.
        // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Flight.md 
        public GimbalDataEnum GimbalDataAvail { get; set; } = GimbalDataEnum.ManualNo;
        // Can we use the gimbal data?
        public bool UseGimbalData { get { return GimbalDataAvail != GimbalDataEnum.ManualNo;  } }
        // Used to display "Gimbal Pitch" versus "Drone Pitch" etc in the UI.
        public string PitchYawRollPrefix { get { return UseGimbalData ? "Gimbal" : "Drone"; } }


        // On an older drone, where GimbalDataAvail == ManualNo,
        // FixedCameraDownDeg provides the fixed camera down angle (from the horizontal) for the drone.
        // The setting is a positive number in degrees.
        // So for a camera physically pointing straight down, set FixedCameraDownDeg to 90.
        // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md
        // section Camera Down Angle for more detail.
        public int FixedCameraDownDeg { get; set; } = 80; // Min 25, Max 90
        // The actual camera area imaged depends on CameraDownDeg.
        public int FixedCameraToVerticalForwardDeg { get { return 90 - FixedCameraDownDeg; } }

        // On an new drone, where GimbalDataAvail == AutoYes or ManualYes,
        // If the camera view temporarily includes the horizon, then the camera can experience "thermal bloom",
        // giving bad thermal readings, and lots of suprious features.
        // Manual operators of drones occassionally look at the horizon to make sure the drone is not going to run into anything.
        // Video frames where the camera down angle is GREATER than MinCameraDownDeg are "out of scope" (not processed).
        // Note: If MinCameraDownDeg is set to 35, and camera has vertical field of vision (VFOV) of 47.6 degrees,
        // then the highest view the app processes is 35 +/- 24 degrees which is 11 to 49 degrees down from the horizon.
        // But the drone operator knows best. So we allowa wide range
        public int MinCameraDownDeg { get; set; } = 15; // Min 15, Max 90


        // Does the drone video / flight data start or end at ground level?
        // Takes values Start, End, Both, Neither, Unknown.
        // The drone's self-calculated altitude is inaccurate. If OnGroundAt is Start, End or Both, we can correct that inaccuracy.
        // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md
        // section Drone Altitude Accuracy for more detail.
        // Also https://github.com/PhilipQuirke/SkyCombAnalystHelp/Errors.md
        public OnGroundAtEnum OnGroundAt { get; set; } = OnGroundAtEnum.Auto;


        // Free form text describing the input video(s).
        public string Notes { get; set; } = "";


        // To improve drone location, pitch, yaw, travel and speed data quality,
        // we smooth by averaging over a window of flight sections, to reduce spikes and leaps.
        // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md
        // section Drone Location Accuracy for more detail. 
        // Sections are recorded every SectionMinMs (250), so a SmoothSectionRadius of 2 spans 5 points or ~1.25 second
        public int SmoothSectionRadius { get; set; } = 2;


        // Does this flight benefit from the use of the legs?
        // Defaults to "yes" if legs are >=33% of the flight path.
        public bool UseLegs { get; set; } = true;



        // For many drones the thermal and optical videos have different pixel resolution and horizontal field of vision(HFOV).
        // SkyComb Analyst shows the location of significant objects found in thermal video on top of the optical video.
        // So it needs to know the difference between the HFOV of the thermal and optical videos
        // ExcludeMarginRatio is the "margin" of the optical video that is not displayed in the thermal video, as a ratio between 0.0 and 0.5.
        // Refer ExcludeMarginRatio.md section Camera Down Angle for more detail.
        public float ExcludeDisplayMarginRatio { get; set; } = 0.125f;


        // A FlightLeg is a section of a drone flight path that is at a mostly constant altitude, in a mostly constant direction / pitch
        // of a reasonable duration and travels a reasonable distance.
        // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md for more details.

        // Maximum variation in altitude allowed in a single step for leg to be considered "smooth"
        public float MaxLegStepAltitudeDeltaM { get; set; } = 0.10f;

        // Maximum variation in altitude allowed in a leg for leg to be considered "mostly level" i.e. leg has a constant Altitude within +/- 4 meters
        public float MaxLegSumAltitudeDeltaM { get; set; } = 1.0f;


        // Maximum variation in direction for a single step for the leg to be considered "mostly in one direction"  
        public int MaxLegStepDeltaYawDeg { get; set; } = 4;

        // Maximum variation in direction allowed over the leg for the leg to be considered "mostly in one direction" 
        public int MaxLegSumDeltaYawDeg { get; set; } = 10;


        // If GimbalDataAvail == ManualNo
        //      When there is no wind, drone flight is mostly level (i.e. pitch is 0). When a drone 
        //      approaches a corner it pitches up to slow down, and when a drone leaves a corner
        //      it pitches down to speed down. Both pitches change the apparent velocity of features. 
        //      As a drone pitchs up to slow down, over successive 1/4 second intervals, the
        //      raw pitch can spike from 0.1 to 14.4 to 20.8 to 18.7 to 3.0 to 0.6 to 0.1
        //      Assume large "steps" in pitch will have a higher inaccuracy 
        //      Note that the camera gimbal automatically compensates (negates) the impact of
        //      changes in drone pitch & roll on the video images captured.
        // Else
        //      The gimbal camera down angle is likely consistent over legs and corners,
        //      with the gimbal compensating for changes in the drone pitch & roll,
        //      but could be 45 degrees.
        //      In this case refer MinCameraDownDeg
        // (In both cases the Gimbal yaw mirrors the drone yaw - with a lag when cornering.)
        public int MaxLegStepPitchDeg { get; set; } = 12; // Degrees
        public int MaxLegSumPitchDeg { get; set; } = 18; // Degrees


        // Minimum duration of a leg in milliseconds 
        public int MinLegDurationMs { get; set; } = 2000; // 2 seconds


        // Minimum distance drone must travels horizontally over the leg
        public float MinLegDistanceM { get; set; } = 5;


        // Maximum gap in flight data gap allowed in a leg. (In rare cases, drone hardware issues can result
        // in a say 1.5s gap in the flight record and video during the middle of a "leg", effectively breaking it into two legs.)
        public int MaxLegGapDurationMs { get; set; } = 2 * FlightSectionModel.SectionMinMs;


        // The Camera down angle must be in range +25 to +90 degrees.
        // Pointing at the horizon is 0 degrees, and is bad as 1) areas imaged by the video are far away.
        // and 2) thermal cameras experience thermal bloom and their measurements are unreliable.
        public void ValidateFixedCameraDownDeg()
        {
            if (GimbalDataAvail == GimbalDataEnum.ManualNo)
            {
                if (FixedCameraDownDeg < 25)
                    FixedCameraDownDeg = 25;

                if (FixedCameraDownDeg > 90)
                    FixedCameraDownDeg = 90;
            }
            else
                // CameraDownDeg is not used if the Gimbal data is available
                FixedCameraDownDeg = 0;
        }


        public void ValidateMinCameraDownDeg()
        {
            if (MinCameraDownDeg < 15)
                MinCameraDownDeg = 15;

            if (MinCameraDownDeg > 90)
                MinCameraDownDeg = 90;
        }


        // Get the class's settings as datapairs (e.g. for saving to a datastore)
        public DataPairList GetSettings()
        {
            return new DataPairList
            {
                { "Run Video From S", RunVideoFromS, SecondsNdp },
                { "Run Video To S", RunVideoToS, SecondsNdp },
                { "Thermal To Optical Video Delay S", ThermalToOpticalVideoDelayS, SecondsNdp },
                { "Gimbal Data Available", GimbalDataAvail.ToString() },
                { "Fixed Camera Down Degrees", FixedCameraDownDeg },
                { "Min Camera Down Degrees", MinCameraDownDeg },
                { "On Ground At", OnGroundAt.ToString() },
                { "Smooth Section Radius", SmoothSectionRadius },
                { "Use Legs", UseLegs },
                { "Exclude Margin Ratio", ExcludeDisplayMarginRatio, 3 },
                { "Notes", ( Notes == "" ? " " : Notes ) },
            };
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public void LoadSettings(List<string> settings)
        {
            int i = 0;
            RunVideoFromS = StringToFloat(settings[i++]);
            RunVideoToS = StringToFloat(settings[i++]);
            ThermalToOpticalVideoDelayS = StringToFloat(settings[i++]);
            GimbalDataAvail = (GimbalDataEnum)Enum.Parse(typeof(GimbalDataEnum), settings[i++]);
            FixedCameraDownDeg = StringToNonNegInt(settings[i++]);
            MinCameraDownDeg = StringToNonNegInt(settings[i++]);
            OnGroundAt = (OnGroundAtEnum)Enum.Parse(typeof(OnGroundAtEnum), settings[i++]);
            SmoothSectionRadius = StringToNonNegInt(settings[i++]);
            UseLegs = StringToBool(settings[i++]);
            ExcludeDisplayMarginRatio = StringToFloat(settings[i++]);
            Notes = settings[i++];

            ValidateFixedCameraDownDeg();
            ValidateMinCameraDownDeg();
        }


        // Get the class's "Leg" settings as datapairs (e.g. for saving to a datastore). Must align with above index values.
        public DataPairList GetLegSettings()
        {
            return new DataPairList
            {
                { "Max Leg Step Alt Delta M", MaxLegStepAltitudeDeltaM, LocationNdp },
                { "Max Leg Sum Alt Delta M", MaxLegSumAltitudeDeltaM, LocationNdp },
                { "Max Leg Step Delta Yaw Deg", MaxLegStepDeltaYawDeg },
                { "Max Leg Sum Delta Yaw Deg", MaxLegSumDeltaYawDeg },
                { "Min Leg Duration Ms", MinLegDurationMs },
                { "Min Leg Distance M", MinLegDistanceM, LocationNdp },
                { "Max Leg Gap Duration Ms",  MaxLegGapDurationMs },
                { "Max Leg Step Pitch Deg",  MaxLegStepPitchDeg },
                { "Max Leg Sum Pitch Deg",  MaxLegSumPitchDeg },
            };
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetLegSettings function.
        public void LoadLegSettings(List<string> settings)
        {
            int i = 0;
            MaxLegStepAltitudeDeltaM = StringToNonNegFloat(settings[i++]);
            MaxLegSumAltitudeDeltaM = StringToNonNegFloat(settings[i++]);
            MaxLegStepDeltaYawDeg = StringToInt(settings[i++]);
            MaxLegSumDeltaYawDeg = StringToInt(settings[i++]);
            MinLegDurationMs = StringToNonNegInt(settings[i++]);
            MinLegDistanceM = StringToNonNegFloat(settings[i++]);
            MaxLegGapDurationMs = StringToNonNegInt(settings[i++]);
            MaxLegStepPitchDeg = StringToInt(settings[i++]);
            MaxLegSumPitchDeg = StringToInt(settings[i++]);
        }


        // Describe (summarise) the drone settings.
        public string Describe()
        {
            var answer =
                "From " + VideoModel.DurationSecToString(RunVideoFromS) +
                " to " + VideoModel.DurationSecToString(RunVideoToS) + " \r\n";

            if (OnGroundAt != OnGroundAtEnum.Neither)
                answer += "On ground at: " + OnGroundAt + "\r\n";

            if ((GimbalDataAvail == GimbalDataEnum.AutoYes) ||
               (GimbalDataAvail == GimbalDataEnum.ManualYes))
                answer += "Gimbal pitch, yaw, roll available\r\n";
            else
                answer += "Camera down: " + FixedCameraDownDeg + " degrees\r\n";

            if (ThermalToOpticalVideoDelayS != 0)
                answer += "Thermal to optical video: " + ThermalToOpticalVideoDelayS + " secs\r\n";

            if (Notes != "")
                answer += "Notes: " + Notes;

            return answer;
        }
    }
}