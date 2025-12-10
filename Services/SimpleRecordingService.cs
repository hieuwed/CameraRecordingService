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
using OpenCvSharp;

namespace CameraRecordingService.Services
{
    /// <summary>
    /// SIMPLE recording service - no FFmpeg, no complexity
    /// Just record frames at realistic FPS directly to MP4
    /// </summary>
    public class SimpleRecordingService : IRecordingService, IDisposable
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
        private VideoWriter? _videoWriter;

        // Events
        public event EventHandler<RecordingEventArgs>? OnRecordingStatusChanged;
        public event EventHandler<RecordingErrorEventArgs>? OnRecordingError;
        public event EventHandler<RecordingEventArgs>? OnRecordingCompleted;

        public bool IsRecording => _isRecording;

        public SimpleRecordingService()
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

                if (!FilePathHelper.EnsureDirectoryExists(config.OutputPath))
                    throw new InvalidRecordingConfigException("OutputPath", "Cannot create directory");

                // Generate output file path
                _outputFilePath = FilePathHelper.GenerateUniqueFileName(
                    config.OutputPath,
                    config.FileName,
                    "mp4");

                // Use VERY LOW FPS for screen recording (3 FPS is realistic for slow screen capture)
                // This matches actual capture speed, so video duration will be correct
                int realisticFPS = 3;
                
                // Initialize VideoWriter with MP4V codec (simple, works everywhere)
                int fourcc = VideoWriter.FourCC('m', 'p', '4', 'v');
                
                _videoWriter = new VideoWriter(
                    _outputFilePath,
                    fourcc,
                    realisticFPS,
                    new Size(config.Width, config.Height),
                    isColor: true
                );

                if (!_videoWriter.IsOpened())
                {
                    _videoWriter?.Dispose();
                    _videoWriter = null;
                    throw new MediaFoundationException("Failed to initialize video writer");
                }

                _currentConfig = config;
                _currentFrameProvider = frameProvider;
                _isRecording = true;
                _frameCount = 0;

                _recordingStopwatch = Stopwatch.StartNew();
                _statusTimer = new Timer(UpdateRecordingStatus, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
                _cancellationTokenSource = new CancellationTokenSource();
                _recordingTask = RecordingTaskAsync(_cancellationTokenSource.Token);

                RaiseRecordingStatusChanged();
                return true;
            }
            catch (Exception ex)
            {
                _isRecording = false;
                _videoWriter?.Dispose();
                _videoWriter = null;
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

                _cancellationTokenSource?.Cancel();

                if (_recordingTask != null)
                    await _recordingTask;

                _statusTimer?.Dispose();
                _recordingStopwatch?.Stop();

                // Close video writer
                _videoWriter?.Release();
                _videoWriter?.Dispose();
                _videoWriter = null;

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

        private async Task RecordingTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Target: 3 FPS (333ms per frame) - realistic for screen capture
                int frameIntervalMs = 333;
                var frameTimer = Stopwatch.StartNew();
                long nextFrameTime = frameIntervalMs;

                while (!cancellationToken.IsCancellationRequested && _isRecording)
                {
                    long currentTime = frameTimer.ElapsedMilliseconds;
                    
                    // Time for next frame?
                    if (currentTime >= nextFrameTime)
                    {
                        var frame = await _currentFrameProvider!.GetCurrentFrameAsync();

                        if (frame != null && frame is Mat mat && !mat.Empty() && _videoWriter != null)
                        {
                            _videoWriter.Write(mat);
                            _frameCount++;
                            mat.Dispose();
                        }
                        
                        // Schedule next frame
                        nextFrameTime += frameIntervalMs;
                    }
                    else
                    {
                        // Wait until next frame time
                        int waitTime = (int)(nextFrameTime - currentTime);
                        await Task.Delay(Math.Min(waitTime, 50), cancellationToken);
                    }

                    // Check max duration
                    if (_currentConfig?.MaxDuration != null &&
                        _recordingStopwatch!.Elapsed > _currentConfig.MaxDuration)
                    {
                        break;
                    }
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

        public async Task<bool> PauseRecordingAsync()
        {
            await Task.CompletedTask;
            return false;
        }

        public async Task<bool> ResumeRecordingAsync()
        {
            await Task.CompletedTask;
            return false;
        }

        public async Task<RecordingStatus> GetRecordingStatusAsync()
        {
            return await Task.FromResult(_currentStatus);
        }

        private void UpdateRecordingStatus(object? state)
        {
            if (!_isRecording || _recordingStopwatch == null)
                return;

            _currentStatus.IsRecording = true;
            _currentStatus.Duration = _recordingStopwatch.Elapsed;
            _currentStatus.FrameCount = _frameCount;
            _currentStatus.UpdatedAt = DateTime.Now;

            if (_recordingStopwatch.ElapsedMilliseconds > 0)
            {
                _currentStatus.CurrentFPS = (_frameCount * 1000.0) / _recordingStopwatch.ElapsedMilliseconds;
            }

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
            _videoWriter?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
