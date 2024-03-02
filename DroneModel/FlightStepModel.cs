// Copyright SkyComb Limited 2023. All rights reserved.
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
        public int FlightLegId { get; set; } = 0;
        public string FlightLegName { get { return IdToLetter(FlightLegId); } }


        // The ground (not drone) elevation, above sea level (meters)
        // directly below the drone
        public float DemM { get; set; } = UnknownValue;
        // The surface (i.e tree-top) elevation, above sea level (meters)
        // directly below the drone
        public float DsmM { get; set; } = UnknownValue;
        // Get the best measure of the surface elevation we have available.
        public float DsmElseDemM { get { return (DsmM != UnknownValue ? DsmM : DemM); } }


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

        // Unit vector from drone location to the input image.
        // If GimbalDataAvail
        // then Yaw is in camera direction and may differ from drone direction to travel.
        // else Yaw is the drone direction of travel.
        // Either way we use yaw to determine the InputImageCenter.
        public VelocityF InputImageUnitVector { get { 
                return new VelocityF(
                    - (float)Math.Cos( YawRad + Math.PI / 2 ), 
                    + (float)Math.Sin( YawRad + Math.PI / 2 )); } }

        // The ground (not drone) elevation, above sea level (meters)
        // at the centre of the imaged area
        public float InputImageDemM { get; set; } = UnknownValue;

        // The surface (i.e tree-top) elevation, above sea level (meters)
        // at the centre of the imaged area
        public float InputImageDsmM { get; set; } = UnknownValue;


        // The amount to correct the altitude reported by the drone in this step.
        public float FixAltM { get; set; } = 0;


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
        public const int ImageDemMSetting = FirstFreeSetting + 6;
        public const int ImageDsmMSetting = FirstFreeSetting + 7;
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

            answer.Add("Leg Id", FlightLegId);
            answer.Add("Leg Name", FlightLegName);
            answer.Add("DSM", DsmM, HeightNdp); // Graphs depend on this name (TBC)
            answer.Add("DEM", DemM, HeightNdp); // Graphs depend on this name (TBC)
            answer.Add("Img Center", (InputImageCenter != null ? InputImageCenter.ToString() : "0,0"));
            answer.Add("Img Size M", (InputImageSizeM != null ? InputImageSizeM.ToString(2) : "0,0"));
            answer.Add("Img Dem M", InputImageDemM, ElevationNdp);
            answer.Add("Img Dsm M", InputImageDsmM, ElevationNdp);
            answer.Add("Has Leg", (FlightLegId > 0 ? 1 : 0));
            // We do not save FixAltM

            return answer;
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public override void LoadSettings(List<string> settings)
        {
            base.LoadSettings(settings);

            int i = FirstFreeSetting - 1;
            FlightLegId = StringToNonNegInt(settings[i++]);
            i++; // Skip LegName 
            DsmM = StringToFloat(settings[i++]);
            DemM = StringToFloat(settings[i++]);
            InputImageCenter = new DroneLocation(settings[i++]);
            InputImageSizeM = new AreaF(settings[i++]);
            InputImageDemM = StringToFloat(settings[i++]);
            InputImageDsmM = StringToFloat(settings[i++]);
            i++; // Skip HasLeg  
            // We do not load FixAltM. It is updated by CombSpan on load

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


        // Drone altitudes are often measured using barometic pressure, which is inaccurate, and can be negative!
        // These offsets (derived from OnGroundAt logic) are added to the drone FlightStep altitudes to give more accurate altitudes.
        protected float OnGroundAtFixStartM { get; set; } = 0;
        protected float OnGroundAtFixEndM { get; set; } = 0;
        // Do we have DroneOnGroundAtFix offsets?
        public bool HasOnGroundAtFix { get { return (OnGroundAtFixStartM != 0 || OnGroundAtFixEndM != 0); } }


        public FlightStepsModel(string fileName, List<string>? settings = null)
        {
            FileName = fileName;
            if (settings != null)
                LoadSettings(settings);
        }


        public string ShortFileName()
        {
            if (FileName == "")
                return "";

            var answer = FileName.Substring(FileName.LastIndexOf('\\') + 1);

            // Uppercase filename and lowercase suffix for consistency
            return
                answer.Substring(0, answer.LastIndexOf('.')).ToUpper() +
                answer.Substring(answer.LastIndexOf('.')).ToLower();
        }


        // Get this FlightSteps object's settings as datapairs (e.g. for saving to a datastore)
        public override DataPairList GetSettings()
        {
            var answer = base.GetSettings();

            answer.Add("File Name", ShortFileName());
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
            int offset = LoadSettingsOffset(settings);

            FileName = settings[offset++];
            AvgSpeedMps = StringToFloat(settings[offset++]);
            MinDemM = StringToFloat(settings[offset++]);
            MaxDemM = StringToFloat(settings[offset++]);
            MinDsmM = StringToFloat(settings[offset++]);
            MaxDsmM = StringToFloat(settings[offset++]);
            OnGroundAtFixStartM = StringToFloat(settings[offset++]);
            OnGroundAtFixEndM = StringToFloat(settings[offset++]);
        }
    }

}
