// Copyright SkyComb Limited 2023. All rights reserved.
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Style;
using OfficeOpenXml.Table.PivotTable;
using SkyCombDrone.CommonSpace;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;
using System.Drawing;
using System.Xml;


namespace SkyCombDrone.PersistModel
{

    // The SkyComb app creates a datastore (spreadsheet) per set of (1 or 2) videos and (0 to 2) srt files.
    // The datastore stores all settings & findings acts like a database or "no-sql document store".
    public class DataStore : GenericDataStore
    {
        // The current version of the code base.
        public static string CodeVersion = "6.0";


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
        //public const string IndexTabName = "Index";
        //public const string FilesTabName = "Files";
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


        // Columns
        public const int LhsColOffset = 1;
        public const int MidColOffset = 4;
        public const int RhsColOffset = 7;
        public const int FarRhsColOffset = 10;


        // These are the physical files that are referenced in the DataStore
        public string ThermalVideoName { get; set; } = "";
        public string OpticalVideoName { get; set; } = "";
        public string ThermalFlightName { get; set; } = "";
        public string OpticalFlightName { get; set; } = "";
        public string OutputVideoName { get; set; } = "";


        // Open an existing DataStore & load Files tab settings
        public DataStore(ExcelPackage store, string fileName) : base(store, fileName)
        {
            SelectWorksheet(FilesTabName);
            LoadSettings(GetColumnSettings(3, LhsColOffset, LhsColOffset + LabelToValueCellOffset));
        }


        // Set the workbook properties
        public void SetWorkbookProperties()
        {
            Store.Workbook.Properties.Title = "SkyComb Analyst Output";
            Store.Workbook.Properties.Author = "SkyComb Limited";
            Store.Workbook.Properties.Comments = "Generated by the SkyComb Analyst tool.";
        }


        // Get the Index tab contents
        public DataPairList GetIndex()
        {
            return new DataPairList
                {
                    { IndexTabName, "This tab" },
                    { FilesTabName, "List of drone input files and output files created" },
                    { "", "" },
                    { DroneTabName, "Summary drone and elevation data" },
                    { Sections1TabName, "Raw drone flight log data table" },
                    { Sections2TabName, "Raw drone flight log graphs" },
                    { DemTabName, "Ground elevation data table, pivot and graph" },
                    { DsmTabName, "Surface (aka tree-top) elevation data table, pivot and graph" },
                    { Steps1TabName, "Smoothed drone flight log data table" },
                    { Steps2TabName, "Smoothed drone flight log graphs" },
                    { Legs1TabName, "Drone flight legs data table" },
                    { "", "" },
                    { ProcessTabName, "Summary image processing and object data"},
                    { Blocks1TabName, "Processing blocks (aka video frames) data table - combines Step, DEM, DSM, Leg & image data" },
                    { Blocks2TabName, "Processing blocks (aka video frames) graphs" },
                    { FeaturesTabName, "Feature (cluster of hot pixels in one video frame) data table" },
                    { Objects1TabName, "Object (sequence of features across multiple video frames) data table" },
                    { Objects2TabName, "Object graphs - combines object, feature & block data" },
                    { Legs2TabName, "Legs (in the blocks scope) data table - refines the Drone flight leg's altitude" },
                    { "", "" },
                    { ObjectCategoryTabName, "Master (valid) category data table" },
                    { CategoryTabName, "Object category (annotations) data table" },
                    { PopulationTabName, "Categorised object population Graphs" },
                };
        }


