using System;
using CameraRecordingService.Enums;
using System.IO;

namespace CameraRecordingService.Config
{
    /// <summary>
    /// Default values and constants for screenshots
    /// </summary>
    public static class ScreenshotDefaults
    {
        /// <summary>
        /// Default image format
        /// </summary>
        public static readonly ImageFormat DEFAULT_IMAGE_FORMAT = ImageFormat.PNG;

        /// <summary>
        /// Default output path (Pictures folder)
        /// </summary>
        public static readonly string DEFAULT_OUTPUT_PATH = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CameraScreenshots");

        /// <summary>
        /// Supported image formats
        /// </summary>
        public static readonly ImageFormat[] SUPPORTED_FORMATS = { ImageFormat.PNG, ImageFormat.JPG, ImageFormat.BMP };

        /// <summary>
        /// Default JPG quality (1-100)
        /// </summary>
        public const int DEFAULT_JPG_QUALITY = 85;

        /// <summary>
        /// Minimum interval for interval screenshots
        /// </summary>
        public static readonly TimeSpan MIN_INTERVAL_SCREENSHOT = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Maximum interval for interval screenshots
        /// </summary>
        public static readonly TimeSpan MAX_INTERVAL_SCREENSHOT = TimeSpan.FromSeconds(60);
    }
}
