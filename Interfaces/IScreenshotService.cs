using System.Threading.Tasks;
using System;
using CameraRecordingService.Events;
using CameraRecordingService.Models;

namespace CameraRecordingService.Interfaces
{
    /// <summary>
    /// Interface for screenshot service
    /// </summary>
    public interface IScreenshotService
    {
        /// <summary>
        /// Take a single screenshot
        /// </summary>
        /// <param name="config">Screenshot configuration</param>
        /// <returns>Screenshot result</returns>
        Task<ScreenshotResult> TakeScreenshotAsync(ScreenshotConfig config);

        /// <summary>
        /// Event: Screenshot taken successfully
        /// </summary>
        event EventHandler<ScreenshotEventArgs>? OnScreenshotTaken;

        /// <summary>
        /// Event: Screenshot error occurred
        /// </summary>
        event EventHandler<RecordingErrorEventArgs>? OnScreenshotError;
    }
}
