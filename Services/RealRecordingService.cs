using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CameraRecordingService.Config;
using CameraRecordingService.Events;
using CameraRecordingService.Exceptions;
using CameraRecordingService.Helpers;
using CameraRecordingService.Interfaces;
using CameraRecordingService.Models;

namespace CameraRecordingService.Services
{
    /// <summary>
    /// Service for video recording with real encoding
    /// </summary>
    public class RealRecordingService : IRecordingService, IDisposable
    {
        private bool _isRecording;
        private RecordingConfig? _currentConfig;
        private IVideoFrameProvider? _currentFrameProvider;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _recordingTask;
        private Stopwatch? _recordingStopwatch;
        private RecordingStatus _currentStatus;
        private string _outputFilePath = string.Empty;
        private int _frameCount = 0;
        private Timer? _statusTimer;
        private VideoEncodingService? _encodingService;

        // Events
        public event EventHandler<RecordingEventArgs>? OnRecordingStatusChanged;
        public event EventHandler<RecordingErrorEventArgs>? OnRecordingError;
        public event EventHandler<RecordingEventArgs>? OnRecordingCompleted;

        // Properties
        public bool IsRecording => _isRecording;

        public RealRecordingService()
        {
            _isRecording = false;
            _currentStatus = new RecordingStatus();
        }

        public async Task<bool> StartRecordingAsync(IVideoFrameProvider frameProvider, RecordingConfig config)
        {
            try
            {
                if (_isRecording)
                    throw new RecordingAlreadyInProgressException();

                if (frameProvider == null)
                    throw new ArgumentNullException(nameof(frameProvider));

                if (config == null)
                    throw new ArgumentNullException(nameof(config));

                // Validate config
                var validationResult = ValidateRecordingConfig(config);
                if (!validationResult.IsValid)
                    throw new InvalidRecordingConfigException("RecordingConfig", validationResult.ErrorMessage);

                // Ensure output directory exists
                if (!FilePathHelper.EnsureDirectoryExists(config.OutputPath))
                    throw new InvalidRecordingConfigException("OutputPath", "Cannot create directory");

                // Check disk space
                long estimatedSize = DiskSpaceHelper.EstimateRecordingSize(
                    config.MaxDuration ?? TimeSpan.FromMinutes(30),
                    config.Bitrate);

                DiskSpaceHelper.CheckDiskSpace(config.OutputPath, estimatedSize);

                // Generate output file path
                string extension = "avi"; // Use AVI for better compatibility with MJPEG codec
                _outputFilePath = FilePathHelper.GenerateUniqueFileName(
                    config.OutputPath,
                    config.FileName,
                    extension);

                // Initialize video encoder
                _encodingService = new VideoEncodingService();
                bool encoderInitialized = _encodingService.Initialize(
                    _outputFilePath,
                    config.Width,
                    config.Height,
                    config.FramesPerSecond,
                    config.VideoCodec
                );

                if (!encoderInitialized)
                {
                    _encodingService?.Dispose();
                    _encodingService = null;
                    throw new MediaFoundationException("Failed to initialize video encoder");
                }

                // Setup
                _currentConfig = config;
                _currentFrameProvider = frameProvider;
                _isRecording = true;
                _frameCount = 0;

                // Start stopwatch
                _recordingStopwatch = Stopwatch.StartNew();

                // Start status timer (update every 500ms)
                _statusTimer = new Timer(UpdateRecordingStatus, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));

                // Start recording task
                _cancellationTokenSource = new CancellationTokenSource();
                _recordingTask = RecordingTaskAsync(_cancellationTokenSource.Token);

                RaiseRecordingStatusChanged();
                return true;
            }
            catch (Exception ex)
            {
                _isRecording = false;
                _encodingService?.Dispose();
                _encodingService = null;
                RaiseRecordingError(ex.Message, ex);
                return false;
            }
        }

        public async Task<string> StopRecordingAsync()
        {
            try
            {
                if (!_isRecording)
                    throw new RecordingNotStartedException();

                // Signal cancellation
                _cancellationTokenSource?.Cancel();

                // Wait for task to complete
                if (_recordingTask != null)
                    await _recordingTask;

                // Stop timer and stopwatch
                _statusTimer?.Dispose();
                _recordingStopwatch?.Stop();

                // Finalize encoding
                _encodingService?.Finalize();
                _encodingService?.Dispose();
                _encodingService = null;

                _isRecording = false;

                RaiseRecordingStatusChanged();
                RaiseRecordingCompleted();

                return _outputFilePath;
            }
            catch (Exception ex)
            {
                RaiseRecordingError(ex.Message, ex);
                throw;
            }
        }

