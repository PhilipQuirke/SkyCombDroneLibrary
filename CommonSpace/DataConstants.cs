// Copyright SkyComb Limited 2023. All rights reserved.
using SkyCombGround.CommonSpace;


namespace SkyCombDrone.CommonSpace
{
    // The SkyComb app creates a datastore (spreadsheet) per set of (1 or 2) videos and (0 to 2) srt files.
    // The datastore stores all settings & findings acts like a database or "no-sql document store".
    public class DataConstants : Constants
    {
        // Fonts
        public const int LargeTitleFontSize = 16;
        public const int MediumTitleFontSize = 14;


        // Titles
        public const string Main1Title = "SkyComb Analyst";
        public const string Main2Title = "DataStore";
        public const string IndexTitle = "Index";
        public const string FilesTitle = "Files";
        public const string DroneSummaryTitle = "Drone Summary";
        public const string VideoInputTitleSuffix = ": Video Input";
        public const string FlightSectionTitleSuffix = ": Flight Section";
        public const string FlightStepTitle = "Prime : Flight Step";
        public const string GroundInputTitle = "Ground Data";
        public const string UserInputTitle = "User Input:";
        public const string LegTitle = "Leg Config:";
        public const string ProcessSummaryTitle = "Process Summary";
        public const string ProcessConfigTitle = "Process Config:";
        public const string RunConfigTitle = "Run Config:";
        public const string ResultsTitle = "Results:";
        public const string EffortTitle = "Effort:";
        public const string DrawTitle = "DrawLines:";
        public const string OutputConfigTitle = "Output Config:";
        public const string VideoTitle = "Video Input:";
        public const string ModelFlightStepSummaryTitle = "Flight Step Summary:";
        public const string ObjectSummaryTitle = "Objects Summary:";
        public const string PopulationSummaryTitle = "Population Summary";


        // Tab Names
        public const string IndexTabName = "Index";
        public const string FilesTabName = "Files";
        public const string DroneTabName = "Drone";
        public const string Sections1TabName = "Sects1";
        public const string Sections2TabName = "Sects2";
        public const string DemTabName = "DEM";
        public const string DsmTabName = "DSM";
        public const string Steps1TabName = "Steps1";
        public const string Steps2TabName = "Steps2";
        public const string Legs1TabName = "Legs1";
        public const string ProcessTabName = "Process";
        public const string Blocks1TabName = "Blks1";
        public const string Blocks2TabName = "Blks2";
        public const string PixelsTabName = "Pxls";
        public const string FeaturesTabName = "Feats";
        public const string Objects1TabName = "Objs1";
        public const string Objects2TabName = "Objs2";
        public const string Legs2TabName = "Legs2";
        public const string CategoryTabName = "Cat1";
        public const string ObjectCategoryTabName = "Cat2";
        public const string PopulationTabName = "Popln";


        // Chart outline sizes
        public const int StandardChartCols = 13;
        public const int StandardChartRows = 15;
        public const int LargeChartRows = 2 * StandardChartRows;


        // Rows
        public const int Chapter1TitleRow = 3;
        public const int Chapter2TitleRow = 21;
        public const int Chapter3TitleRow = 38;
        public const int Chapter4TitleRow = 50;

        public const int ModelTitleRow = 3;
        public const int RunTitleRow = 3;
        public const int ResultsTitleRow = 9;
        public const int EffortTitleRow = 18;
        public const int OutputTitleRow = 24;
        public const int DrawTitleRow = 35;

        public const int IndexContentRow = 5;


        // Columns / Column offset
        public const int LhsColOffset = 1;
        public const int MidColOffset = 4;
        public const int RhsColOffset = 7;
        public const int FarRhsColOffset = 10;
        public const int LabelToValueCellOffset = 1;
    }
}
