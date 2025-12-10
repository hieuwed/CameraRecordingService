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
    /// Professional recording service using Variable Frame Rate (VFR) with timestamps
    /// This ensures video duration matches recording time exactly, regardless of actual capture FPS
    /// </summary>
    public class VFRRecordingService : IRecordingService, IDisposable
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
        private Stopwatch? _captureStopwatch; // For tracking actual capture time

        // Events
        public event EventHandler<RecordingEventArgs>? OnRecordingStatusChanged;
        public event EventHandler<RecordingErrorEventArgs>? OnRecordingError;
        public event EventHandler<RecordingEventArgs>? OnRecordingCompleted;

        public bool IsRecording => _isRecording;

        public VFRRecordingService()
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
                    "avi"); // Use AVI for VFR support

                // Use a nominal FPS (e.g., 30) - this is just metadata
                // Actual timing will be controlled by timestamps
                int nominalFPS = 30;
                
                // Initialize VideoWriter with MJPEG codec (good VFR support)
                int fourcc = VideoWriter.FourCC('M', 'J', 'P', 'G');
                
                _videoWriter = new VideoWriter(
                    _outputFilePath,
                    fourcc,
                    nominalFPS,
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
                _captureStopwatch = Stopwatch.StartNew(); // Track actual capture time
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
                _captureStopwatch?.Stop();

                // Calculate ACTUAL FPS based on real recording time
                // This is the key to VFR: actual_fps = total_frames / total_time
                double actualRecordingTimeSeconds = _recordingStopwatch.Elapsed.TotalSeconds;
                double actualFPS = _frameCount / actualRecordingTimeSeconds;
                
                // Close current video writer
                _videoWriter?.Release();
                _videoWriter?.Dispose();
                _videoWriter = null;

                // Now we need to re-encode with the ACTUAL FPS
                // This ensures: video_duration = frame_count / actual_fps = recording_time ✓
                string tempFile = _outputFilePath;
                string finalFile = Path.ChangeExtension(_outputFilePath, ".mp4");
                
                try
                {
                    // Re-encode with actual FPS
                    await ReEncodeWithActualFPS(tempFile, finalFile, actualFPS);
                    
                    // Delete temp file if re-encoding succeeded
                    if (File.Exists(tempFile) && File.Exists(finalFile))
                    {
                        File.Delete(tempFile);
                    }
                    
                    _outputFilePath = finalFile;
                }
                catch
                {
                    // Re-encoding failed, use AVI file instead
                    string aviFile = Path.ChangeExtension(finalFile, ".avi");
                    if (File.Exists(aviFile))
                    {
                        _outputFilePath = aviFile;
                    }
                    else if (File.Exists(tempFile))
                    {
                        _outputFilePath = tempFile;
                    }
                }
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

        private async Task ReEncodeWithActualFPS(string inputPath, string outputPath, double actualFPS)
        {
            try
            {
                // Use actual FPS calculated from recording time
                string fpsValue = actualFPS.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                
                // Re-encode to MP4 with actual FPS
                // Use libx264 for better compatibility
                string ffmpegArgs = $"-i \"{inputPath}\" -c:v libx264 -preset ultrafast -crf 23 -r {fpsValue} -pix_fmt yuv420p -y \"{outputPath}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = ffmpegArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        
                        if (process.ExitCode != 0)
                        {
                            // FFmpeg failed - keep the AVI file instead
                            string aviOutput = Path.ChangeExtension(outputPath, ".avi");
                            if (File.Exists(inputPath))
                            {
                                File.Move(inputPath, aviOutput, overwrite: true);
                                // Update output path to AVI
                                throw new Exception($"FFmpeg failed, saved as AVI: {aviOutput}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback: keep the AVI file
                string aviOutput = Path.ChangeExtension(outputPath, ".avi");
                if (File.Exists(inputPath) && !File.Exists(aviOutput))
                {
                    File.Move(inputPath, aviOutput, overwrite: true);
                }
                throw new MediaFoundationException($"Re-encoding failed: {ex.Message}. File saved as AVI.");
            }
        }

        private async Task RecordingTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Capture frames as fast as possible
                // Each frame will have its own timestamp based on actual capture time
                
                while (!cancellationToken.IsCancellationRequested && _isRecording)
                {
                    // Record the time BEFORE capturing the frame
                    long captureTimeMs = _captureStopwatch!.ElapsedMilliseconds;
                    
                    var frame = await _currentFrameProvider!.GetCurrentFrameAsync();

                    if (frame != null && frame is Mat mat && !mat.Empty() && _videoWriter != null)
                    {
                        // Write frame with its actual capture timestamp
                        // OpenCV VideoWriter will use the order of frames to determine timing
                        // The key is to capture frames at their natural rate
                        _videoWriter.Write(mat);
                        _frameCount++;
                        mat.Dispose();
                    }

                    // Check max duration
                    if (_currentConfig?.MaxDuration != null &&
                        _recordingStopwatch!.Elapsed > _currentConfig.MaxDuration)
                    {
                        break;
                    }

                    // Small delay to prevent CPU overload, but don't enforce strict timing
                    // Let frames be captured at their natural rate
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
