// Copyright SkyComb Limited 2025. All rights reserved. 
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;


namespace SkyCombDrone.PersistModel
{
    // Given a single video file name, find out whether there is a video and/or a flight log file.
    // Find the existing DataStore or create a DataStore.
    // Return key information needed to instantiate input, model & run objects 
    public class DataStoreFactory
    {
        public const string DataStoreSuffix = "_SkyComb.xlsx";


        // Find name of video file that exists
        public static string FindVideoFileName(string fileName)
        {
            var answer = BaseDataStore.SwapFileNameExtension(fileName, ".mp4");
            if (System.IO.File.Exists(answer))
                return answer;

            answer = BaseDataStore.SwapFileNameExtension(fileName, ".avi");
            if (System.IO.File.Exists(answer))
                return answer;

            answer = BaseDataStore.SwapFileNameExtension(fileName, ".ts");
            if (System.IO.File.Exists(answer))
                return answer;

            return "";
        }


        // See if there is an SRT file with the same name as the video file, just a different extension.
        // Alternatively, we accept a M4T*.CSV file in the same directory
        public static string FindFlightLogFileName(string videoFileName)
        {
            var fileName = BaseDataStore.SwapFileNameExtension(videoFileName, ".csv");
            if (System.IO.File.Exists(fileName))
                return fileName;

            string? videoDir = null;
            try { videoDir = Path.GetDirectoryName(videoFileName); } catch { }
            string[] m4tFiles = Array.Empty<string>();
            if (!string.IsNullOrEmpty(videoDir) && Directory.Exists(videoDir))
                m4tFiles = Directory.GetFiles(videoDir, "M4T*.CSV", SearchOption.TopDirectoryOnly);
            else
                m4tFiles = Directory.GetFiles(".", "M4T*.CSV", SearchOption.TopDirectoryOnly);
            if (m4tFiles.Length > 0)
                fileName = m4tFiles[0]; // Use the first matching file

            if (System.IO.File.Exists(fileName))
                return fileName;

            return "";
        }


        // Calculate the names of the input files we have available.
        public static (string, string) FindInputFileNames(string firstFileName)
        {
            string thermalVideoName = "", thermalFlightName = "";

            try
            {
                // Without a video we can't do anything
                thermalVideoName = FindVideoFileName(firstFileName);
                if (thermalVideoName != "")
                    thermalFlightName = FindFlightLogFileName(thermalVideoName);
            }
            catch (Exception ex)
            {
                // Write the error message to the console
                Console.WriteLine("DataStoreFactory.LocateInputFiles_TwoVideos: " + ex.Message);

                thermalVideoName = "";
                thermalFlightName = "";
            }

            return (thermalVideoName, thermalFlightName);
        }


        public static string DataStoreName(string inputDirectory, string inputFileName, string outputElseInputDirectory)
        {
            if (inputFileName == "")
            {
                // Base datastore name on the inputDirectory last folder name
                inputDirectory = inputDirectory.Trim('\\');
                var index = inputDirectory.LastIndexOf("\\");
                var lastFolderName = inputDirectory.Substring(index + 1);
                return outputElseInputDirectory + "\\" + lastFolderName + DataStoreSuffix;
            }
            else
                return outputElseInputDirectory + "\\" + VideoModel.ShortFolderFileName(inputFileName) + DataStoreSuffix;
        }


        private static DroneDataStore? OpenOrCreateDataStore(
            string dataStoreName,
            string thermalFolderName,
            string thermalVideoName,
            string thermalFlightName,
            string outputElseInputDirectory,
            string outputVideoName,
            bool canCreate)
        {
            DroneDataStore? answer = null;

            try
            {
                if (dataStoreName != "")
                {
                    if (System.IO.File.Exists(dataStoreName))
                    {
                        // Open the existing datastore. Will fail if the user has the datastore open for editing.
                        answer = new DroneDataStore(dataStoreName);
                        answer.Open();
                        if (answer.IsOpen && answer.SelectWorksheet(DroneDataStore.FileSettingsTabName))
                        {
                            var cell = answer.Worksheet.Cells[1, 1];
                            if ((cell != null) && (cell.Value != null) && (cell.Value.ToString() == DroneDataStore.PrefixTitle))
                            {
                                // Spreadsheet may have ben copied from say C: to D:
                                // Reset the file names to the new locations
                                answer.InputFolderName = thermalFolderName;
                                answer.ThermalVideoName = thermalVideoName;
                                answer.ThermalFlightName = thermalFlightName;
                            }
                        }
                        else
                            throw BaseConstants.ThrowException("DataStoreFactory.OpenOrCreateDataStore: Failed to open existing DataStore " + dataStoreName);
                    }
                    else if (canCreate)
                        // Create a new datastore.
                        // One failure mode is if the outputElseInputDirectory does not exist.                        
                        answer = new(dataStoreName,
                            thermalFolderName,
                            thermalVideoName,
                            thermalFlightName,
                            outputVideoName);
                }
            }
            catch
            {
                answer = null;
            }

            return answer;
        }


        // From a video file name, locate all applicable input files, ensure that a DataStore exists. 
        // Handles case where there is both a thermal and an optical video file.
        public static DroneDataStore? OpenOrCreate(
            string inputDirectory,
            string inputFileName,
            string outputElseInputDirectory,
            bool doCreate,
            bool inputIsVideo)
        {
            var thermalFolderName = inputDirectory.Trim('\\');
            var thermalVideoName = "";
            var thermalFlightName = "";
            var dataStoreName = "";
            var outputVideoName = "";

            if (inputIsVideo)
            {
                if (inputFileName == "")
                    // Set dataStoreName prefix to the last foldername in the input directory
                    dataStoreName = DataStoreName(inputDirectory, "", outputElseInputDirectory);
                else
                {
                    // Base datastore name on the single video file and associated flight log file
                    (thermalVideoName, thermalFlightName) = FindInputFileNames(inputFileName);

                    if (thermalVideoName != "")
                        dataStoreName = DataStoreName(inputDirectory, BaseDataStore.RemoveFileNameSuffix(thermalVideoName), outputElseInputDirectory);

                    outputVideoName = VideoData.OutputVideoFileName(thermalVideoName, outputElseInputDirectory);
                }
            }
            else
            {
                //(string folder, string file) = BaseDataStore. SplitFileName(inputFileName);
                dataStoreName = DataStoreName(inputDirectory, inputFileName, outputElseInputDirectory);
            }

            return DataStoreFactory.OpenOrCreateDataStore(
                    dataStoreName,
                    thermalFolderName,
                    thermalVideoName,
                    thermalFlightName,
                    outputElseInputDirectory,
                    outputVideoName,
                    doCreate);
        }
    }
}
