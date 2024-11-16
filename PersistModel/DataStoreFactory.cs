using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;


namespace SkyCombDrone.PersistModel
{
    // Given a single (thermal or optical) video file name, find out whether there is one or two-paired videos, and any flight files.
    // Find the existing DataStore or create a DataStore.
    // Return key information needed to instantiate input, model & run objects 
    public class DataStoreFactory
    {
        public const string DataStoreSuffix = "_SkyComb.xlsx";


        // Find name of video file that exists
        public static string FindVideo(string fileName)
        {
            var answer = BaseDataStore.SwapFileNameExtension(fileName, ".mp4");
            if (!System.IO.File.Exists(answer))
            {
                answer = BaseDataStore.SwapFileNameExtension(fileName, ".avi");
                if (!System.IO.File.Exists(answer))
                {
                    answer = "";
                }
            }
            return answer;
        }


        // Calculate the names of the input files we have available.
        public static (string, string) LocateInputFiles_TwoVideos(string firstFileName)
        {
            string thermalVideoName = "", thermalFlightName = "";

            try
            {
                // Without a video we can't do anything
                thermalVideoName = FindVideo(firstFileName);
                if (thermalVideoName != "")
                {
                    // See if there is an SRT file with the same name as the video file, just a different extension
                    thermalFlightName = BaseDataStore.SwapFileNameExtension(thermalVideoName, ".SRT");
                    if (!System.IO.File.Exists(thermalFlightName))
                        thermalFlightName = "";
                }
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


        // Calculate the names of the input files we have available.
        public static (string videoName, string flightName) LocateInputFiles_OneVideo(string theFileName)
        {
            string answerVideoName = "", answerFlightName = "";

            try
            {
                // Without at least one video we can't do anything
                var theVideoName = FindVideo(theFileName);
                if (theVideoName.Length > 0)
                {
                    answerVideoName = theVideoName;
                    if (answerVideoName != "")
                    {
                        // See if there is an SRT file with the same name as the video file, just a different extension
                        answerFlightName = BaseDataStore.SwapFileNameExtension(answerVideoName, ".SRT");
                        if (!System.IO.File.Exists(answerFlightName))
                            answerFlightName = "";
                    }
                }
            }
            catch (Exception ex)
            {
                // Write the error message to the console
                Console.WriteLine("DataStoreFactory.LocateInputFiles_OneVideo: " + ex.Message);

                answerVideoName = "";
                answerFlightName = "";
            }

            return (answerVideoName, answerFlightName);
        }


        public static string DataStoreName(string inputFileName, string outputElseInputDirectory)
        {
            return BaseDataStore.AddFileNameSuffix(outputElseInputDirectory + "\\" + VideoModel.ShortFileName(inputFileName), DataStoreSuffix);
        }


        private static DroneDataStore? OpenOrCreateDataStore(
            string dataStoreName,
            string thermalVideoName, 
            string thermalFlightName, 
            string outputElseInputDirectory,
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
                            thermalVideoName, 
                            thermalFlightName, 
                            VideoData.OutputVideoFileName(thermalVideoName, outputElseInputDirectory));
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
        public static DroneDataStore? OpenOrCreate_TwoVideos(
            string videoFileName,
            string outputElseInputDirectory,
            bool canCreate)
        {
            (var thermalVideoName, var thermalFlightName) =
                LocateInputFiles_TwoVideos(videoFileName);

            var dataStoreName = "";
            if (thermalVideoName != "")
                dataStoreName = DataStoreName(thermalVideoName, outputElseInputDirectory);

            return DataStoreFactory.OpenOrCreateDataStore(
                dataStoreName,
                thermalVideoName, 
                thermalFlightName, 
                outputElseInputDirectory,
                canCreate);
        }


        // From a video file name, locate all applicable input files, ensure that a DataStore exists. 
        public static DroneDataStore? OpenOrCreate_OneVideo(
            string videoFileName,
            string outputElseInputDirectory,
            bool canCreate)
        {
            (var thermalVideoName, var thermalFlightName) =
                LocateInputFiles_OneVideo(videoFileName);

            var dataStoreName = "";
            if (thermalVideoName != "")
                dataStoreName = DataStoreName(thermalVideoName, outputElseInputDirectory);

            return DataStoreFactory.OpenOrCreateDataStore(
                dataStoreName,
                thermalVideoName, 
                thermalFlightName, 
                outputElseInputDirectory,
                canCreate);
        }
    }
}
