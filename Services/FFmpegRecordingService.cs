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
    /// Recording service using FFmpeg for H.265/MP4 encoding
    /// </summary>
    public class FFmpegRecordingService : IRecordingService, IDisposable
    {
        private bool _isRecording;
        private RecordingConfig? _currentConfig;
        private IVideoFrameProvider? _currentFrameProvider;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _recordingTask;
        private System.Diagnostics.Stopwatch? _recordingStopwatch;
        private RecordingStatus _currentStatus;
        private string _outputFilePath = string.Empty;
        private string _tempFramesPath = string.Empty;
        private int _frameCount = 0;
        private Timer? _statusTimer;
        private Process? _ffmpegProcess;

        // Events
        public event EventHandler<RecordingEventArgs>? OnRecordingStatusChanged;
        public event EventHandler<RecordingErrorEventArgs>? OnRecordingError;
        public event EventHandler<RecordingEventArgs>? OnRecordingCompleted;

        // Properties
        public bool IsRecording => _isRecording;

        public FFmpegRecordingService()
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
                    config.MaxDuration ?? TimeSpan.FromHours(2),
                    config.Bitrate);

                DiskSpaceHelper.CheckDiskSpace(config.OutputPath, estimatedSize);

                // Generate output file path
                string extension = "mp4";
                _outputFilePath = FilePathHelper.GenerateUniqueFileName(
                    config.OutputPath,
                    config.FileName,
                    extension);

                // Create temp directory for frames
                _tempFramesPath = Path.Combine(Path.GetTempPath(), $"recording_{Guid.NewGuid():N}");
                Directory.CreateDirectory(_tempFramesPath);

                // Setup
                _currentConfig = config;
                _currentFrameProvider = frameProvider;
                _isRecording = true;
                _frameCount = 0;

                // Start stopwatch
                _recordingStopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Start status timer
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

                // Wait for recording task
                if (_recordingTask != null)
                    await _recordingTask;

                // Stop timer and stopwatch
                _statusTimer?.Dispose();
                _recordingStopwatch?.Stop();

                _isRecording = false;

                // Encode frames to MP4 using FFmpeg
                await EncodeFramesToMP4();

                // Cleanup temp frames
                if (Directory.Exists(_tempFramesPath))
                {
                    Directory.Delete(_tempFramesPath, true);
                }

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

        private async Task RecordingTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                int targetFrameDelay = 1000 / (_currentConfig?.FramesPerSecond ?? 30);

                while (!cancellationToken.IsCancellationRequested && _isRecording)
                {
                    // Get frame from provider
                    var frame = await _currentFrameProvider!.GetCurrentFrameAsync();

                    if (frame != null && frame is Mat mat && !mat.Empty())
                    {
                        // Save frame as image
                        string framePath = Path.Combine(_tempFramesPath, $"frame_{_frameCount:D6}.jpg");
                        Cv2.ImWrite(framePath, mat);
                        _frameCount++;
                        
                        mat.Dispose();
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

        private async Task EncodeFramesToMP4()
        {
            try
            {
                // FFmpeg command for H.265/MP4 encoding
                // -c:v libx265: Use H.265 codec
                // -preset medium: Balance between speed and compression
                // -crf 28: Quality (lower = better, 28 is good for screen recording)
                // -pix_fmt yuv420p: Compatibility with most players
                
                string ffmpegArgs = $"-framerate {_currentConfig!.FramesPerSecond} " +
                    $"-i \"{_tempFramesPath}\\frame_%06d.jpg\" " +
                    $"-c:v libx265 " +
                    $"-preset medium " +
                    $"-crf 28 " +
                    $"-pix_fmt yuv420p " +
                    $"-y \"{_outputFilePath}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = ffmpegArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _ffmpegProcess = Process.Start(processInfo);
                
                if (_ffmpegProcess != null)
                {
                    await _ffmpegProcess.WaitForExitAsync();
                    
                    if (_ffmpegProcess.ExitCode != 0)
                    {
                        string error = await _ffmpegProcess.StandardError.ReadToEndAsync();
                        throw new MediaFoundationException($"FFmpeg encoding failed: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new MediaFoundationException($"Failed to encode video: {ex.Message}");
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

            if (_recordingStopwatch.ElapsedMilliseconds > 0)
            {
                _currentStatus.CurrentFPS = (_frameCount * 1000.0) / _recordingStopwatch.ElapsedMilliseconds;
            }

            _currentStatus.StatusMessage =
                $"Recording: {TimestampHelper.FormatDuration(_currentStatus.Duration)} " +
                $"| {_currentStatus.FrameCount} frames " +
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
            _ffmpegProcess?.Dispose();
            _cancellationTokenSource?.Dispose();
            
            if (Directory.Exists(_tempFramesPath))
            {
                try { Directory.Delete(_tempFramesPath, true); } catch { }
            }
        }
    }
}
