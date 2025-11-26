using Emgu.CV;
using Emgu.CV.Structure;
using SkyCombGround.CommonSpace;
using System.Runtime.InteropServices;

namespace SkyCombDroneLibrary.DroneLogic.DJI
{
    public static class DirpApiWrapper
    {
        private const string DllName = "libdirp.dll";

        // Raw radiometric heat values are generally in range 4000 to 5000. This is a "sanity" check value.
        public const int MinSaneRawHeat = 3000;


        // Native handle type
        private struct SafeDirpHandle : IDisposable
        {
            public IntPtr Handle;
            public void Dispose()
            {
                if (Handle != IntPtr.Zero)
                {
                    dirp_destroy(Handle);
                    Handle = IntPtr.Zero;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct dirp_resolution_t
        {
            public int width;
            public int height;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dirp_create_from_rjpeg(
            byte[] data, int size, out IntPtr ph);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dirp_destroy(IntPtr h);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dirp_get_rjpeg_resolution(
            IntPtr h, out dirp_resolution_t resolution);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int dirp_get_original_raw(
            IntPtr h, [Out] ushort[] raw_image, int size);


        /// <summary>
        /// Loads the raw radiometric data from a DJI R-JPEG file.
        /// </summary>
        /// <param name="jpgPath">Path to the R-JPEG file.</param>
        /// <returns>Raw radiometric data as a ushort array, with image resolution.</returns>
        public static (ushort[] data, int width, int height) GetRawRadiometricData(string jpgPath)
        {
            if (string.IsNullOrWhiteSpace(jpgPath))
                throw new ArgumentException("JPG path must not be null or empty.", nameof(jpgPath));
            if (!File.Exists(jpgPath))
                throw new FileNotFoundException("JPG file not found.", jpgPath);

            byte[] rjpegData = File.ReadAllBytes(jpgPath);

            // Create DIRP handle
            var error_code = dirp_create_from_rjpeg(rjpegData, rjpegData.Length, out IntPtr handle);
            if (error_code != 0)
                throw new InvalidOperationException("Failed to create DIRP handle from R-JPEG:" + error_code);

            using (var safeHandle = new SafeDirpHandle { Handle = handle })
            {
                // Get image resolution
                if (dirp_get_rjpeg_resolution(handle, out dirp_resolution_t resolution) != 0)
                    throw new InvalidOperationException("Failed to get R-JPEG resolution.");

                int pixelCount = resolution.width * resolution.height;
                ushort[] rawData = new ushort[pixelCount];

                // Get raw radiometric data
                if (dirp_get_original_raw(handle, rawData, rawData.Length * sizeof(ushort)) != 0)
                    throw new InvalidOperationException("Failed to get original RAW data.");

                return (rawData, resolution.width, resolution.height);
            }
        }


        public static (ushort[] rawData, int width, int height, ushort minRadioHeat, ushort maxRadioHeat) 
            GetRawRadiometricDataMinMaxData(string input)
        {
            (ushort[] rawData, int width, int height) = DirpApiWrapper.GetRawRadiometricData(input);

            ushort minRadioHeat = rawData.Min();
            ushort maxRadioHeat = rawData.Max();

            return (rawData, width, height, minRadioHeat, maxRadioHeat);
        }


        // Normalize raw radiometric data to 0-255 grayscale image
        // using either image specific min/maxRadioHeat or global overrides
        public static Image<Gray, byte> GetRawRadiometricNormalised(string input, int overrideMinRadioHeat = BaseConstants.UnknownValue, int overrideMaxRadioHeat = BaseConstants.UnknownValue)
        {

            (ushort[] rawData, int width, int height, ushort minRadioHeat, ushort maxRadioHeat) =
                GetRawRadiometricDataMinMaxData(input);

            // If we have global min/max overrides, use them
            if (overrideMinRadioHeat >= MinSaneRawHeat)
                minRadioHeat = (ushort)overrideMinRadioHeat;
            if (overrideMaxRadioHeat >= MinSaneRawHeat)
                maxRadioHeat = (ushort)overrideMaxRadioHeat;

            // Normalize rawData to 0-255
            byte[] normalized = rawData.Select(v =>
                        (byte)(
                            v <= minRadioHeat ? 0 :
                            v >= maxRadioHeat ? 255 :
                            ((v - minRadioHeat) * 255 / Math.Max(1, maxRadioHeat - minRadioHeat))
                        )
                    ).ToArray();

            // Create grayscale image
            Image<Gray, byte> grayImage = new Image<Gray, byte>(width, height);
            System.Buffer.BlockCopy(normalized, 0, grayImage.Data, 0, normalized.Length);

            return grayImage;
        }


        public static Image<Bgr, byte> GetRawRadiometricDataUnitTest(string input)
        {
            var grayImage = GetRawRadiometricNormalised(input);
            return grayImage.Convert<Bgr, byte>();
        }
    }
}