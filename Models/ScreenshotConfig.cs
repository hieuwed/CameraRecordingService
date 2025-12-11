using CameraRecordingService.Enums;
using System.Drawing;

namespace CameraRecordingService.Models
{
    /// <summary>
    /// Configuration for taking screenshots
    /// </summary>
    public class ScreenshotConfig
    {
        /// <summary>
        /// Output directory path for the screenshot file
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// File name (without extension)
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Image format (PNG, JPG, or BMP)
        /// </summary>
        public ImageFormat ImageFormat { get; set; } = ImageFormat.PNG;

        /// <summary>
        /// JPEG quality (1-100, only applies to JPG format)
        /// </summary>
        public int Quality { get; set; } = 85;

        /// <summary>
        /// Add timestamp to filename
        /// </summary>
        public bool AddTimestamp { get; set; } = true;

        /// <summary>
        /// Screenshot capture mode (FullScreen or RegionSelection)
        /// </summary>
        public ScreenshotMode Mode { get; set; } = ScreenshotMode.FullScreen;

        /// <summary>
        /// Capture region (only used when Mode is RegionSelection)
        /// </summary>
        public Rectangle? CaptureRegion { get; set; }
    }
}
