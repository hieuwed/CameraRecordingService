using System;
namespace CameraRecordingService.Exceptions
{
    /// <summary>
    /// Base exception for recording-related errors
    /// </summary>
    public class RecordingException : Exception
    {
        public RecordingException() : base() { }
        public RecordingException(string message) : base(message) { }
        public RecordingException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when trying to start recording while already recording
    /// </summary>
    public class RecordingAlreadyInProgressException : RecordingException
    {
        public RecordingAlreadyInProgressException() : base("Recording is already in progress") { }
    }

    /// <summary>
    /// Exception thrown when trying to stop recording that hasn't started
    /// </summary>
    public class RecordingNotStartedException : RecordingException
    {
        public RecordingNotStartedException() : base("Recording has not been started") { }
    }

    /// <summary>
    /// Exception thrown when codec is not supported
    /// </summary>
    public class CodecNotSupportedException : RecordingException
    {
        public CodecNotSupportedException(Enums.VideoCodec codec) 
            : base($"Codec {codec} is not supported on this system") { }
    }

    /// <summary>
    /// Exception thrown when recording configuration is invalid
    /// </summary>
    public class InvalidRecordingConfigException : RecordingException
    {
        public InvalidRecordingConfigException(string field, string reason) 
            : base($"Invalid recording configuration - {field}: {reason}") { }
    }

    /// <summary>
    /// Exception thrown when disk space is insufficient
    /// </summary>
    public class DiskSpaceInsufficientException : RecordingException
    {
        public DiskSpaceInsufficientException(long required, long available) 
            : base($"Insufficient disk space. Required: {required} bytes, Available: {available} bytes") { }
    }

    /// <summary>
    /// Exception thrown when camera is disconnected during recording
    /// </summary>
    public class CameraDisconnectedException : RecordingException
    {
        public CameraDisconnectedException() : base("Camera was disconnected") { }
    }

    /// <summary>
    /// Exception thrown when Media Foundation encounters an error
    /// </summary>
    public class MediaFoundationException : RecordingException
    {
        public MediaFoundationException(string message) : base($"Media Foundation error: {message}") { }
    }
}
