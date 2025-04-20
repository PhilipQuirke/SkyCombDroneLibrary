// Copyright SkyComb Limited 2024. All rights reserved.
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
        public VelocityF InputImageUnitVector
        {
            get
            {
                return new VelocityF(
                    -(float)Math.Cos(YawRad + Math.PI / 2),
                    +(float)Math.Sin(YawRad + Math.PI / 2));
            }
        }



        // FIX INACCURATE DRONE DATA
        // All drone data feeds are inaccurate. In some cases we can calculate values to improve their accuracy.

        // FixAltM is a delta that improves the altitude reported by the drone.
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
            if (InputImageCenter != null)
                InputImageCenter.AssertGood();
            if (InputImageSizeM != null)
                InputImageSizeM.AssertGood();

            var answer = base.GetSettings();
            answer[0].Key = "Step";

            answer.Add("Leg Id", FlightLegId);
            answer.Add("Leg Name", FlightLegName);
            answer.Add("DSM", DsmM, ElevationNdp); // Graphs depend on this name (TBC)
            answer.Add("DEM", DemM, ElevationNdp); // Graphs depend on this name (TBC)
            answer.Add("Img Center", (InputImageCenter != null ? InputImageCenter.ToString() : "0,0"));
            answer.Add("Img Size M", (InputImageSizeM != null ? InputImageSizeM.ToString(2) : "0,0"));
            answer.Add("Has Leg", (FlightLegId > 0 ? 1 : 0));
            // We do not save FixValues

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
            // We do not load FixValues. It is updated by ProcessSpan on load

            InputImageCenter.AssertGood();
            InputImageSizeM.AssertGood();
        }


        // Add FlightStep settings to the "Block" settings for saving to Datastore to aid debugging/charting.
        public void AppendStepToBlockSettings(ref DataPairList answer)
        {
            answer.Add("Dsm M", DsmM, ElevationNdp);
            answer.Add("Dem M", DemM, ElevationNdp);
        }

    };


    // FlightStepsModel summarises a list of FlightSteps and other summary data
    public abstract class FlightStepsModel : FlightStepSummaryModel
    {
        // The average height of the drone above the DEM over these steps
        public float AvgHeightOverDemM { get; set; } = BaseConstants.UnknownValue;
        // The min height of the drone above the DSM over these steps
        public float MinHeightOverDsmM { get; set; } = BaseConstants.UnknownValue;


        public FlightStepsModel(List<string>? settings = null)
        {
            if (settings != null)
                LoadSettings(settings);
        }


        // Get this FlightSteps object's settings as datapairs (e.g. for saving to a datastore)
        public override DataPairList GetSettings()
        {
            var answer = base.GetSettings();

            answer.Add("Avg Speed Mps", AvgSpeedMps, 2);
            answer.Add("Min Dem M", MinDemM, ElevationNdp);
            answer.Add("Max Dem M", MaxDemM, ElevationNdp);
            answer.Add("Min Dsm M", MinDsmM, ElevationNdp);
            answer.Add("Max Dsm M", MaxDsmM, ElevationNdp);
            answer.Add("Avg Ht over Dem M", AvgHeightOverDemM, ElevationNdp);
            answer.Add("Min Ht over Dsm M", MinHeightOverDsmM, ElevationNdp);

            return answer;
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public override void LoadSettings(List<string> settings)
        {
            int offset = LoadSettingsOffset(settings);

            AvgSpeedMps = StringToFloat(settings[offset++]);
            MinDemM = StringToFloat(settings[offset++]);
            MaxDemM = StringToFloat(settings[offset++]);
            MinDsmM = StringToFloat(settings[offset++]);
            MaxDsmM = StringToFloat(settings[offset++]);
            AvgHeightOverDemM = StringToFloat(settings[offset++]);
            MinHeightOverDsmM = StringToFloat(settings[offset++]);
        }
    }

}
