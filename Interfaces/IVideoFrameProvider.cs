using System.Threading.Tasks;
using System;
namespace CameraRecordingService.Interfaces
{
    /// <summary>
    /// Interface for providing video frames from camera
    /// </summary>
    public interface IVideoFrameProvider
    {
        /// <summary>
        /// Get the current frame from camera
        /// </summary>
        /// <returns>Frame object (can be Bitmap, byte[], or custom frame type)</returns>
        Task<object?> GetCurrentFrameAsync();

        /// <summary>
        /// Whether the camera is currently active
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Frame resolution (width, height)
        /// </summary>
        (int Width, int Height) Resolution { get; }
    }
}
