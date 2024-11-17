// Copyright SkyComb Limited 2024. All rights reserved.
using SkyCombGround.CommonSpace;


namespace SkyCombDrone.CommonSpace
{
    // The SkyComb app creates a datastore (spreadsheet) per set of (1 or 2) videos and (0 to 2) srt files.
    // The datastore stores all settings & findings acts like a database or "no-sql document store".
    public class DataConstants : BaseConstants
    {
        // Titles
        public const string DroneSummaryTitle = "Drone Summary";
        public const string VideoInputTitleSuffix = ": Video Input";
        public const string FlightSectionTitleSuffix = ": Flight Section";
        public const string FlightStepTitle = "Prime : Flight Step";
        public const string UserInputTitle = "User Input:";
        public const string LegTitle = "Leg Config:";
        public const string ProcessSummaryTitle = "Process Summary";
        public const string ProcessConfigTitle = "Process Config:";
        public const string RunConfigTitle = "Run Config:";
        public const string ResultsTitle = "Results:";
        public const string EffortTitle = "Effort:";
        public const string OutputConfigTitle = "Output Config:";
        public const string VideoTitle = "Video Input:";
        public const string ModelFlightStepSummaryTitle = "Flight Step Summary:";
        public const string ObjectSummaryTitle = "Objects Summary:";
        public const string PopulationSummaryTitle = "Population Summary";


        // Rows
        public const int ModelTitleRow = 3;
        public const int RunTitleRow = 3;
        public const int ResultsTitleRow = 9;
        public const int EffortTitleRow = 18;
        public const int OutputTitleRow = 24;
    }
}
