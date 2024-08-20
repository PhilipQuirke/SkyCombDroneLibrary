// Copyright SkyComb Limited 2024. All rights reserved.
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
    public class DroneDataStore : BaseDataStore
    {
        // These are the physical files that are referenced in the DataStore
        public string ThermalVideoName { get; set; } = "";
        public string OpticalVideoName { get; set; } = "";
        public string ThermalFlightName { get; set; } = "";
        public string OpticalFlightName { get; set; } = "";
        public string OutputVideoName { get; set; } = "";


        // Open an existing DataStore & load Files tab settings
        public DroneDataStore(ExcelPackage store, string fileName) : base(store, fileName)
        {
            SelectWorksheet(FilesTabName);
            LoadSettings(GetColumnSettings(3, LhsColOffset, LhsColOffset + LabelToValueCellOffset));
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

            if (Worksheet == null)
                return;

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
                if (index.Key != "")
                {
                    if ((index.Key == DemTabName) || 
                        (index.Key == DsmTabName) ||
                        (index.Key == SwatheTabName) ||
                        (index.Key == Sections1TabName) ||
                        (index.Key == Sections2TabName))
                        cell.Value = index.Key + " (Hidden)";
                    else
                    {
                        cell.Value = index.Key;
                        SetInternalHyperLink(cell, index.Key);
                    }

                    Worksheet.Cells[row, 2].Value = index.Value;
                }

                row++;
            }


            // Help resources
            row += 2;
            SetTitle(ref row, 1, "Help resources");
            Worksheet.Cells[row, 1].Value = "Introduction and index";
            Worksheet.Cells[row, 2].Value = @"https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/README.md";
            SetExternalHyperLink( Worksheet.Cells[row, 2], @"https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/README.md");
            row++;
            Worksheet.Cells[row, 1].Value = "DataStore-specific help";
            Worksheet.Cells[row, 2].Value = @"https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/DataStore.md";
            SetExternalHyperLink(Worksheet.Cells[row, 2], @"https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/DataStore.md");

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


        // Create a DataStore on disk & store the Files settings.
        public DroneDataStore(string selectedFileName, string thermalVideoName, string opticalVideoName, string thermalFlightName, string opticalFlightName, string outputVideoName) : base(selectedFileName)
        {
            ThermalVideoName = thermalVideoName;
            OpticalVideoName = opticalVideoName;
            ThermalFlightName = thermalFlightName;
            OpticalFlightName = opticalFlightName;
            OutputVideoName = outputVideoName;

            SetWorkbookAnalystProperties();
            SaveIndex();
            SaveFiles();

            Store.SaveAs(DataStoreFileName);
            FreeResources();
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


        public (ExcelWorksheet?, int) EndRow(string dataTabName)
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
        public (ExcelWorksheet? pivotWs, ExcelWorksheet? dataWs, int) PreparePivotArea(string pivotTabName, string pivotName, string dataTabName)
        {
            (var dataWs, int lastDataRow) = EndRow(dataTabName);

            (_, var pivotWs) = SelectOrAddWorksheet(pivotTabName);
            if((pivotWs != null) && (pivotWs.PivotTables[pivotName] != null))
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


        public void SetAxises(ExcelChart chart, string xformat = "0", string yformat = "0")
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


        public void SetAxises(ExcelChart chart, string xTitle, string yTitle, string xformat = "0", string yformat = "0")
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
            if ((threshold > 0) && (maxRow > 0) && (Worksheet != null))
            {
                string columnChar = DroneDataStore.ColumnIndexToChar(column).ToString();
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
            if ((threshold > 0) && (maxRow > 0) && (Worksheet != null))
            {
                string columnChar = DroneDataStore.ColumnIndexToChar(column).ToString();
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
        public DroneDataStore Data { get; }


        public DataStoreAccessor(DroneDataStore data)
        {
            Data = data;
        }


        public void Save()
        {
            Data.SelectWorksheet(IndexTabName);
            if (Data.Worksheet != null)
                Data.Worksheet.View.SetTabSelected();
            Data.Save();
        }
    }
}
