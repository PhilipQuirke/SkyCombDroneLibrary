using System.Drawing;
using System.Drawing.Imaging;
using Emgu.CV.Structure;
using Emgu.CV;
using OfficeOpenXml;


namespace SkyCombDrone.PersistModel
{
    public class ExcelImageHandler
    {
        private const float StandardDpi = 96.0f;
        private readonly ExcelWorksheet? Worksheet;

        public ExcelImageHandler(ExcelWorksheet? worksheet)
        {
            Worksheet = worksheet;
        }

        public void SaveBitmap(Bitmap? theBitmap, string name, int row, int col = 0, int percent = 100)
        {
            if (theBitmap == null || Worksheet == null)
                return;

            using (var normalizedBitmap = NormalizeBitmap(theBitmap))
            using (var stream = new MemoryStream())
            {
                // Save with optimal PNG encoder settings
                var pngEncoder = GetPngEncoder();
                var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 100L);

                // Save the normalized bitmap to the memory stream
                normalizedBitmap.Save(stream, pngEncoder, encoderParameters);

                // Reset stream position before adding to worksheet
                stream.Position = 0;

                // Add picture to worksheet
                var picture = Worksheet.Drawings.AddPicture(name, stream);
                picture.SetPosition(row, 0, col, 0);
                picture.Border.Width = 0;

                // Apply scaling while maintaining aspect ratio
                if (percent != 100)
                {
                    double scale = percent / 100.0;
                    // EPPlus uses pixels for SetSize
                    picture.SetSize((int)(normalizedBitmap.Width * scale), (int)(normalizedBitmap.Height * scale));
                }
            }
        }

        private static Bitmap NormalizeBitmap(Bitmap source)
        {
            // Create a new bitmap with consistent DPI and format
            var normalized = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            normalized.SetResolution(StandardDpi, StandardDpi);

            using (var g = Graphics.FromImage(normalized))
            {
                // Configure for high quality
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                // Draw the original bitmap onto the normalized one
                g.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            return normalized;
        }

        private static ImageCodecInfo GetPngEncoder()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            return codecs.First(codec => codec.FormatID == ImageFormat.Png.Guid);
        }

        public void SaveBitmap(Image<Bgr, byte>? emguImage, string name, int row, int col = 0, int percent = 100)
        {
            if (emguImage == null)
                return;

            using (var bitmap = emguImage.ToBitmap())
            {
                SaveBitmap(bitmap, name, row, col, percent);
            }
        }
    }
}
