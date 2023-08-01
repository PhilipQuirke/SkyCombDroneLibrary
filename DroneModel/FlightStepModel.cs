using SkyCombGround.CommonSpace;



// Models are used in-memory and to persist/load data to/from the datastore
namespace SkyCombDrone.DroneModel
{
    // Cleansed input data about a flight section
    public class FlightStepModel : TardisModel
    {
        public int StepId { get { return TardisId; } }


        // A "leg" is a part of the flight in a constant direction, with constant altitude, of reasonable duration.
        // Many steps will NOT be part of a leg e.g. spinning, climbing, descending, or drone flight log doesn't provide Yaw data.
        public int LegId { get; set; } = 0;
        public string LegName { get { return LegIdToName(LegId); } }


        // The ground (not drone) elevation, above sea level (meters)
        public float DemM { get; set; } = UnknownValue;

        // The surface (i.e tree-top) elevation, above sea level (meters)
        public float DsmM { get; set; } = UnknownValue;


        // InputImageCenter:
        // Center of the step's video IMAGE (not drone) location. May be meters forward of drone location.
        // Accuracy: Depends on drone height (poor) and ground elevation (good) accuracy.
        // Used in ModelCombFeature via FlightStep.CalcImageFeatureLocationM
        public DroneLocation? InputImageCenter { get; set; }

        // InputImageSizeM:  
        // The width & height (in metres) of the thermal image of the ground (aka drone thermal field of vision)
        // Depends on the drone's height above the ground, the camera FOV and camera down angle.
        // We need the image dimensions to calculate the NorthingM/EastingM "delta" of an significant object in the image. 
        // Accuracy: Depends on drone height (poor) and ground elevation (good) accuracy.
        public AreaF? InputImageSizeM { get; set; }


        // Drone absolute velocity and direction as vector. Vector length is SpeedMps
        // The drone absolute (compass) direction of travel IS relevant.
        public VelocityF? StepVelocityMps { get; set; }


        // ImageVelocityMps: "Point of view" velocity and turn rate (in drone Mps).
        // Think of it as the velocity you could detect solely by looking at the video image.
        // So: 
        //  - The drone current-step speed IS relevant
        //  - The drone CHANGE in direction from previous step IS relevant.
        //  - The drone absolute direction of travel is NOT relevant.
        //  - The drone height above ground is NOT directly relevant (but is relevant if used to calculate GroundVelocityMps).
        protected VelocityF? ImageVelocityMps { get; set; }


        public FlightStepModel(FlightSectionModel flightSection, List<string>? settings = null) : base(flightSection.TardisId)
        {
            if (settings != null)
                LoadSettings(settings);
        }


        // One-based settings index values. Must align with GetSettings procedure below
        public const int LegIdSetting = FirstFreeSetting;
        public const int LegNameSetting = FirstFreeSetting + 1;
        public const int DsmSetting = FirstFreeSetting + 2;
        public const int DemSetting = FirstFreeSetting + 3;
        public const int ImageCenterSetting = FirstFreeSetting + 4;
        public const int ImageSizeMSetting = FirstFreeSetting + 5;
        public const int StepVelMpsSetting = FirstFreeSetting + 6;
        public const int ImgVelMpsSetting = FirstFreeSetting + 7;
        public const int HasLegSetting = FirstFreeSetting + 8;


