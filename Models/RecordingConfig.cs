using System;
using CameraRecordingService.Enums;

namespace CameraRecordingService.Models
{
    /// <summary>
    /// Configuration for video recording
    /// </summary>
    public class RecordingConfig
    {
        /// <summary>
        /// Output directory path for the recording file
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// File name (without extension)
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Video codec to use (H.264 or H.265)
        /// </summary>
        public VideoCodec VideoCodec { get; set; } = VideoCodec.H264;

        /// <summary>
        /// Video bitrate in kilobits per second (affects quality and file size)
        /// </summary>
        public int Bitrate { get; set; } = 5000; // 5 Mbps

        /// <summary>
        /// Video width in pixels
        /// </summary>
        public int Width { get; set; } = 1920;

        /// <summary>
        /// Video height in pixels
        /// </summary>
        public int Height { get; set; } = 1080;

        /// <summary>
        /// Frames per second
        /// </summary>
        public int FramesPerSecond { get; set; } = 30;

        /// <summary>
        /// Maximum recording duration (null = unlimited)
        /// </summary>
        public TimeSpan? MaxDuration { get; set; }

        /// <summary>
        /// Enable audio recording
        /// </summary>
        public bool EnableAudio { get; set; } = true;

        /// <summary>
        /// Audio sample rate (44100 or 48000 Hz)
        /// </summary>
        public int AudioSampleRate { get; set; } = 48000;
    }
}
