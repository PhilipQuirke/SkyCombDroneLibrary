// Copyright SkyComb Limited 2025. All rights reserved. 
using OfficeOpenXml;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;


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

        public void SaveBitmap(Bitmap? theBitmap, string name, int row, int col, int percent = 100)
        {
            if (theBitmap == null || Worksheet == null)
                return;

            try
            {
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
            catch (Exception ex)
            {
                // Suppress error
                Debug.Print("ExcelImageHandler.SaveBitmap" + ex.ToString());
            }
        }

        public void SaveBitmapSized(Bitmap? theBitmap, string name, int row, int col, int widthPx, int heightPx)
        {
            if (theBitmap == null || Worksheet == null)
                return;

            try
            {
                using (var normalizedBitmap = NormalizeBitmap(theBitmap))
                using (var stream = new MemoryStream())
                {
                    var pngEncoder = GetPngEncoder();
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 100L);

                    normalizedBitmap.Save(stream, pngEncoder, encoderParameters);
                    stream.Position = 0;

                    var picture = Worksheet.Drawings.AddPicture(name, stream);
                    picture.SetPosition(row, 0, col, 0);
                    picture.Border.Width = 0;

                    // Use fixed pixel dimensions if specified
                    if (widthPx > 0 && heightPx > 0)
                    {
                        picture.SetSize(widthPx, heightPx);
                    }
                    else
                    {
                        picture.SetSize(normalizedBitmap.Width, normalizedBitmap.Height);
                    }
                }
            }
            catch (Exception ex)
            {
                // Suppress error
                Debug.Print("ExcelImageHandler.SaveBitmapSized" + ex.ToString());
            }
        }
    }
}
