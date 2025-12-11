using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CameraRecordingService.Helpers
{
    /// <summary>
    /// Helper class for screen capture operations
    /// </summary>
    public static class ScreenCaptureHelper
    {
        /// <summary>
        /// Capture the entire primary screen
        /// </summary>
        /// <returns>Bitmap of the entire screen</returns>
        public static Bitmap CaptureFullScreen()
        {
            // Get primary screen bounds
            var bounds = Screen.PrimaryScreen.Bounds;
            return CaptureRegion(new Rectangle(0, 0, bounds.Width, bounds.Height));
        }

        /// <summary>
        /// Capture a specific region of the screen
        /// </summary>
        /// <param name="region">Rectangle defining the region to capture</param>
        /// <returns>Bitmap of the specified region</returns>
        public static Bitmap CaptureRegion(Rectangle region)
        {
            // Create bitmap with region size
            var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Copy screen content to bitmap
                graphics.CopyFromScreen(
                    region.Left,
                    region.Top,
                    0,
                    0,
                    region.Size,
                    CopyPixelOperation.SourceCopy);
            }

            return bitmap;
        }

        /// <summary>
        /// Save bitmap to file with specified format and quality
        /// </summary>
        /// <param name="bitmap">Bitmap to save</param>
        /// <param name="filePath">Output file path</param>
        /// <param name="format">Image format</param>
        /// <param name="quality">JPEG quality (1-100, only for JPG)</param>
        public static void SaveBitmap(Bitmap bitmap, string filePath, Enums.ImageFormat format, int quality = 85)
        {
            ImageFormat imageFormat;
            
            switch (format)
            {
                case Enums.ImageFormat.PNG:
                    imageFormat = ImageFormat.Png;
                    bitmap.Save(filePath, imageFormat);
                    break;

                case Enums.ImageFormat.BMP:
                    imageFormat = ImageFormat.Bmp;
                    bitmap.Save(filePath, imageFormat);
                    break;

                case Enums.ImageFormat.JPG:
                    imageFormat = ImageFormat.Jpeg;
                    
                    // Set JPEG quality
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality, 
                        (long)quality);
                    
                    var jpegCodec = GetEncoderInfo("image/jpeg");
                    if (jpegCodec != null)
                    {
                        bitmap.Save(filePath, jpegCodec, encoderParameters);
                    }
                    else
                    {
                        bitmap.Save(filePath, imageFormat);
                    }
                    break;

                default:
                    throw new ArgumentException($"Unsupported image format: {format}");
            }
        }

        private static ImageCodecInfo? GetEncoderInfo(string mimeType)
        {
            var encoders = ImageCodecInfo.GetImageEncoders();
            foreach (var encoder in encoders)
            {
                if (encoder.MimeType == mimeType)
                    return encoder;
            }
            return null;
        }
    }
}