        public async Task<bool> PauseRecordingAsync()
        {
            // TODO: Implement pause
            await Task.CompletedTask;
            return false;
        }

        public async Task<bool> ResumeRecordingAsync()
        {
            // TODO: Implement resume
            await Task.CompletedTask;
            return false;
        }

        public async Task<RecordingStatus> GetRecordingStatusAsync()
        {
            return await Task.FromResult(_currentStatus);
        }

        private async Task RecordingTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                int targetFrameDelay = 1000 / (_currentConfig?.FramesPerSecond ?? 30);

                while (!cancellationToken.IsCancellationRequested && _isRecording)
                {
                    // Get frame from provider
                    var frame = await _currentFrameProvider!.GetCurrentFrameAsync();

                    if (frame != null && _encodingService != null)
                    {
                        // Write frame to video
                        bool written = await _encodingService.WriteFrameAsync(frame);
                        if (written)
                        {
                            _frameCount++;
                        }
                    }

                    // Check max duration
                    if (_currentConfig?.MaxDuration != null &&
                        _recordingStopwatch!.Elapsed > _currentConfig.MaxDuration)
                    {
                        break;
                    }

                    // Delay to maintain target FPS
                    await Task.Delay(targetFrameDelay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                RaiseRecordingError($"Recording error: {ex.Message}", ex);
            }
        }

        private void UpdateRecordingStatus(object? state)
        {
            if (!_isRecording || _recordingStopwatch == null)
                return;

            _currentStatus.IsRecording = true;
            _currentStatus.Duration = _recordingStopwatch.Elapsed;
            _currentStatus.FrameCount = _frameCount;
            _currentStatus.UpdatedAt = DateTime.Now;

            // Calculate FPS
            if (_recordingStopwatch.ElapsedMilliseconds > 0)
            {
                _currentStatus.CurrentFPS = (_frameCount * 1000.0) / _recordingStopwatch.ElapsedMilliseconds;
            }

            // Get file size if file exists
            if (File.Exists(_outputFilePath))
            {
                var fileInfo = new FileInfo(_outputFilePath);
                _currentStatus.FileSize = fileInfo.Length;
            }

            _currentStatus.StatusMessage =
                $"Recording: {TimestampHelper.FormatDuration(_currentStatus.Duration)} " +
                $"| {TimestampHelper.GetHumanReadableFileSize(_currentStatus.FileSize)} " +
                $"| {_currentStatus.CurrentFPS:F1} FPS";

            RaiseRecordingStatusChanged();
        }

        private (bool IsValid, string ErrorMessage) ValidateRecordingConfig(RecordingConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.OutputPath))
                return (false, "OutputPath cannot be empty");

            if (!FilePathHelper.IsPathValid(config.OutputPath))
                return (false, "OutputPath is not valid");

            if (string.IsNullOrWhiteSpace(config.FileName))
                return (false, "FileName cannot be empty");

            var resValidation = MediaValidationHelper.ValidateResolution(config.Width, config.Height);
            if (!resValidation.IsValid)
                return resValidation;

            var fpsValidation = MediaValidationHelper.ValidateFPS(config.FramesPerSecond);
            if (!fpsValidation.IsValid)
                return fpsValidation;

            var bitrateValidation = MediaValidationHelper.ValidateBitrate(
                config.Bitrate,
                config.Width,
                config.Height,
                config.FramesPerSecond);

            if (!bitrateValidation.IsValid)
                return bitrateValidation;

            return (true, string.Empty);
        }

        private void RaiseRecordingStatusChanged()
        {
            OnRecordingStatusChanged?.Invoke(this, new RecordingEventArgs { Status = _currentStatus });
        }

        private void RaiseRecordingError(string message, Exception? exception = null)
        {
            OnRecordingError?.Invoke(this, new RecordingErrorEventArgs
            {
                ErrorMessage = message,
                Exception = exception
            });
        }

        private void RaiseRecordingCompleted()
        {
            OnRecordingCompleted?.Invoke(this, new RecordingEventArgs { Status = _currentStatus });
        }

        public void Dispose()
        {
            _statusTimer?.Dispose();
            _encodingService?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
