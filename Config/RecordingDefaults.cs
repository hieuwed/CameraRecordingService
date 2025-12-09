using System;
using CameraRecordingService.Enums;
using System.IO;

namespace CameraRecordingService.Config
{
    /// <summary>
    /// Default values and constants for recording
    /// </summary>
    public static class RecordingDefaults
    {
        /// <summary>
        /// Default video codec
        /// </summary>
        public static readonly VideoCodec DEFAULT_VIDEO_CODEC = VideoCodec.H264;

        /// <summary>
        /// Default bitrate in kbps (5 Mbps)
        /// </summary>
        public const int DEFAULT_BITRATE = 5000;

        /// <summary>
        /// Default output path (Videos folder)
        /// </summary>
        public static readonly string DEFAULT_OUTPUT_PATH = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "CameraRecordings");

        /// <summary>
        /// Supported video codecs
        /// </summary>
        public static readonly VideoCodec[] SUPPORTED_CODECS = { VideoCodec.H264, VideoCodec.H265 };

        /// <summary>
        /// Minimum bitrate in kbps
        /// </summary>
        public const int MIN_BITRATE = 500;

        /// <summary>
        /// Maximum bitrate in kbps
        /// </summary>
        public const int MAX_BITRATE = 50000;

        /// <summary>
        /// Maximum recording duration (12 hours)
        /// </summary>
        public static readonly TimeSpan MAX_RECORDING_DURATION = TimeSpan.FromHours(12);

        /// <summary>
        /// Default resolution width
        /// </summary>
        public const int DEFAULT_WIDTH = 1920;

        /// <summary>
        /// Default resolution height
        /// </summary>
        public const int DEFAULT_HEIGHT = 1080;

        /// <summary>
        /// Default frames per second
        /// </summary>
        public const int DEFAULT_FPS = 30;
    }
}
