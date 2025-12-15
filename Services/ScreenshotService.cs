using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System;
using CameraRecordingService.Events;
using CameraRecordingService.Exceptions;
using CameraRecordingService.Helpers;
using CameraRecordingService.Interfaces;
using CameraRecordingService.Models;

namespace CameraRecordingService.Services
{
    /// <summary>
    /// Service for taking screenshots
    /// </summary>
    public class ScreenshotService : IScreenshotService
    {
        // Events
        public event EventHandler<ScreenshotEventArgs>? OnScreenshotTaken;
        public event EventHandler<RecordingErrorEventArgs>? OnScreenshotError;

        /// <summary>
        /// Take a single screenshot
        /// </summary>
        public async Task<ScreenshotResult> TakeScreenshotAsync(ScreenshotConfig config)
        {
            try
            {
                if (config == null)
                    throw new ArgumentNullException(nameof(config));

                // Validate config
                var validationResult = ValidateScreenshotConfig(config);
                if (!validationResult.IsValid)
                    throw new InvalidScreenshotConfigException("ScreenshotConfig", validationResult.ErrorMessage);

                // Ensure output directory exists
                if (!FilePathHelper.EnsureDirectoryExists(config.OutputPath))
                    throw new InvalidScreenshotConfigException("OutputPath", "Cannot create directory");

                // Generate filename
                string fileName = config.FileName;
                if (config.AddTimestamp)
                {
                    fileName = $"{fileName}_{TimestampHelper.GenerateTimestamp()}";
                }

                string extension = config.ImageFormat.ToString().ToLower();
                string filePath = FilePathHelper.GenerateUniqueFileName(
                    config.OutputPath,
                    fileName,
                    extension);

                // Capture screenshot based on mode
                System.Drawing.Bitmap? bitmap = null;
                
                if (config.Mode == Enums.ScreenshotMode.FullScreen)
                {
                    // Capture full screen
                    bitmap = Helpers.ScreenCaptureHelper.CaptureFullScreen();
                }
                else if (config.Mode == Enums.ScreenshotMode.RegionSelection && config.CaptureRegion.HasValue)
                {
                    // Capture specific region
                    bitmap = Helpers.ScreenCaptureHelper.CaptureRegion(config.CaptureRegion.Value);
                }
                else
                {
                    throw new InvalidScreenshotConfigException("Mode", "Invalid screenshot mode or missing capture region");
                }

                // Save bitmap to file
                Helpers.ScreenCaptureHelper.SaveBitmap(bitmap, filePath, config.ImageFormat, config.Quality);
                bitmap.Dispose();

                var result = new ScreenshotResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileSize = new FileInfo(filePath).Length,
                    Timestamp = DateTime.Now
                };

                RaiseScreenshotTaken(result);

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                var result = new ScreenshotResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };

                RaiseScreenshotError(ex.Message, ex);

                return result;
            }
        }


        // Private methods

        private (bool IsValid, string ErrorMessage) ValidateScreenshotConfig(ScreenshotConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.OutputPath))
                return (false, "OutputPath cannot be empty");

            if (!FilePathHelper.IsPathValid(config.OutputPath))
                return (false, "OutputPath is not valid");

            if (string.IsNullOrWhiteSpace(config.FileName))
                return (false, "FileName cannot be empty");

            if (!MediaValidationHelper.IsImageFormatSupported(config.ImageFormat))
                return (false, $"Image format {config.ImageFormat} is not supported");

            if (config.ImageFormat == Enums.ImageFormat.JPG)
            {
                if (config.Quality < 1 || config.Quality > 100)
                    return (false, "JPG quality must be between 1 and 100");
            }

            return (true, string.Empty);
        }

        private void RaiseScreenshotTaken(ScreenshotResult result)
        {
            OnScreenshotTaken?.Invoke(this, new ScreenshotEventArgs { Result = result });
        }

        private void RaiseScreenshotError(string message, Exception? exception = null)
        {
            OnScreenshotError?.Invoke(this, new RecordingErrorEventArgs
            {
                ErrorMessage = message,
                Exception = exception
            });
        }
    }
}
