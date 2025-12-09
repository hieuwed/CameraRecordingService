using System;

namespace CameraRecordingService.Models
{
    /// <summary>
    /// Result of a screenshot operation
    /// </summary>
    public class ScreenshotResult
    {
        /// <summary>
        /// Whether the screenshot was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Full path to the saved screenshot file
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Timestamp when screenshot was taken
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Error message if screenshot failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