        // Get this FlightStep object's settings as datapairs (e.g. for saving to a datastore). Must align with above index values.
        public override DataPairList GetSettings()
        {
            if(InputImageCenter != null)
                InputImageCenter.AssertGood();
            if (InputImageSizeM != null)
                InputImageSizeM.AssertGood();

            var answer = base.GetSettings();
            answer[0].Key = "Step";

            answer.Add("Leg Id", LegId);
            answer.Add("Leg Name", LegName);
            answer.Add("DSM", DsmM, HeightNdp); // Graphs depend on this name (TBC)
            answer.Add("DEM", DemM, HeightNdp); // Graphs depend on this name (TBC)
            answer.Add("Image Center", (InputImageCenter != null ? InputImageCenter.ToString() : "0,0"));
            answer.Add("Image Size M", (InputImageSizeM != null ? InputImageSizeM.ToString(2) : "0,0"));
            answer.Add("Step Vel Mps", StepVelocityMps.ToString(5));
            answer.Add("Img Vel Mps", (ImageVelocityMps != null ? ImageVelocityMps.ToString(PixelVelNdp) : "0,0"));
            answer.Add("Has Leg", (LegId > 0 ? 1 : 0));

            return answer;
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public override void LoadSettings(List<string> settings)
        {
            base.LoadSettings(settings);

            int i = FirstFreeSetting - 1;
            LegId = StringToNonNegInt(settings[i++]);
            i++; // Skip LegName 
            DsmM = StringToFloat(settings[i++]);
            DemM = StringToFloat(settings[i++]);
            InputImageCenter = new DroneLocation(settings[i++]);
            InputImageSizeM = new AreaF(settings[i++]);
            StepVelocityMps = new VelocityF(settings[i++]);
            ImageVelocityMps = new VelocityF(settings[i++]);
            i++; // Skip HasLeg  

            InputImageCenter.AssertGood();
            InputImageSizeM.AssertGood();
        }


        // Add FlightStep settings to the "Block" settings for saving to Datastore to aid debugging/charting.
        public void AppendStepToBlockSettings(ref DataPairList answer)
        {
            answer.Add("Dsm M", DsmM, HeightNdp);
            answer.Add("Dem M", DemM, HeightNdp);
        }

    };


    // FlightStepsModel summarises a list of FlightSteps and other summary data
    public abstract class FlightStepsModel : FlightStepSummaryModel
    {
        // The file name containing the flight data
        public string FileName { get; set; }


        // To improve location and speed data quality, we smooth Steps by averaging over a window of flight Sections.
        // We average this Step using previous NumSmoothSteps/2 Sections & next NumSmoothSteps/2 Sections.
        // If NumSmoothSteps=4 and SectionMinMs=250, this is smoothing over 1 seconds. 
        // If NumSmoothSteps=0 then this setting has no effect.
        // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md for more details.
        public int NumSmoothSteps { get; set; } = 4;


        // Drone altitudes are often measured using barometic pressure, which is inaccurate, and can be negative!
        // These offsets (derived from OnGroundAt logic) are added to the drone FlightStep altitudes to give more accurate altitudes.
        protected float OnGroundAtFixStartM { get; set; } = 0;
        protected float OnGroundAtFixEndM { get; set; } = 0;
        // Do we have DroneOnGroundAtFix offsets?
        public bool HasOnGroundAtFix { get { return (OnGroundAtFixStartM != 0 || OnGroundAtFixEndM != 0); } }


        public FlightStepsModel(string fileName, int numSmoothSteps, List<string>? settings = null)
        {
            FileName = fileName;
            NumSmoothSteps = numSmoothSteps;
            if (settings != null)
                LoadSettings(settings);
        }


        // Get this FlightSteps object's settings as datapairs (e.g. for saving to a datastore)
        public override DataPairList GetSettings()
        {
            var answer = base.GetSettings();

            answer.Add("File Name", FileName);
            answer.Add("# Smooth Steps", NumSmoothSteps);
            answer.Add("Avg Speed Mps", AvgSpeedMps, 2);
            answer.Add("Min Dem M", MinDemM, HeightNdp);
            answer.Add("Max Dem M", MaxDemM, HeightNdp);
            answer.Add("Min Dsm M", MinDsmM, HeightNdp);
            answer.Add("Max Dsm M", MaxDsmM, HeightNdp);
            answer.Add("OnGroundAt Fix Start M", OnGroundAtFixStartM, HeightNdp);
            answer.Add("OnGroundAt Fix End M", OnGroundAtFixEndM, HeightNdp);

            return answer;
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public override void LoadSettings(List<string> settings)
        {
            base.LoadSettings(settings);

            FileName = settings[23];
            NumSmoothSteps = StringToNonNegInt(settings[24]);
            AvgSpeedMps = StringToFloat(settings[25]);
            MinDemM = StringToFloat(settings[26]);
            MaxDemM = StringToFloat(settings[27]);
            MinDsmM = StringToFloat(settings[28]);
            MaxDsmM = StringToFloat(settings[29]);
            OnGroundAtFixStartM = StringToFloat(settings[30]);
            OnGroundAtFixEndM = StringToFloat(settings[31]);
        }
    }

}
