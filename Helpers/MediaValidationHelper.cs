using System.Linq;
using System;
using CameraRecordingService.Enums;

namespace CameraRecordingService.Helpers
{
    /// <summary>
    /// Helper class for media validation
    /// </summary>
    public static class MediaValidationHelper
    {
        /// <summary>
        /// Validate resolution
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateResolution(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return (false, "Width and height must be greater than 0");

            if (width > 7680 || height > 4320) // 8K max
                return (false, "Resolution exceeds maximum supported (8K)");

            if (width < 320 || height < 240)
                return (false, "Resolution is below minimum supported (320x240)");

            return (true, string.Empty);
        }

        /// <summary>
        /// Validate FPS
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateFPS(int fps)
        {
            if (fps <= 0)
                return (false, "FPS must be greater than 0");

            if (fps > 120)
                return (false, "FPS exceeds maximum supported (120)");

            if (fps < 10)
                return (false, "FPS is below minimum supported (10)");

            return (true, string.Empty);
        }

        /// <summary>
        /// Validate bitrate
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateBitrate(int bitrate, int width, int height, int fps)
        {
            if (bitrate < Config.RecordingDefaults.MIN_BITRATE)
                return (false, $"Bitrate is below minimum ({Config.RecordingDefaults.MIN_BITRATE} kbps)");

            if (bitrate > Config.RecordingDefaults.MAX_BITRATE)
                return (false, $"Bitrate exceeds maximum ({Config.RecordingDefaults.MAX_BITRATE} kbps)");

            // Check if bitrate is reasonable for resolution
            int pixels = width * height;
            int minRecommended = (pixels * fps) / 10000; // Very rough estimate

            if (bitrate < minRecommended / 2)
                return (false, $"Bitrate may be too low for this resolution. Recommended minimum: {minRecommended} kbps");

            return (true, string.Empty);
        }

        /// <summary>
        /// Check if codec is supported
        /// </summary>
        public static bool IsCodecSupported(VideoCodec codec)
        {
            return Config.RecordingDefaults.SUPPORTED_CODECS.Contains(codec);
        }

        /// <summary>
        /// Check if image format is supported
        /// </summary>
        public static bool IsImageFormatSupported(ImageFormat format)
        {
            return Config.ScreenshotDefaults.SUPPORTED_FORMATS.Contains(format);
        }
    }
}
