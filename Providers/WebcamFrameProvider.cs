using System;
using System.Threading.Tasks;
using CameraRecordingService.Interfaces;
using OpenCvSharp;

namespace CameraRecordingService.Providers
{
    /// <summary>
    /// Webcam frame provider using OpenCV
    /// </summary>
    public class WebcamFrameProvider : IVideoFrameProvider, IDisposable
    {
        private VideoCapture? _capture;
        private Mat? _currentFrame;
        private readonly int _cameraIndex;
        private bool _isActive;
        private readonly object _lock = new object();

        /// <summary>
        /// Whether the camera is currently active
        /// </summary>
        public bool IsActive => _isActive && _capture != null && _capture.IsOpened();

        /// <summary>
        /// Frame resolution (width, height)
        /// </summary>
        public (int Width, int Height) Resolution
        {
            get
            {
                if (_capture != null && _capture.IsOpened())
                {
                    return ((int)_capture.FrameWidth, (int)_capture.FrameHeight);
                }
                return (0, 0);
            }
        }

        /// <summary>
        /// Create webcam frame provider
        /// </summary>
        /// <param name="cameraIndex">Camera index (0 = default camera)</param>
        public WebcamFrameProvider(int cameraIndex = 0)
        {
            _cameraIndex = cameraIndex;
            _currentFrame = new Mat();
        }

        /// <summary>
        /// Initialize and open the camera
        /// </summary>
        public bool Initialize(int width = 1920, int height = 1080, int fps = 30)
        {
            try
            {
                _capture = new VideoCapture(_cameraIndex);
                
                if (!_capture.IsOpened())
                {
                    return false;
                }

                // Set camera properties
                _capture.Set(VideoCaptureProperties.FrameWidth, width);
                _capture.Set(VideoCaptureProperties.FrameHeight, height);
                _capture.Set(VideoCaptureProperties.Fps, fps);

                _isActive = true;
                return true;
            }
            catch
            {
                _isActive = false;
                return false;
            }
        }

        /// <summary>
        /// Get current frame from camera
        /// </summary>
        public async Task<object?> GetCurrentFrameAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_capture == null || !_capture.IsOpened() || _currentFrame == null)
                    {
                        return null;
                    }

                    try
                    {
                        // Read frame from camera
                        _capture.Read(_currentFrame);

                        if (_currentFrame.Empty())
                        {
                            return null;
                        }

                        // Clone the frame to avoid threading issues
                        return _currentFrame.Clone();
                    }
                    catch
                    {
                        return null;
                    }
                }
            });
        }

        /// <summary>
        /// Release camera resources
        /// </summary>
        public void Dispose()
        {
            _isActive = false;
            
            lock (_lock)
            {
                _currentFrame?.Dispose();
                _currentFrame = null;

                _capture?.Release();
                _capture?.Dispose();
                _capture = null;
            }
        }
    }
}
