using System;

namespace CameraRecordingService.Events
{
    /// <summary>
    /// Event arguments for recording errors
    /// </summary>
    public class RecordingErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Exception that caused the error
        /// </summary>
        public Exception? Exception { get; set; }
    }
}
