using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CameraRecordingService.Config;
using CameraRecordingService.Events;
using CameraRecordingService.Exceptions;
using CameraRecordingService.Helpers;
using CameraRecordingService.Interfaces;
using CameraRecordingService.Models;
using OpenCvSharp;
using Xabe.FFmpeg;

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
        
        // Audio capture
        private IAudioCaptureProvider? _audioProvider;
        private string _audioFilePath = string.Empty;
        
        // Pause/Resume support
        private bool _isPaused;
        private DateTime? _pauseStartTime;
        private TimeSpan _totalPausedDuration;
        private List<string> _audioSegments = new List<string>();
        private int _audioSegmentIndex = 0;

        // Events
        public event EventHandler<RecordingEventArgs>? OnRecordingStatusChanged;
        public event EventHandler<RecordingErrorEventArgs>? OnRecordingError;
        public event EventHandler<RecordingEventArgs>? OnRecordingCompleted;

        public bool IsRecording => _isRecording;

        public FinalRecordingService(IAudioCaptureProvider? audioProvider = null)
        {
            _isRecording = false;
            _currentStatus = new RecordingStatus();
            _audioProvider = audioProvider;
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
                _isPaused = false;
                _pauseStartTime = null;
                _totalPausedDuration = TimeSpan.Zero;
                _audioSegments.Clear();
                _audioSegmentIndex = 0;

                // Start audio capture FIRST if enabled (before video to ensure sync)
                if (config.EnableAudio && _audioProvider != null)
                {
                    try 
                    {
                        var audioDir = Path.GetDirectoryName(_outputFilePath) ?? "";
                        // Ensure directory exists for audio file too
                        FilePathHelper.EnsureDirectoryExists(audioDir);
                        
                        _audioFilePath = Path.Combine(
                            audioDir,
                            Path.GetFileNameWithoutExtension(_outputFilePath) + "_audio_0.wav");
                        
                        _audioSegments.Add(_audioFilePath);
                        _audioProvider.StartCapture(_audioFilePath);
                    }
                    catch (Exception ex)
                    {
                        // Fallback: Disable audio if capture fails start
                        config.EnableAudio = false;
                        _audioSegments.Clear();
                        // Log warning (non-fatal)
                        RaiseRecordingError($"Audio capture failed to start: {ex.Message}. Continuation without audio.", ex);
                    }
                }

                // Then start video recording (ensures audio and video start together)
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

                // Stop audio capture if it was running
                if (_currentConfig?.EnableAudio == true && _audioProvider != null)
                {
                    try {
                         _audioProvider.StopCapture();
                    } catch { /* Ignore stop errors if audio failed midway */ }
                }

                _isRecording = false;

                // Calculate ACTUAL FPS from ACTIVE recording time (exclude paused time)
                var actualRecordingTime = _recordingStopwatch.Elapsed - _totalPausedDuration;
                double actualFPS = _frameCount / actualRecordingTime.TotalSeconds;
                
                // Write video with ACTUAL FPS
                string videoOnlyPath = _outputFilePath.Replace(".mp4", "_video.mp4");
                await WriteVideoWithActualFPS(videoOnlyPath, actualFPS);
                
                // Mux audio and video if audio was enabled
                if (_currentConfig?.EnableAudio == true && _audioSegments.Count > 0)
                {
                    // Merge all audio segments first if multiple
                    string finalAudioPath = _audioSegments[0];
                    
                    if (_audioSegments.Count > 1)
                    {
                        finalAudioPath = _outputFilePath.Replace(".mp4", "_audio_merged.wav");
                        await MergeAudioSegmentsAsync(_audioSegments, finalAudioPath);
                    }
                    
                    if (File.Exists(finalAudioPath))
                    {
                        await MuxAudioVideoAsync(videoOnlyPath, finalAudioPath, _outputFilePath);
                        
                        // Clean up temporary files
                        try
                        {
                            File.Delete(videoOnlyPath);
                            
                            // Delete all audio segments
                            foreach (var segment in _audioSegments)
                            {
                                if (File.Exists(segment))
                                    File.Delete(segment);
                            }
                            
                            // Delete merged audio if it was created
                            if (_audioSegments.Count > 1 && File.Exists(finalAudioPath))
                                File.Delete(finalAudioPath);
                        }
                        catch { /* Ignore cleanup errors */ }
                    }
                }
                else
                {
                    // No audio, just rename video file to final output
                    if (File.Exists(videoOnlyPath))
                    {
                        File.Move(videoOnlyPath, _outputFilePath, overwrite: true);
                    }
                }
                
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

        private async Task MuxAudioVideoAsync(string videoPath, string audioPath, string outputPath)
        {
            try
            {
                // Get media info
                var videoInfo = await FFmpeg.GetMediaInfo(videoPath);
                var audioInfo = await FFmpeg.GetMediaInfo(audioPath);

                var videoStream = videoInfo.VideoStreams.FirstOrDefault();
                var audioStream = audioInfo.AudioStreams.FirstOrDefault();

                if (videoStream == null)
                    throw new InvalidOperationException("No video stream found");

                if (audioStream == null)
                    throw new InvalidOperationException("No audio stream found");

                // Create conversion with both streams and sync parameters
                var conversion = FFmpeg.Conversions.New()
                    .AddStream(videoStream)
                    .AddStream(audioStream)
                    .AddParameter("-async 1")  // Audio sync method
                    .AddParameter("-vsync cfr")  // Constant frame rate for video
                    .SetOutput(outputPath)
                    .SetOverwriteOutput(true);

                // Execute the muxing
                await conversion.Start();
            }
            catch (Exception ex)
            {
                // If muxing fails, just use the video file
                if (File.Exists(videoPath))
                {
                    File.Copy(videoPath, outputPath, overwrite: true);
                }
                
                RaiseRecordingError($"Audio muxing failed: {ex.Message}. Video saved without audio.", ex);
            }
        }

        private async Task MergeAudioSegmentsAsync(List<string> audioSegments, string outputPath)
        {
            try
            {
                // Create FFmpeg concat command
                var conversion = FFmpeg.Conversions.New();
                
                // Add all input files
                foreach (var segment in audioSegments)
                {
                    conversion.AddParameter($"-i \"{segment}\"");
                }
                
                // Use concat filter to merge audio segments
                var filterComplex = $"concat=n={audioSegments.Count}:v=0:a=1[outa]";
                conversion.AddParameter($"-filter_complex \"{filterComplex}\"");
                conversion.AddParameter("-map \"[outa]\"");
                conversion.SetOutput(outputPath);
                conversion.SetOverwriteOutput(true);
                
                await conversion.Start();
            }
            catch (Exception ex)
            {
                // If merge fails, just use the first segment
                if (File.Exists(audioSegments[0]))
                {
                    File.Copy(audioSegments[0], outputPath, overwrite: true);
                }
                else
                {
                    throw new InvalidOperationException($"Failed to merge audio segments: {ex.Message}", ex);
                }
            }
        }

        private async Task RecordingTaskAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Capture frames as fast as possible
                while (!cancellationToken.IsCancellationRequested && _isRecording)
                {
                    // Skip frame capture when paused
                    if (_isPaused)
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

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
            try
            {
                if (!_isRecording || _isPaused)
                    return false;

                _isPaused = true;
                _pauseStartTime = DateTime.Now;
                
                // Stop audio during pause to match video duration
                if (_currentConfig?.EnableAudio == true && _audioProvider != null && _audioProvider.IsCapturing)
                {
                    _audioProvider.StopCapture();
                }

                RaiseRecordingStatusChanged();
                return await Task.FromResult(true);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ResumeRecordingAsync()
        {
            try
            {
                if (!_isRecording || !_isPaused)
                    return false;

                // Add the pause duration to total
                if (_pauseStartTime.HasValue)
                {
                    _totalPausedDuration += DateTime.Now - _pauseStartTime.Value;
                    _pauseStartTime = null;
                }

                _isPaused = false;
                
                // Resume audio - create new segment
                if (_currentConfig?.EnableAudio == true && _audioProvider != null)
                {
                    _audioSegmentIndex++;
                    var newSegmentPath = Path.Combine(
                        Path.GetDirectoryName(_outputFilePath) ?? "",
                        Path.GetFileNameWithoutExtension(_outputFilePath) + $"_audio_{_audioSegmentIndex}.wav");
                    
                    _audioSegments.Add(newSegmentPath);
                    _audioProvider.StartCapture(newSegmentPath);
                }

                RaiseRecordingStatusChanged();
                return await Task.FromResult(true);
            }
            catch
            {
                return false;
            }
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
            
            // Calculate display duration
            var displayDuration = _recordingStopwatch.Elapsed - _totalPausedDuration;
            
            // If currently paused, subtract the current pause duration
            if (_isPaused && _pauseStartTime.HasValue)
            {
                displayDuration -= (DateTime.Now - _pauseStartTime.Value);
            }
            
            _currentStatus.Duration = displayDuration;
            _currentStatus.FrameCount = _frameCount;
            _currentStatus.UpdatedAt = DateTime.Now;

            if (_recordingStopwatch.ElapsedMilliseconds > 0)
            {
                _currentStatus.CurrentFPS = (_frameCount * 1000.0) / _recordingStopwatch.ElapsedMilliseconds;
            }

            // Estimate file size based on frames
            _currentStatus.FileSize = _frameCount * 100000; // Rough estimate

            if (_isPaused)
            {
                _currentStatus.StatusMessage = $"⏸ Paused: {TimestampHelper.FormatDuration(_currentStatus.Duration)} | {_frameCount} frames";
            }
            else
            {
                _currentStatus.StatusMessage =
                    $"Recording: {TimestampHelper.FormatDuration(_currentStatus.Duration)} " +
                    $"| {_frameCount} frames " +
                    $"| {_currentStatus.CurrentFPS:F1} FPS";
            }

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
            _audioProvider?.Dispose();
            
            // Clean up any remaining frames
            foreach (var frame in _capturedFrames)
            {
                frame?.Dispose();
            }
            _capturedFrames.Clear();
        }
    }
}
