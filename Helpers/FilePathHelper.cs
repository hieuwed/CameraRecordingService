using System.IO;
using System;
namespace CameraRecordingService.Helpers
{
    /// <summary>
    /// Helper class for file path operations
    /// </summary>
    public static class FilePathHelper
    {
        /// <summary>
        /// Generate a unique filename by appending a number if file exists
        /// </summary>
        public static string GenerateUniqueFileName(string folder, string baseName, string extension)
        {
            string fileName = $"{baseName}.{extension}";
            string fullPath = Path.Combine(folder, fileName);

            if (!File.Exists(fullPath))
                return fullPath;

            int counter = 1;
            while (File.Exists(fullPath))
            {
                fileName = $"{baseName}_{counter}.{extension}";
                fullPath = Path.Combine(folder, fileName);
                counter++;
            }

            return fullPath;
        }

        /// <summary>
        /// Ensure directory exists, create if it doesn't
        /// </summary>
        public static bool EnsureDirectoryExists(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if path is valid
        /// </summary>
        public static bool IsPathValid(string path)
        {
            try
            {
                Path.GetFullPath(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if file is writable
        /// </summary>
        public static bool IsFileWritable(string filePath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (directory == null) return false;

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream fs = File.Create(filePath, 1, FileOptions.DeleteOnClose))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get default save folder based on type
        /// </summary>
        public static string GetDefaultSaveFolder(bool isVideo = true)
        {
            if (isVideo)
                return Config.RecordingDefaults.DEFAULT_OUTPUT_PATH;
            else
                return Config.ScreenshotDefaults.DEFAULT_OUTPUT_PATH;
        }
    }
}
