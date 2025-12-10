using System;
using System.Windows;
using System.Windows.Threading;
using CameraRecordingService.Interfaces;
using CameraRecordingService.Models;
using CameraRecordingService.Services;
using CameraRecordingService.Enums;
using CameraRecordingService.Providers;

namespace RecordingServiceDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IRecordingService _recordingService;
        private readonly IScreenshotService _screenshotService;
        private readonly WebcamFrameProvider _cameraProvider;
        private readonly ScreenRecordingProvider _screenProvider;
        private DispatcherTimer? _statusUpdateTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize services - SIMPLE approach, no FFmpeg
            _recordingService = new SimpleRecordingService();
            _screenshotService = new ScreenshotService();
            
            // Initialize both camera and screen providers
            _cameraProvider = new WebcamFrameProvider(0); // 0 = default camera
            _screenProvider = new ScreenRecordingProvider();
            
            // Initialize webcam
            bool cameraInitialized = _cameraProvider.Initialize(1280, 720, 30); // Lower resolution for better compatibility
            
            // Initialize screen capture
            bool screenInitialized = _screenProvider.Initialize(1920, 1080); // Full HD for screen
            
            // Update footer based on initialization
            if (cameraInitialized && screenInitialized)
            {
                var camRes = _cameraProvider.Resolution;
                var scrRes = _screenProvider.Resolution;
                FooterText.Text = $"? Camera: {camRes.Width}x{camRes.Height} | Screen: {scrRes.Width}x{scrRes.Height}";
            }
            else if (cameraInitialized)
            {
                var res = _cameraProvider.Resolution;
                FooterText.Text = $"? Camera ready: {res.Width}x{res.Height} | ?? Screen capture unavailable";
            }
            else if (screenInitialized)
            {
                var res = _screenProvider.Resolution;
                FooterText.Text = $"?? Camera unavailable | ? Screen ready: {res.Width}x{res.Height}";
            }
            else
            {
                MessageBox.Show("Failed to initialize both camera and screen capture.\n\nPlease check your system.", 
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                FooterText.Text = "?? No recording sources available";
            }
            
            // Subscribe to events
            _recordingService.OnRecordingStatusChanged += RecordingService_OnRecordingStatusChanged;
            _recordingService.OnRecordingError += RecordingService_OnRecordingError;
            _recordingService.OnRecordingCompleted += RecordingService_OnRecordingCompleted;
            
            _screenshotService.OnScreenshotTaken += ScreenshotService_OnScreenshotTaken;
            _screenshotService.OnScreenshotError += ScreenshotService_OnScreenshotError;
            
            // Setup status update timer
            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
        }

        private async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Determine which source to use
                IVideoFrameProvider frameProvider;
                string sourceType;
                
                if (CameraSourceRadio.IsChecked == true)
                {
                    frameProvider = _cameraProvider;
                    sourceType = "Camera";
                }
                else
                {
                    frameProvider = _screenProvider;
                    sourceType = "Screen";
                }
                
                // Calculate appropriate bitrate based on resolution
                // Keep within valid range: 6220 - 50000 kbps
                int width = frameProvider.Resolution.Width;
                int height = frameProvider.Resolution.Height;
                
                // Use reasonable bitrate: ~0.15 bits per pixel for good quality
                int calculatedBitrate = (width * height * 30 * 15) / 100000; // in kbps
                int bitrate = Math.Clamp(calculatedBitrate, 8000, 20000); // 8-20 Mbps range
                
                var config = new RecordingConfig
                {
                    OutputPath = RecordingOutputPath.Text,
                    FileName = RecordingFileName.Text,
                    VideoCodec = VideoCodec.H264,
                    Width = width,
                    Height = height,
                    FramesPerSecond = sourceType == "Screen" ? 10 : 30, // Lower FPS for screen capture
                    Bitrate = bitrate, // Safe bitrate within valid range
                    EnableAudio = false
                };

                bool started = await _recordingService.StartRecordingAsync(frameProvider, config);
                
                if (started)
                {
                    StartRecordingButton.IsEnabled = false;
                    StopRecordingButton.IsEnabled = true;
                    CameraSourceRadio.IsEnabled = false;
                    ScreenSourceRadio.IsEnabled = false;
                    _statusUpdateTimer?.Start();
                    
                    FooterText.Text = $"?? Recording {sourceType}...";
                }
                else
                {
                    MessageBox.Show("Failed to start recording. Check output path and permissions.", 
                        "Recording Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting recording: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopRecordingButton.IsEnabled = false;
                
                // Show processing message
                FooterText.Text = "⏳ Processing video... Please wait (this may take a few seconds)";
                
                // Stop recording (this will trigger FFmpeg re-encoding)
                var filePath = await _recordingService.StopRecordingAsync();
                
                StartRecordingButton.IsEnabled = true;
                CameraSourceRadio.IsEnabled = true;
                ScreenSourceRadio.IsEnabled = true;
                _statusUpdateTimer?.Stop();
                
                MessageBox.Show($"Recording saved to:\n{filePath}\n\nFormat: MP4 (H.264 codec)\nPlayable on all devices!", 
                    "Recording Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                
                FooterText.Text = $"✓ Recording saved: {filePath}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping recording: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                
                StartRecordingButton.IsEnabled = true;
                StopRecordingButton.IsEnabled = false;
                CameraSourceRadio.IsEnabled = true;
                ScreenSourceRadio.IsEnabled = true;
                _statusUpdateTimer?.Stop();
            }
        }

        private async void TakeScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = new ScreenshotConfig
                {
                    OutputPath = ScreenshotOutputPath.Text,
                    FileName = ScreenshotFileName.Text,
                    ImageFormat = ImageFormat.PNG,
                    AddTimestamp = true
                };

                // Use camera for screenshots
                var result = await _screenshotService.TakeScreenshotAsync(_cameraProvider, config);
                
                if (result.Success)
                {
                    ScreenshotStatusText.Text = $"? Saved: {result.FilePath}\nSize: {result.FileSize} bytes\nTime: {result.Timestamp:HH:mm:ss}";
                    FooterText.Text = $"Screenshot saved: {result.FilePath}";
                }
                else
                {
                    ScreenshotStatusText.Text = $"? Failed: {result.ErrorMessage}";
                    MessageBox.Show($"Screenshot failed: {result.ErrorMessage}", 
                        "Screenshot Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error taking screenshot: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StatusUpdateTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var status = await _recordingService.GetRecordingStatusAsync();
                UpdateRecordingStatus(status);
            }
            catch
            {
                // Ignore errors during status update
            }
        }

        private void RecordingService_OnRecordingStatusChanged(object? sender, CameraRecordingService.Events.RecordingEventArgs e)
        {
            Dispatcher.Invoke(() => UpdateRecordingStatus(e.Status));
        }

        private void RecordingService_OnRecordingError(object? sender, CameraRecordingService.Events.RecordingErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Recording error: {e.ErrorMessage}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                FooterText.Text = $"Error: {e.ErrorMessage}";
            });
        }

        private void RecordingService_OnRecordingCompleted(object? sender, CameraRecordingService.Events.RecordingEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                FooterText.Text = "Recording completed successfully";
            });
        }

        private void ScreenshotService_OnScreenshotTaken(object? sender, CameraRecordingService.Events.ScreenshotEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                FooterText.Text = $"Screenshot saved: {e.Result.FilePath}";
            });
        }

        private void ScreenshotService_OnScreenshotError(object? sender, CameraRecordingService.Events.RecordingErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                FooterText.Text = $"Screenshot error: {e.ErrorMessage}";
            });
        }

        private void UpdateRecordingStatus(RecordingStatus status)
        {
            RecordingStatusText.Text = status.IsRecording ? "?? Recording..." : "Ready";
            RecordingDurationText.Text = $"Duration: {status.Duration:hh\\:mm\\:ss}";
            RecordingFrameCountText.Text = $"Frames: {status.FrameCount}";
            RecordingFpsText.Text = $"FPS: {status.CurrentFPS:F1}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _statusUpdateTimer?.Stop();
            _cameraProvider?.Dispose();
            _screenProvider?.Dispose();
            
            if (_recordingService is IDisposable disposableRecording)
            {
                disposableRecording.Dispose();
            }
            
            base.OnClosed(e);
        }
    }
}
