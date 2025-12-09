using System;
namespace CameraRecordingService.Exceptions
{
    /// <summary>
    /// Base exception for screenshot-related errors
    /// </summary>
    public class ScreenshotException : Exception
    {
        public ScreenshotException() : base() { }
        public ScreenshotException(string message) : base(message) { }
        public ScreenshotException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when screenshot configuration is invalid
    /// </summary>
    public class InvalidScreenshotConfigException : ScreenshotException
    {
        public InvalidScreenshotConfigException(string field, string reason) 
            : base($"Invalid screenshot configuration - {field}: {reason}") { }
    }

    /// <summary>
    /// Exception thrown when image format is not supported
    /// </summary>
    public class ImageFormatNotSupportedException : ScreenshotException
    {
        public ImageFormatNotSupportedException(Enums.ImageFormat format) 
            : base($"Image format {format} is not supported") { }
    }

    /// <summary>
    /// Exception thrown when camera frame is unavailable
    /// </summary>
    public class CameraFrameUnavailableException : ScreenshotException
    {
        public CameraFrameUnavailableException() : base("Camera frame is not available") { }
    }

    /// <summary>
    /// Exception thrown when file write fails
    /// </summary>
    public class FileWriteException : ScreenshotException
    {
        public FileWriteException(string path, Exception innerException) 
            : base($"Failed to write file: {path}", innerException) { }
    }

    /// <summary>
    /// Exception thrown when screenshot save fails
    /// </summary>
    public class ScreenshotSaveFailedException : ScreenshotException
    {
        public ScreenshotSaveFailedException(string reason) 
            : base($"Failed to save screenshot: {reason}") { }
    }
}
