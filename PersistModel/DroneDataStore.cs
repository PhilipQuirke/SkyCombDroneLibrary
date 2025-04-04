// Copyright SkyComb Limited 2025. All rights reserved.
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Style;
using SkyCombDrone.CommonSpace;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;
using System.Drawing;


namespace SkyCombDrone.PersistModel
{

    public class ImageDataStore : BaseDataStore
    {
        public ImageDataStore(string fileName, bool create) : base(fileName, false)
        {
        }


        // Save the bitmap to the datastore
        public void SaveBitmap(Bitmap? theBitmap, string chartName, int row, int col = 0, int percent = 100)
        {
            if (theBitmap == null || Worksheet == null)
                return;

            var imageHandler = new ExcelImageHandler(Worksheet);
            imageHandler.SaveBitmap(theBitmap, chartName, row, col, percent);
        }
    }


    // The SkyComb app creates a datastore (spreadsheet) per video and flight log file.
    // The datastore stores all settings & findings acts like a database or "no-sql document store".
    public class DroneDataStore : ImageDataStore
    {
        // These are the physical files that are referenced in the DataStore
        public string ThermalVideoName { get; set; } = "";
        public string ThermalFlightName { get; set; } = "";
        public string OutputVideoName { get; set; } = "";


        // Open an existing DataStore & load Files tab settings
        public DroneDataStore(string fileName) : base(fileName, false)
        {
            SelectWorksheet(FileSettingsTabName);
            LoadSettings(GetColumnSettings(3, LhsColOffset, LhsColOffset + LabelToValueCellOffset));
        }


        // Save the Index tab
        public void SaveIndex()
        {
            AddWorksheet(HomeTabName);

            SetLargeTitle(IndexTitle);

            SetColumnWidth(1, 10);
            SetColumnWidth(2, 55);
            SetColumnWidth(3, 20);
            SetColumnWidth(4, 20);

            // Create an index of tab names and purposes 
            if (Worksheet == null)
                return;

            var indexData = GetIndex();
            int row = IndexContentRow;
            foreach ((string title, string description, bool do_internal_link, string external_link) in indexData)
            {
                var area_cell = Worksheet.Cells[row, 1];
                var desc_cell = Worksheet.Cells[row, 2];
                var link_cell = Worksheet.Cells[row, 3];
                var title_cell = Worksheet.Cells[row, 4];

                bool do_external = (external_link != "");

                title_cell.Value = title;

                if ((title == "") && !(do_external || do_internal_link))
                {
                    area_cell.Style.Font.Bold = (title == "") && !(do_external || do_internal_link);
                    area_cell.Style.Font.Size += 2;
                    area_cell.Value = description;
                }
                else
                    desc_cell.Value = description;

                link_cell.Value = (do_external ? "Help" : (do_internal_link ? "Link" : (title != "" ? "Hidden" : "")));
                if (do_external)
                    SetExternalHyperLink(link_cell, external_link);
                else if (do_internal_link)
                    SetInternalHyperLink(link_cell, title);
 
                row++;
            }
        }


        // Save the settings to the Files tab
        public void SaveFileSettings()
        {
            SelectOrAddWorksheet(FileSettingsTabName);

            SetLargeTitle(FilesTitle);

            int row = 3;
            SetDataListColumn(ref row, 1, GetSettings());

            SetColumnWidth(1, 25);
        }


        // Create a DataStore on disk & store the Files settings.
        public DroneDataStore(string selectedFileName, string thermalVideoName, string thermalFlightName, string outputVideoName) : base(selectedFileName, true)
        {
            ThermalVideoName = thermalVideoName;
            ThermalFlightName = thermalFlightName;
            OutputVideoName = outputVideoName;

            SetWorkbookAnalystProperties();
            SaveIndex();

            // Add worksheets for ensure desired tab ordering 
            AddWorksheet(AnimalReportTabName);
            AddWorksheet(DroneReportTabName);
            AddWorksheet(GroundReportTabName);
            AddWorksheet(FileSettingsTabName);
            AddWorksheet(DroneSettingsTabName);
            AddWorksheet(ProcessSettingsTabName);

            SaveFileSettings();

            Store.SaveAs(DataStoreFileName);
            FreeResources();
        }


        // Open the existing datastore 
        public override void Open()
        {
            base.Open();

            SelectWorksheet(FileSettingsTabName);
        }


        // Get the object's settings as datapairs (e.g. for saving to a datastore)
        // We don't want to store blank values as this stops the settings from being read back from the datastore.
        public DataPairList GetSettings()
        {
            return new DataPairList
            {
                { "ThermalVideoName", ( ThermalVideoName == "" ? UnknownString : ThermalVideoName ) },
                { "ThermalFlightName", ( ThermalFlightName == "" ? UnknownString : ThermalFlightName ) },
                { "DataStoreFileName", DataStoreFileName },
                { "OutputVideoName", OutputVideoName },
            };
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        private void LoadSettings(List<string> settings)
        {
            if (settings == null)
                return;

            ThermalVideoName = settings[0];
            ThermalFlightName = settings[1];
            // DataStoreFileName = settings[2]

            if (ThermalVideoName.ToLower() == UnknownString.ToLower())
                ThermalVideoName = "";
            if (ThermalFlightName.ToLower() == UnknownString.ToLower())
                ThermalFlightName = "";
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


        public void FormatSummaryPage( int dataWidth1 = 15, int dataWidth2 = 15, int dataWidth3 = 15)
        {
            SetColumnWidth(LhsColOffset, 25);
            SetColumnWidth(LhsColOffset + LabelToValueCellOffset, dataWidth1);
            SetColumnWidth(MidColOffset, 25);
            SetColumnWidth(MidColOffset + LabelToValueCellOffset, dataWidth2);
            SetColumnWidth(RhsColOffset, 25);
            SetColumnWidth(RhsColOffset + LabelToValueCellOffset, dataWidth3);

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
            Data.SelectWorksheet(HomeTabName);
            if (Data.Worksheet != null)
                Data.Worksheet.View.SetTabSelected();
            Data.Save();
        }
    }
}
