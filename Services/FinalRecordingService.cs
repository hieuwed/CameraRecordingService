using System;
using System.Collections.Generic;
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
    /// ULTIMATE SIMPLE SOLUTION: Capture all frames, then write video with correct FPS
    /// This GUARANTEES video duration = recording duration
    /// </summary>
    public class FinalRecordingService : IRecordingService, IDisposable
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
        
        // Store frames in memory
        private List<Mat> _capturedFrames = new List<Mat>();

        // Events
        public event EventHandler<RecordingEventArgs>? OnRecordingStatusChanged;
        public event EventHandler<RecordingErrorEventArgs>? OnRecordingError;
        public event EventHandler<RecordingEventArgs>? OnRecordingCompleted;

        public bool IsRecording => _isRecording;

        public FinalRecordingService()
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

                _currentConfig = config;
                _currentFrameProvider = frameProvider;
                _isRecording = true;
                _frameCount = 0;
                _capturedFrames.Clear();

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

                _isRecording = false;

                // Calculate ACTUAL FPS from recording
                double recordingTimeSeconds = _recordingStopwatch.Elapsed.TotalSeconds;
                double actualFPS = _frameCount / recordingTimeSeconds;
                
                // Write video with ACTUAL FPS
                await WriteVideoWithActualFPS(_outputFilePath, actualFPS);
                
                // Clean up frames
                foreach (var frame in _capturedFrames)
                {
                    frame?.Dispose();
                }
                _capturedFrames.Clear();

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

        private async Task WriteVideoWithActualFPS(string outputPath, double fps)
        {
            await Task.Run(() =>
            {
                if (_capturedFrames.Count == 0 || _currentConfig == null)
                    return;

                // Use actual FPS calculated from recording time
                // This ensures: duration = frame_count / fps = recording_time ✓
                int fourcc = VideoWriter.FourCC('m', 'p', '4', 'v');
                
                using (var writer = new VideoWriter(
                    outputPath,
                    fourcc,
                    fps,
                    new Size(_currentConfig.Width, _currentConfig.Height),
                    isColor: true))
                {
                    if (!writer.IsOpened())
                        throw new MediaFoundationException("Failed to create video file");

                    // Write all captured frames
                    foreach (var frame in _capturedFrames)
                    {
                        if (frame != null && !frame.Empty())
                        {
                            writer.Write(frame);
                        }
                    }
                }
            });
        }

        private async Task RecordingTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Capture frames as fast as possible
                while (!cancellationToken.IsCancellationRequested && _isRecording)
                {
                    var frame = await _currentFrameProvider!.GetCurrentFrameAsync();

                    if (frame != null && frame is Mat mat && !mat.Empty())
                    {
                        // Clone and store the frame
                        _capturedFrames.Add(mat.Clone());
                        _frameCount++;
                        mat.Dispose();
                    }

                    // Check max duration
                    if (_currentConfig?.MaxDuration != null &&
                        _recordingStopwatch!.Elapsed > _currentConfig.MaxDuration)
                    {
                        break;
                    }

                    // Small delay to prevent CPU overload
                    await Task.Delay(10, cancellationToken);
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

            // Estimate file size based on frames
            _currentStatus.FileSize = _frameCount * 100000; // Rough estimate

            _currentStatus.StatusMessage =
                $"Recording: {TimestampHelper.FormatDuration(_currentStatus.Duration)} " +
                $"| {_frameCount} frames " +
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
            _cancellationTokenSource?.Dispose();
            
            // Clean up any remaining frames
            foreach (var frame in _capturedFrames)
            {
                frame?.Dispose();
            }
            _capturedFrames.Clear();
        }
    }
}