        // Save the Index tab
        public void SaveIndex()
        {
            AddWorksheet(IndexTabName);

            SetTitles("");

            SetColumnWidth(1, 25);
            SetColumnWidth(2, 90);
            SetColumnWidth(3, 22);
            SetColumnWidth(4, 12);

            // Create an index of tab names and purposes 
            int row = 3;
            SetTitle(ref row, 1, "Index of tabs");

            Worksheet.Cells[row, 1].Value = "Tab";
            Worksheet.Cells[row, 2].Value = "Purpose";
            Worksheet.Cells[row, 3].Value = "Last updated";
            Worksheet.Cells[row, 4].Value = "Using version";
            for (int col = 1; col <= 4; col++)
                Worksheet.Cells[row, col].Style.Font.Bold = true;

            var indexData = GetIndex();
            row = IndexContentRow;
            foreach (var index in indexData)
            {
                var cell = Worksheet.Cells[row, 1];
                cell.Value = index.Key;
                if (index.Key != "")
                {
                    cell.Hyperlink = new Uri("#'" + index.Key + "'!A1", UriKind.Relative);
                    cell.Style.Font.UnderLine = true;
                    cell.Style.Font.Color.SetColor(Color.Blue);
                }

                Worksheet.Cells[row, 2].Value = index.Value;

                row++;
            }


            // Help resources
            row += 2;
            SetTitle(ref row, 1, "Help resources");
            Worksheet.Cells[row, 1].Value = "Introduction and index";
            Worksheet.Cells[row, 2].Value = @"https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/README.md";
            Worksheet.Cells[row, 2].Hyperlink = new Uri(@"https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/README.md", UriKind.Absolute);
            Worksheet.Cells[row, 2].Style.Font.UnderLine = true;
            Worksheet.Cells[row, 2].Style.Font.Color.SetColor(Color.Blue);
            row++;
            Worksheet.Cells[row, 1].Value = "DataStore-specific help";
            Worksheet.Cells[row, 2].Value = @"https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/DataStore.md";
            Worksheet.Cells[row, 2].Hyperlink = new Uri(@"https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/DataStore.md", UriKind.Absolute);
            Worksheet.Cells[row, 2].Style.Font.UnderLine = true;
            Worksheet.Cells[row, 2].Style.Font.Color.SetColor(Color.Blue);

            // Copyright
            row += 3;
            Worksheet.Cells[row, 2].Value = "Copyright 2023 SkyComb Limited. All rights reserved.";


            SetLastUpdateDateTime(IndexTabName);
        }


        // Save the settings to the Files tab
        public void SaveFiles()
        {
            AddWorksheet(FilesTabName);

            SetTitles(FilesTitle);

            int row = 3;
            SetDataListColumn(ref row, 1, GetSettings());

            SetColumnWidth(1, 25);

            SetLastUpdateDateTime(FilesTabName);
        }


        // For the specified tabName, save when tab was updated and using what code version
        public void SetLastUpdateDateTime(string tabName)
        {
            if (SelectWorksheet(IndexTabName))
            {
                var indexData = GetIndex();
                int row = IndexContentRow;
                foreach (var index in indexData)
                {
                    if (index.Key == tabName)
                    {
                        Worksheet.Cells[row, 3].Value = DateTime.Now.ToString();

                        // Store the code version that stored the changed data. 
                        // Helps if say the video was processed with an old version of the code,
                        // but the object categories were assigned using a new version of the code.
                        Worksheet.Cells[row, 4].Value = CodeVersion;
                        break;
                    }

                    row++;
                }
            }
        }


        // Set and save the current date time in the Files tab
        public void SaveLastUpdateDateTime(string tabName)
        {
            Open();
            SetLastUpdateDateTime(tabName);
            Store.Save();
            Close();
        }


        // Create a DataStore on disk & store the Files settings.
        public DataStore(string selectedFileName, string thermalVideoName, string opticalVideoName, string thermalFlightName, string opticalFlightName, string outputVideoName) : base(selectedFileName)
        {
            ThermalVideoName = thermalVideoName;
            OpticalVideoName = opticalVideoName;
            ThermalFlightName = thermalFlightName;
            OpticalFlightName = opticalFlightName;
            OutputVideoName = outputVideoName;

            SetWorkbookProperties();
            SaveIndex();
            SaveFiles();

            Store.SaveAs(DataStoreFileName);
            Close();
        }


        // Open the existing datastore 
        public override void Open()
        {
            base.Open();

            SelectWorksheet(FilesTabName);
        }


