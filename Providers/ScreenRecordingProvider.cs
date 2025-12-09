using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CameraRecordingService.Interfaces;
using OpenCvSharp;

namespace CameraRecordingService.Providers
{
    /// <summary>
    /// Screen recording provider using Windows GDI
    /// </summary>
    public class ScreenRecordingProvider : IVideoFrameProvider, IDisposable
    {
        private bool _isActive;
        private int _screenWidth;
        private int _screenHeight;
        private readonly object _lock = new object();

        /// <summary>
        /// Whether screen capture is active
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Screen resolution
        /// </summary>
        public (int Width, int Height) Resolution => (_screenWidth, _screenHeight);

        /// <summary>
        /// Initialize screen capture
        /// </summary>
        public bool Initialize()
        {
            try
            {
                // Get primary screen dimensions
                _screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
                _screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
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
        /// Initialize with custom resolution (will scale screen capture)
        /// </summary>
        public bool Initialize(int width, int height)
        {
            try
            {
                _screenWidth = width;
                _screenHeight = height;
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
        /// Capture current screen frame
        /// </summary>
        public async Task<object?> GetCurrentFrameAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_isActive)
                        return null;

                    try
                    {
                        // Get screen bounds
                        var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;

                        // Create bitmap to hold screen capture
                        using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
                        {
                            // Capture screen
                            using (var graphics = Graphics.FromImage(bitmap))
                            {
                                graphics.CopyFromScreen(
                                    bounds.X, bounds.Y,
                                    0, 0,
                                    bounds.Size,
                                    CopyPixelOperation.SourceCopy
                                );
                            }

                            // Convert to OpenCV Mat
                            var mat = BitmapToMat(bitmap);

                            // Resize if needed
                            if (bounds.Width != _screenWidth || bounds.Height != _screenHeight)
                            {
                                var resized = new Mat();
                                Cv2.Resize(mat, resized, new OpenCvSharp.Size(_screenWidth, _screenHeight));
                                mat.Dispose();
                                return resized;
                            }

                            return mat;
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }
            });
        }

        /// <summary>
        /// Convert System.Drawing.Bitmap to OpenCV Mat
        /// </summary>
        private Mat BitmapToMat(Bitmap bitmap)
        {
            var mat = new Mat(bitmap.Height, bitmap.Width, MatType.CV_8UC3);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb
            );

            try
            {
                // Copy bitmap data to Mat
                var dataPtr = mat.Data;
                var stride = bitmapData.Stride;
                var scan0 = bitmapData.Scan0;

                unsafe
                {
                    byte* srcPtr = (byte*)scan0;
                    byte* dstPtr = (byte*)dataPtr;

                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        byte* srcRow = srcPtr + (y * stride);
                        byte* dstRow = dstPtr + (y * mat.Width * 3);

                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            // BGR format (OpenCV uses BGR, Bitmap uses BGR too)
                            dstRow[x * 3 + 0] = srcRow[x * 3 + 0]; // B
                            dstRow[x * 3 + 1] = srcRow[x * 3 + 1]; // G
                            dstRow[x * 3 + 2] = srcRow[x * 3 + 2]; // R
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return mat;
        }

        public void Dispose()
        {
            _isActive = false;
        }
    }
}
