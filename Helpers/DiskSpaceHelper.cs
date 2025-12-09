using System.IO;
using System;
namespace CameraRecordingService.Helpers
{
    /// <summary>
    /// Helper class for disk space operations
    /// </summary>
    public static class DiskSpaceHelper
    {
        /// <summary>
        /// Estimate recording size based on duration and bitrate
        /// </summary>
        /// <param name="duration">Recording duration</param>
        /// <param name="bitrate">Bitrate in kbps</param>
        /// <returns>Estimated size in bytes</returns>
        public static long EstimateRecordingSize(TimeSpan duration, int bitrate)
        {
            // bitrate is in kbps, convert to bytes per second
            double bytesPerSecond = (bitrate * 1000.0) / 8.0;
            long estimatedBytes = (long)(bytesPerSecond * duration.TotalSeconds);

            // Add 10% overhead for container format
            return (long)(estimatedBytes * 1.1);
        }

        /// <summary>
        /// Check if there's enough disk space
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <param name="requiredBytes">Required space in bytes</param>
        /// <exception cref="Exceptions.DiskSpaceInsufficientException">Thrown when disk space is insufficient</exception>
        public static void CheckDiskSpace(string path, long requiredBytes)
        {
            long availableBytes = GetDriveFreeSpace(path);

            if (availableBytes < requiredBytes)
            {
                throw new Exceptions.DiskSpaceInsufficientException(requiredBytes, availableBytes);
            }
        }

        /// <summary>
        /// Get free space on drive containing the path
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>Free space in bytes</returns>
        public static long GetDriveFreeSpace(string path)
        {
            try
            {
                string? root = Path.GetPathRoot(path);
                if (root == null) return 0;

                DriveInfo drive = new DriveInfo(root);
                return drive.AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }
    }
}
