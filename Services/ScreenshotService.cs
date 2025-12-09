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
    /// Service for taking screenshots (stub implementation)
    /// </summary>
    public class ScreenshotService : IScreenshotService
    {
        private bool _isCapturingInterval;
        private Timer? _intervalTimer;
        private IVideoFrameProvider? _currentFrameProvider;
        private ScreenshotConfig? _currentConfig;

        // Events
        public event EventHandler<ScreenshotEventArgs>? OnScreenshotTaken;
        public event EventHandler<RecordingErrorEventArgs>? OnScreenshotError;

        // Properties
        public bool IsCapturingInterval => _isCapturingInterval;

        public ScreenshotService()
        {
            _isCapturingInterval = false;
        }

        /// <summary>
        /// Take a single screenshot
        /// </summary>
        public async Task<ScreenshotResult> TakeScreenshotAsync(IVideoFrameProvider frameProvider, ScreenshotConfig config)
        {
            try
            {
                if (frameProvider == null)
                    throw new ArgumentNullException(nameof(frameProvider));

                if (config == null)
                    throw new ArgumentNullException(nameof(config));

                // Validate config
                var validationResult = ValidateScreenshotConfig(config);
                if (!validationResult.IsValid)
                    throw new InvalidScreenshotConfigException("ScreenshotConfig", validationResult.ErrorMessage);

                // Ensure output directory exists
                if (!FilePathHelper.EnsureDirectoryExists(config.OutputPath))
                    throw new InvalidScreenshotConfigException("OutputPath", "Cannot create directory");

                // Get frame from camera
                var frame = await frameProvider.GetCurrentFrameAsync();
                if (frame == null)
                    throw new CameraFrameUnavailableException();

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

                // TODO: Implement actual frame saving
                // For now, create a dummy file
                await File.WriteAllTextAsync(filePath, $"Screenshot taken at {DateTime.Now}");

                var result = new ScreenshotResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileSize = new FileInfo(filePath).Length,
                    Timestamp = DateTime.Now
                };

                RaiseScreenshotTaken(result);

                return result;
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

        /// <summary>
        /// Start taking screenshots at intervals
        /// </summary>
        public async Task<bool> StartIntervalScreenshotAsync(IVideoFrameProvider frameProvider, ScreenshotConfig config, TimeSpan interval)
        {
            try
            {
                if (_isCapturingInterval)
                    return false;

                if (interval < Config.ScreenshotDefaults.MIN_INTERVAL_SCREENSHOT ||
                    interval > Config.ScreenshotDefaults.MAX_INTERVAL_SCREENSHOT)
                {
                    throw new InvalidScreenshotConfigException("Interval", 
                        $"Interval must be between {Config.ScreenshotDefaults.MIN_INTERVAL_SCREENSHOT.TotalSeconds}s and {Config.ScreenshotDefaults.MAX_INTERVAL_SCREENSHOT.TotalSeconds}s");
                }

                _currentFrameProvider = frameProvider;
                _currentConfig = config;
                _isCapturingInterval = true;

                _intervalTimer = new Timer(async _ =>
                {
                    if (_currentFrameProvider != null && _currentConfig != null)
                    {
                        await TakeScreenshotAsync(_currentFrameProvider, _currentConfig);
                    }
                }, null, TimeSpan.Zero, interval);

                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                RaiseScreenshotError(ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// Stop interval screenshot
        /// </summary>
        public async Task<bool> StopIntervalScreenshotAsync()
        {
            if (!_isCapturingInterval)
                return false;

            _intervalTimer?.Dispose();
            _intervalTimer = null;
            _isCapturingInterval = false;
            _currentFrameProvider = null;
            _currentConfig = null;

            await Task.CompletedTask;
            return true;
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
