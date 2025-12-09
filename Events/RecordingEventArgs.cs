using CameraRecordingService.Models;
using System;

namespace CameraRecordingService.Events
{
    /// <summary>
    /// Event arguments for recording status changes
    /// </summary>
    public class RecordingEventArgs : EventArgs
    {
        /// <summary>
        /// Current recording status
        /// </summary>
        public RecordingStatus Status { get; set; } = new RecordingStatus();
    }
}
