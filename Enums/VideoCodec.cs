using System;

namespace CameraRecordingService.Enums
{
    /// <summary>
    /// Video codec types supported for recording
    /// </summary>
    public enum VideoCodec
    {
        /// <summary>
        /// H.264 codec (most compatible)
        /// </summary>
        H264,

        /// <summary>
        /// H.265 codec (better compression)
        /// </summary>
        H265,

        /// <summary>
        /// MJPEG codec (simple, larger files)
        /// </summary>
        MJPEG
    }
}