        // Get the object's settings as datapairs (e.g. for saving to a datastore)
        // We don't want to store blank values as this stops the settings from being read back from the datastore.
        public DataPairList GetSettings()
        {
            return new DataPairList
            {
                // Input files
                { "ThermalVideoName", ( ThermalVideoName == "" ? UnknownString : ThermalVideoName ) },
                { "OpticalVideoName", ( OpticalVideoName == "" ? UnknownString : OpticalVideoName ) },
                { "ThermalFlightName", ( ThermalFlightName == "" ? UnknownString : ThermalFlightName ) },
                { "OpticalFlightName", ( OpticalFlightName == "" ? UnknownString : OpticalFlightName ) },
                
                // Output files
                { "DataStoreFileName", DataStoreFileName },
                { "OutputVideoName", OutputVideoName },
            };
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        private void LoadSettings(List<string> settings)
        {
            ThermalVideoName = settings[0];
            OpticalVideoName = settings[1];
            ThermalFlightName = settings[2];
            OpticalFlightName = settings[3];
            // DataStoreFileName = settings[4]

            if (ThermalVideoName.ToLower() == UnknownString.ToLower())
                ThermalVideoName = "";
            if (OpticalVideoName.ToLower() == UnknownString.ToLower())
                OpticalVideoName = "";
            if (ThermalFlightName.ToLower() == UnknownString.ToLower())
                ThermalFlightName = "";
            if (OpticalFlightName.ToLower() == UnknownString.ToLower())
                OpticalFlightName = "";
        }


        // Delta numeric string like DJI_0046.mp4 to give DJI_0045.mp4
        // On a file name like C:\\Data_Input\\TestVideo.mp4 will return "".
        public static string DeltaNumericString(string input, int delta)
        {
            var startPos = input.LastIndexOf("_");

            int newValue = 0;
            try
            {
                var numericString = input.Substring(startPos + 1, 4);
                newValue = StringToInt(numericString) + delta;
            }
            catch
            {
                return "";
            }

            return input.Substring(0, startPos + 1) + newValue.ToString("D4") + input.Substring(startPos + 5);
        }


        public void SetTitle(ref int row, int col, string title, int fontsize = MediumTitleFontSize)
        {
            var cell = Worksheet.Cells[row, col];
            cell.Value = title;
            cell.Style.Font.Bold = true;
            cell.Style.Font.Size = fontsize;
            cell.Style.Font.Color.SetColor(fontsize == MediumTitleFontSize ? Color.DarkBlue : Color.Blue);
            row++;
        }


        public void SetTitles(string title3)
        {
            int row = 1;
            SetTitle(ref row, 1, Main1Title, LargeTitleFontSize);
            row = 1;
            SetTitle(ref row, 2, Main2Title, LargeTitleFontSize);
            row = 1;
            SetTitle(ref row, 4, title3, LargeTitleFontSize);
        }


        public void SetCellLabelAndStr(ref int row, int baseCol, string label, string value)
        {
            Worksheet.Cells[row, baseCol].Value = label;
            Worksheet.Cells[row, baseCol + LabelToValueCellOffset].Value = value;
            row++;
        }


        public void SetCellLabelAndInt(ref int row, int baseCol, string label, int value)
        {
            Worksheet.Cells[row, baseCol].Value = label;
            Worksheet.Cells[row, baseCol + LabelToValueCellOffset].Value = value;
            row++;
        }


        public void SetCellLabelAndInt(ref int row, string label, int value)
        {
            SetCellLabelAndInt(ref row, LhsColOffset, label, value);
        }


        public void SetDataListColumn(ref int row, int col, DataPairList list, bool showUnknown = true, int extraColOffset = 0)
        {
            if (list == null)
                return;

            foreach (var pair in list)
            {
                bool isUnknown = (pair.Ndp >= 0) && (pair.Value == UnknownValue.ToString());
                if (isUnknown && !showUnknown)
                    continue;

                Worksheet.Cells[row, col].Value = pair.Key;
                if (isUnknown)
                    Worksheet.Cells[row, col + LabelToValueCellOffset + extraColOffset].Value = UnknownString;
                else
                    SetDataPairValue(row, col + LabelToValueCellOffset + extraColOffset, pair);
                row++;
            }
        }


        public void SetTitleAndDataListColumn(String title, int firstRow, int col, DataPairList list, bool showUnknown = true, int extraColOffset = 0)
        {
            int row = firstRow;
            SetTitle(ref row, col, title);
            SetDataListColumn(ref row, col, list, showUnknown, extraColOffset);
        }


        public (ExcelWorksheet, int) EndRow(string dataTabName)
        {
            int lastDataRow = 0;
            var dataWs = ReferWorksheet(dataTabName);
            if ((dataWs != null) && (dataWs.Dimension != null) && (dataWs.Dimension.End != null))
                lastDataRow = dataWs.Dimension.End.Row;
            return (dataWs, lastDataRow);
        }


        // Prepare to add a chart on one tab referring to data from another tab.
        public (ExcelWorksheet, int) PrepareChartArea(string chartTabName, string chartName, string dataTabName)
        {
            (_, int lastDataRow) = EndRow(dataTabName);

            (_, var chartWs) = SelectOrAddWorksheet(chartTabName);
            if (chartWs.Drawings[chartName] != null)
                chartWs.Drawings.Remove(chartWs.Drawings[chartName]);
            Assert(chartWs.Drawings[chartName] == null, "PrepareChartArea: Bad logic");

            return (chartWs, lastDataRow);
        }


        // Prepare to add a pivot table on one tab referring to data from another tab.
        public (ExcelWorksheet pivotWs, ExcelWorksheet dataWs, int) PreparePivotArea(string pivotTabName, string pivotName, string dataTabName)
        {
            (var dataWs, int lastDataRow) = EndRow(dataTabName);

            (_, var pivotWs) = SelectOrAddWorksheet(pivotTabName);
            if (pivotWs.PivotTables[pivotName] != null)
                pivotWs.PivotTables.Delete(pivotName);

            return (pivotWs, dataWs, lastDataRow);
        }


        // Enable conditional formatting on a pivot table.
        public void AddConditionalFormattingToPivotTable(ExcelPivotTable pivotTable)
        {
            var worksheetXml = pivotTable.WorkSheet.WorksheetXml;
            var element = worksheetXml.GetElementsByTagName("conditionalFormatting")[0];
            ((XmlElement)element).SetAttribute("pivot", "1");
        }


        public static void SetChartTitle(ExcelChart chart, string title)
        {
            chart.Title.Text = title;
            chart.Title.Font.Size = MediumTitleFontSize;
            chart.Title.Font.Bold = true;
            chart.Title.Font.Color = Color.DarkBlue; // Re obsolete hint: Suggested "Fill" actually fills in the background not title text color. 
        }


        public static void SetChart(ExcelChart chart, string title, float rowOffset, int colOffset, int depth, int width = StandardChartCols)
        {
            SetChartTitle(chart, title);

            int startRow = (int)(rowOffset * StandardChartRows);
            chart.SetPosition(startRow, 0, colOffset * StandardChartCols, 0);
            chart.To.Column = colOffset * StandardChartCols + width;
            chart.To.Row = startRow + depth;
        }


        public static void SetAxises(ExcelChart chart, string xformat = "0", string yformat = "0")
        {
            chart.XAxis.MajorGridlines.Width = 1;
            chart.XAxis.MinorTickMark = eAxisTickMark.None;
            chart.XAxis.Format = xformat;
            // AxisPosition = eAxisPosition.Bottom;  

            chart.YAxis.MajorGridlines.Width = 1;
            chart.YAxis.MinorTickMark = eAxisTickMark.None;
            chart.YAxis.Format = yformat;
            // chart.YAxis.AxisPosition = eAxisPosition.Left;
        }


        public static void SetAxises(ExcelChart chart, string xTitle, string yTitle, string xformat = "0", string yformat = "0")
        {
            chart.XAxis.Title.Text = xTitle;
            chart.YAxis.Title.Text = yTitle;

            SetAxises(chart, xformat, yformat);
        }


        // Add a serie to a chart from a named tab
        public ExcelChartSerie AddSerie(ExcelChart chart, string dataTabName, string header, int dataCol, int indexCol)
        {
            ExcelChartSerie serie = null;

            var dataWS = ReferWorksheet(dataTabName);
            if ((dataWS != null) && (dataWS.Dimension != null) && (dataWS.Dimension.End != null))
            {
                var lastRow = dataWS.Dimension.End.Row;
                if (lastRow > 1)
                {
                    serie = chart.Series.Add(
                        dataWS.Cells[2, dataCol, lastRow, dataCol],
                        dataWS.Cells[2, indexCol, lastRow, indexCol]);
                    serie.Header = header;
                }
            }

            return serie;
        }


        public void AddScatterSerie(ExcelScatterChart chart, string dataTabName, string header, int dataCol, int indexCol, Color color, int markerSize = 4)
        {
            var serie = AddSerie(chart, dataTabName, header, dataCol, indexCol) as ExcelScatterChartSerie;
            if (serie == null)
                return;

            serie.Marker.Border.Fill.Color = color;
            serie.Marker.Style = eMarkerStyle.Circle;
            serie.Marker.Size = markerSize;
            serie.Marker.Fill.Color = color;
            serie.Fill.Color = color;
        }


        public void AddLineSerie(ExcelLineChart chart, string dataTabName, string header, int dataCol, int indexCol, Color color, int markerSize = 2 /* 2 to 72 */, int borderWidth = 0)
        {
            var serie = AddSerie(chart, dataTabName, header, dataCol, indexCol) as ExcelLineChartSerie;
            if (serie == null)
                return;

            serie.Marker.Fill.Color = color;
            serie.Marker.Style = eMarkerStyle.Circle;
            serie.Marker.Size = markerSize;
            serie.Fill.Color = color;
            serie.Border.Width = borderWidth;
        }


        // Highlight in red any cells in the current worksheet, where the column value exceeds the threshold. 
        public void AddConditionalRuleBad(int column /* one-based */, int maxRow, float threshold)
        {
            if ((threshold > 0) && (maxRow > 0))
            {
                string columnChar = DataStore.ColumnIndexToChar(column).ToString();
                string thresholdStr = threshold.ToString();
                var rule = Worksheet.ConditionalFormatting.AddExpression(
                    Worksheet.Cells[2, column, maxRow, column]);
                rule.Formula = string.Format("OR(${0}2<-{1},${2}2>{3})", columnChar, thresholdStr, columnChar, thresholdStr);
                rule.Style.Fill.PatternType = ExcelFillStyle.Solid;
                rule.Style.Fill.BackgroundColor.Color = BadValueColor;
            }
        }


        // Highlight in green any cells in the current worksheet, where the column value in below the threshold. 
        public void AddConditionalRuleGood(int column /* one-based */, int maxRow, float threshold)
        {
            if ((threshold > 0) && (maxRow > 0))
            {
                string columnChar = DataStore.ColumnIndexToChar(column).ToString();
                string thresholdStr = threshold.ToString();
                var rule = Worksheet.ConditionalFormatting.AddExpression(
                    Worksheet.Cells[2, column, maxRow, column]);
                rule.Formula = string.Format("AND(${0}2>-{1},${2}2<{3})", columnChar, thresholdStr, columnChar, thresholdStr);
                rule.Style.Fill.PatternType = ExcelFillStyle.Solid;
                rule.Style.Fill.BackgroundColor.Color = GoodValueColor;
            }
        }


        public void FormatSummaryPage()
        {
            SetColumnWidth(LhsColOffset, 25);
            SetColumnWidth(LhsColOffset + LabelToValueCellOffset, 30);
            SetColumnWidth(MidColOffset, 25);
            SetColumnWidth(MidColOffset + LabelToValueCellOffset, 30);
            SetColumnWidth(RhsColOffset, 25);
            SetColumnWidth(RhsColOffset + LabelToValueCellOffset, 30);

            Worksheet.Column(LhsColOffset + LabelToValueCellOffset).Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            Worksheet.Column(MidColOffset + LabelToValueCellOffset).Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            Worksheet.Column(RhsColOffset + LabelToValueCellOffset).Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

            // Column might not exist
            try
            {
                SetColumnWidth(FarRhsColOffset, 25);
                SetColumnWidth(FarRhsColOffset + LabelToValueCellOffset, 30);
                Worksheet.Column(FarRhsColOffset + LabelToValueCellOffset).Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            }
            catch { }
        }

    }


    public class DataStoreAccessor : DataConstants
    {
        public DataStore Data { get; }


        public DataStoreAccessor(DataStore data)
        {
            Data = data;
        }


        public void SaveAndClose()
        {
            Data.SelectWorksheet(IndexTabName);
            Data.Worksheet.View.SetTabSelected();
            Data.SaveAndClose();
        }
    }
}
