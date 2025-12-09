using System.Threading.Tasks;
using System;
using CameraRecordingService.Events;
using CameraRecordingService.Models;

namespace CameraRecordingService.Interfaces
{
    /// <summary>
    /// Interface for video recording service
    /// </summary>
    public interface IRecordingService
    {
        /// <summary>
        /// Start recording from camera
        /// </summary>
        /// <param name="frameProvider">Video frame provider</param>
        /// <param name="config">Recording configuration</param>
        /// <returns>True if recording started successfully</returns>
        Task<bool> StartRecordingAsync(IVideoFrameProvider frameProvider, RecordingConfig config);

        /// <summary>
        /// Stop recording and return file path
        /// </summary>
        /// <returns>Path to the recorded file</returns>
        Task<string> StopRecordingAsync();

        /// <summary>
        /// Pause recording (advanced feature)
        /// </summary>
        Task<bool> PauseRecordingAsync();

        /// <summary>
        /// Resume recording (advanced feature)
        /// </summary>
        Task<bool> ResumeRecordingAsync();

        /// <summary>
        /// Get current recording status
        /// </summary>
        Task<RecordingStatus> GetRecordingStatusAsync();

        /// <summary>
        /// Whether recording is currently active
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// Event: Recording status changed
        /// </summary>
        event EventHandler<RecordingEventArgs>? OnRecordingStatusChanged;

        /// <summary>
        /// Event: Recording error occurred
        /// </summary>
        event EventHandler<RecordingErrorEventArgs>? OnRecordingError;

        /// <summary>
        /// Event: Recording completed
        /// </summary>
        event EventHandler<RecordingEventArgs>? OnRecordingCompleted;
    }
}
