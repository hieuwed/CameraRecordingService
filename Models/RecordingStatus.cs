using System;

namespace CameraRecordingService.Models
{
    /// <summary>
    /// Current status of recording operation
    /// </summary>
    public class RecordingStatus
    {
        /// <summary>
        /// Whether recording is currently active
        /// </summary>
        public bool IsRecording { get; set; }

        /// <summary>
        /// Duration of recording so far
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Current file size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Number of frames recorded
        /// </summary>
        public int FrameCount { get; set; }

        /// <summary>
        /// Current frames per second
        /// </summary>
        public double CurrentFPS { get; set; }

        /// <summary>
        /// Status message
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
