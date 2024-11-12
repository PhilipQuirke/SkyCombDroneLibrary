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
        public static (string, string, string, string) LocateInputFiles_TwoVideos(
            Func<string, DateTime> readDateEncodedUtc,
            string firstFileName)
        {
            string thermalVideoName = "", opticalVideoName = "", thermalFlightName = "", opticalFlightName = "";

            VideoModel? secondVideoData = null;
            VideoModel? firstVideoData = null;

            try
            {
                // Without at least one video we can't do anything
                var firstVideoName = FindVideo(firstFileName);
                if (firstVideoName.Length > 0)
                {
                    firstVideoData = new VideoModel(firstVideoName, true, readDateEncodedUtc); // guess at thermal

                    // Do we have a second video (in a thermal/optical pair) that overlaps the first video timewise closely?
                    var secondVideoName = "";

                    // First guess + 1
                    secondVideoName = FindVideo(DroneDataStore.DeltaNumericString(firstVideoName, +1));
                    if (secondVideoName.Length > 0)
                    {
                        secondVideoData = new VideoModel(secondVideoName, true, readDateEncodedUtc); // guess at thermal

                        // Based on video DateEncodedUtc datetime and durationMs this is an accurate match mechanism
                        if (VideoModel.PercentOverlap(firstVideoData, secondVideoData) < 95)
                        {
                            secondVideoName = "";
                            secondVideoData.FreeResources();
                            secondVideoData = null;
                        }
                    }

                    // Second guess - 1
                    if (secondVideoName == "")
                    {
                        secondVideoName = FindVideo(DroneDataStore.DeltaNumericString(firstVideoName, -1));
                        if (secondVideoName.Length > 0)
                        {
                            secondVideoData = new VideoModel(secondVideoName, true, readDateEncodedUtc); // guess at thermal

                            // Based on video DateEncodedUtc datetime and durationMs this is an accurate match mechanism
                            if (VideoModel.PercentOverlap(firstVideoData, secondVideoData) < 95)
                            {
                                secondVideoName = "";
                                secondVideoData.FreeResources();
                                secondVideoData = null;
                            }
                        }
                    }

                    if (secondVideoName.Length > 0)
                    {
                        // We have two videos. Assume the one with more pixels is the optical video
                        if (firstVideoData.ImagePixels > secondVideoData.ImagePixels)
                        {
                            thermalVideoName = secondVideoName;
                            opticalVideoName = firstVideoName;
                        }
                        else
                        {
                            thermalVideoName = firstVideoName;
                            opticalVideoName = secondVideoName;
                        }
                    }
                    else
                    {
                        // We have one video.
                        // Given the purpose/focus of SkyComb Analyst, we assume it is thermal. 
                        thermalVideoName = firstVideoName;
                        opticalVideoName = "";
                    }


                    if (thermalVideoName != "")
                    {
                        // See if there is an SRT file with the same name as the video file, just a different extension
                        thermalFlightName = BaseDataStore.SwapFileNameExtension(thermalVideoName, ".SRT");
                        if (!System.IO.File.Exists(thermalFlightName))
                            thermalFlightName = "";
                    }

                    if (opticalVideoName != "")
                    {
                        // See if there is an SRT file with the same name as the video file, just a different extension
                        opticalFlightName = BaseDataStore.SwapFileNameExtension(opticalVideoName, ".SRT");
                        if (!System.IO.File.Exists(opticalFlightName))
                            opticalFlightName = "";
                    }
                }
            }
            catch (Exception ex)
            {
                // Write the error message to the console
                Console.WriteLine("DataStoreFactory.LocateInputFiles_TwoVideos: " + ex.Message);

                thermalVideoName = "";
                opticalVideoName = "";
                thermalFlightName = "";
                opticalFlightName = "";
            }
            finally
            {
                firstVideoData?.FreeResources();
                secondVideoData?.FreeResources();
            }

            return (thermalVideoName, opticalVideoName, thermalFlightName, opticalFlightName);
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
            string thermalVideoName, string opticalVideoName,
            string thermalFlightName, string opticalFlightName,
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
                            if ((cell != null) && (cell.Value != null) && (cell.Value.ToString() == DroneDataStore.Main1Title))
                            {
                                // Spreadsheet may have ben copied from say C: to D:
                                // Reset the file names to the new locations
                                answer.ThermalVideoName = thermalVideoName;
                                answer.ThermalFlightName = thermalFlightName;
                                answer.OpticalVideoName = opticalVideoName;
                                answer.OpticalFlightName = opticalFlightName;
                            }
                        }
                        else
                            throw BaseConstants.ThrowException("DataStoreFactory.OpenOrCreateDataStore: Failed to open existing DataStore " + dataStoreName);
                    }
                    else if (canCreate)
                        // Create a new datastore.
                        // One failure mode is if the outputElseInputDirectory does not exist.                        
                        answer = new(dataStoreName,
                            thermalVideoName, opticalVideoName,
                            thermalFlightName, opticalFlightName,
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
            Func<string, DateTime> readDateEncodedUtc,
            string videoFileName,
            string outputElseInputDirectory,
            bool canCreate)
        {
            (var thermalVideoName, var opticalVideoName, var thermalFlightName, var opticalFlightName) =
                LocateInputFiles_TwoVideos(readDateEncodedUtc, videoFileName);

            var dataStoreName = "";
            if (thermalVideoName != "")
                dataStoreName = DataStoreName(thermalVideoName, outputElseInputDirectory);
            else if (opticalVideoName != "")
                dataStoreName = DataStoreName(opticalVideoName, outputElseInputDirectory);

            return DataStoreFactory.OpenOrCreateDataStore(
                dataStoreName,
                thermalVideoName, opticalVideoName,
                thermalFlightName, opticalFlightName,
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
                thermalVideoName, "",
                thermalFlightName, "",
                outputElseInputDirectory,
                canCreate);
        }
    }
}
