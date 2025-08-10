using Emgu.CV;
using Emgu.CV.Structure;
using System.Drawing;
using System.Drawing.Imaging;


namespace SkyCombDrone.DrawSpace
{
    internal class BitmapGenerator
    {
        // Standard reference DPI - typically 96 DPI is the default Windows value
        private const float StandardDpi = 96.0f;

        public static Bitmap CreateDpiIndependentBitmap(int width, int height)
        {
            // Create a new bitmap with PixelFormat.Format32bppArgb for best quality
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            // Set the DPI to a fixed value
            bitmap.SetResolution(StandardDpi, StandardDpi);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // Configure graphics object for high quality
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            }

            return bitmap;
        }


        public static Bitmap CreateDpiIndependentBitmap(Image<Bgr, byte> emguImage)
        {
            // Convert Emgu CV image to Bitmap while maintaining quality
            Bitmap convertedBitmap = emguImage.ToBitmap();

            // Create a new bitmap with proper DPI settings
            Bitmap resultBitmap = new Bitmap(convertedBitmap.Width, convertedBitmap.Height, PixelFormat.Format32bppArgb);
            resultBitmap.SetResolution(StandardDpi, StandardDpi);

            // Copy the content with high-quality settings
            using (Graphics g = Graphics.FromImage(resultBitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                // Draw the converted bitmap onto the new DPI-independent bitmap
                g.DrawImage(convertedBitmap, 0, 0, convertedBitmap.Width, convertedBitmap.Height);
            }

            // Clean up the intermediate bitmap
            convertedBitmap.Dispose();

            return resultBitmap;
        }


        public static Image<Bgr, byte> ConvertToEmguImage(Bitmap bitmap)
        {
            // Convert back to Emgu CV image if needed
            return bitmap.ToImage<Bgr, byte>();
        }


        public static void SaveBitmapToFile(Bitmap bitmap, string filePath)
        {
            // When saving, explicitly set the resolution again to ensure it's preserved
            using (Bitmap saveableBitmap = new Bitmap(bitmap))
            {
                saveableBitmap.SetResolution(StandardDpi, StandardDpi);

                // Save with maximum quality
                ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
                EncoderParameters encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 100L);

                saveableBitmap.Save(filePath, jpegCodec, encoderParams);
            }
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            return codecs.FirstOrDefault(codec => codec.MimeType == mimeType);
        }
    }
}
