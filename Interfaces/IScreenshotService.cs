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
        /// Take a single screenshot from camera
        /// </summary>
        /// <param name="frameProvider">Video frame provider</param>
        /// <param name="config">Screenshot configuration</param>
        /// <returns>Screenshot result</returns>
        Task<ScreenshotResult> TakeScreenshotAsync(IVideoFrameProvider frameProvider, ScreenshotConfig config);

        /// <summary>
        /// Start taking screenshots at regular intervals
        /// </summary>
        /// <param name="frameProvider">Video frame provider</param>
        /// <param name="config">Screenshot configuration</param>
        /// <param name="interval">Time interval between screenshots</param>
        Task<bool> StartIntervalScreenshotAsync(IVideoFrameProvider frameProvider, ScreenshotConfig config, TimeSpan interval);

        /// <summary>
        /// Stop interval screenshot
        /// </summary>
        Task<bool> StopIntervalScreenshotAsync();

        /// <summary>
        /// Whether interval screenshot is currently active
        /// </summary>
        bool IsCapturingInterval { get; }

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
