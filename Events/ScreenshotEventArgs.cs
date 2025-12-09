using System;
using CameraRecordingService.Models;

namespace CameraRecordingService.Events
{
    /// <summary>
    /// Event arguments for screenshot events
    /// </summary>
    public class ScreenshotEventArgs : EventArgs
    {
        /// <summary>
        /// Screenshot result
        /// </summary>
        public ScreenshotResult Result { get; set; } = new ScreenshotResult();
    }
}
