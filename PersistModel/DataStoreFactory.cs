using OfficeOpenXml;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;


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
                answer = BaseDataStore.SwapFileNameExtension(fileName, ".mov");
                if (!System.IO.File.Exists(answer))
                {
                    answer = "";
                }
            }
            return answer;
        }


        // Calculate the names of the input files we have available.
        public static (string, string, string, string) LocateInputFiles(
            Func<string, DateTime> readDateEncodedUtc,
            string firstFileName)
        {
            string thermalVideoName = "", opticalVideoName = "", thermalFlightName = "", opticalFlightName = "";

            try
            {
                // Without at least one video we can't do anything
                var firstVideoName = FindVideo(firstFileName);
                if (firstVideoName.Length > 0)
                {
                    var firstVideoData = new VideoModel(firstVideoName, true, readDateEncodedUtc); // guess at thermal

                    // Do we have a second video (in a thermal/optical pair) that overlaps the first video timewise closely?
                    var secondVideoName = FindVideo(DroneDataStore.DeltaNumericString(firstVideoName, (firstVideoData.Thermal ? -1 : +1)));
                    VideoModel secondVideoData = null;
                    if (secondVideoName.Length > 0)
                    {
                        secondVideoData = new VideoModel(secondVideoName, true, readDateEncodedUtc); // guess at thermal

                        // Based on video DateEncodedUtc datetime and durationMs this is an accurate match mechanism
                        if (VideoModel.PercentOverlap(firstVideoData, secondVideoData) < 95)
                            // We only have one video
                            secondVideoName = "";
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
                Console.WriteLine("DataStoreFactory.LocateInputFiles: " + ex.Message);

                thermalVideoName = "";
                opticalVideoName = "";
                thermalFlightName = "";
                opticalFlightName = "";
            }

            return (thermalVideoName, opticalVideoName, thermalFlightName, opticalFlightName);
        }


        public static string DataStoreName(string inputFileName, string outputElseInputDirectory)
        {
            return BaseDataStore.AddFileNameSuffix(outputElseInputDirectory + "\\" + VideoModel.ShortFileName(inputFileName), DataStoreSuffix);
        }


        // From a video file name, locate all applicable input files, ensure that a DataStore exists. 
        public static DroneDataStore Create(
            Func<string, DateTime> readDateEncodedUtc,
            string videoFileName, 
            string outputElseInputDirectory)
        {
            DroneDataStore answer = null;

            try
            {
                (var thermalVideoName, var opticalVideoName, var thermalFlightName, var opticalFlightName) =
                    LocateInputFiles(readDateEncodedUtc, videoFileName);

                var dataStoreName = "";
                if (thermalVideoName != "")
                    dataStoreName = DataStoreName(thermalVideoName, outputElseInputDirectory);
                else if (opticalVideoName != "")
                    dataStoreName = DataStoreName(opticalVideoName, outputElseInputDirectory);

                if (dataStoreName != "")
                {
                    if (System.IO.File.Exists(dataStoreName))
                    {
                        // Open the existing datastore. Will fail if the user has the datastore open for editing.
                        // Assume it contains data matching the files found already.
                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                        ExcelPackage store = new(dataStoreName);
                        if (store != null)
                        {
                            var worksheet = store.Workbook.Worksheets[DroneDataStore.FilesTabName];
                            if (worksheet != null)
                            {
                                var cell = worksheet.Cells[1, 1];
                                if ((cell != null) && (cell.Value != null) && (cell.Value.ToString() == DroneDataStore.Main1Title))
                                    answer = new(store, dataStoreName);
                            }
                        }
                        else
                        {
                            throw BaseConstants.ThrowException("DataStoreFactory.Create: Failed to open existing DataStore " + dataStoreName);
                        }
                    }
                    else
                        // Create a new datastore.
                        // One failure mode is if the outputElseInputDirectory does not exist.                        
                        answer = new(dataStoreName,
                            thermalVideoName, opticalVideoName,
                            thermalFlightName, opticalFlightName,
                            VideoData.OutputVideoFileName(videoFileName, outputElseInputDirectory));
                }
            }
            catch
            {
                answer = null;
            }

            return answer;
        }

    }
}
