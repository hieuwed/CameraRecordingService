using System;
using System.IO;
using System.Threading.Tasks;
using CameraRecordingService.Interfaces;
using CameraRecordingService.Models;
using OpenCvSharp;

namespace CameraRecordingService.Services
{
    /// <summary>
    /// Video encoding service using OpenCV VideoWriter
    /// </summary>
    public class VideoEncodingService : IDisposable
    {
        private VideoWriter? _videoWriter;
        private bool _isEncoding;
        private readonly object _lock = new object();

        /// <summary>
        /// Whether encoding is currently active
        /// </summary>
        public bool IsEncoding => _isEncoding;

        /// <summary>
        /// Initialize video writer
        /// </summary>
        public bool Initialize(string outputPath, int width, int height, double fps, Enums.VideoCodec codec)
        {
            try
            {
                // Map our codec enum to OpenCV FourCC
                // Use MP4V for MP4 container - most compatible
                int fourcc = codec switch
                {
                    Enums.VideoCodec.MJPEG => VideoWriter.FourCC('M', 'J', 'P', 'G'),
                    Enums.VideoCodec.H264 => VideoWriter.FourCC('m', 'p', '4', 'v'), // MP4V - works with MP4
                    Enums.VideoCodec.H265 => VideoWriter.FourCC('m', 'p', '4', 'v'), // Fallback to MP4V
                    _ => VideoWriter.FourCC('m', 'p', '4', 'v') // Default to MP4V
                };

                _videoWriter = new VideoWriter(
                    outputPath,
                    fourcc,
                    fps,
                    new Size(width, height),
                    isColor: true
                );

                if (!_videoWriter.IsOpened())
                {
                    _videoWriter?.Dispose();
                    _videoWriter = null;
                    return false;
                }

                _isEncoding = true;
                return true;
            }
            catch
            {
                _videoWriter?.Dispose();
                _videoWriter = null;
                _isEncoding = false;
                return false;
            }
        }

        /// <summary>
        /// Write a frame to the video file
        /// </summary>
        public async Task<bool> WriteFrameAsync(object frame)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_videoWriter == null || !_videoWriter.IsOpened())
                    {
                        return false;
                    }

                    try
                    {
                        if (frame is Mat mat && !mat.Empty())
                        {
                            _videoWriter.Write(mat);
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }
                }
            });
        }

        /// <summary>
        /// Finalize and close the video file
        /// </summary>
        public void Finalize()
        {
            lock (_lock)
            {
                _isEncoding = false;
                _videoWriter?.Release();
                _videoWriter?.Dispose();
                _videoWriter = null;
            }
        }

        public void Dispose()
        {
            Finalize();
        }
    }
}
