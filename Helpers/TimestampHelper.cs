using System.IO;
using System;
namespace CameraRecordingService.Helpers
{
    /// <summary>
    /// Helper class for timestamp operations
    /// </summary>
    public static class TimestampHelper
    {
        /// <summary>
        /// Generate timestamp string for filenames (2025-12-08_15-30-45)
        /// </summary>
        public static string GenerateTimestamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        /// <summary>
        /// Format duration as HH:MM:SS
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            return duration.ToString(@"hh\:mm\:ss");
        }

        /// <summary>
        /// Get human-readable file size (e.g., "125.5 MB")
        /// </summary>
        public static string GetHumanReadableFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Add timestamp to filename
        /// </summary>
        public static string AddTimestampToFileName(string fileName)
        {
            string timestamp = GenerateTimestamp();
            string extension = Path.GetExtension(fileName);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            return $"{nameWithoutExt}_{timestamp}{extension}";
        }
    }
}
