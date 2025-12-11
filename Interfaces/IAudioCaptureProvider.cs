using System;

namespace CameraRecordingService.Interfaces
{
    /// <summary>
    /// Interface for audio capture providers
    /// </summary>
    public interface IAudioCaptureProvider : IDisposable
    {
        /// <summary>
        /// Initialize the audio capture device
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz (e.g., 48000)</param>
        /// <param name="channels">Number of channels (1 = mono, 2 = stereo)</param>
        /// <returns>True if initialization succeeded</returns>
        bool Initialize(int sampleRate = 48000, int channels = 2);

        /// <summary>
        /// Start capturing audio to the specified file path
        /// </summary>
        /// <param name="outputFilePath">Path to save the audio file (WAV format)</param>
        void StartCapture(string outputFilePath);

        /// <summary>
        /// Stop capturing audio
        /// </summary>
        void StopCapture();

        /// <summary>
        /// Whether audio is currently being captured
        /// </summary>
        bool IsCapturing { get; }

        /// <summary>
        /// Sample rate in Hz
        /// </summary>
        int SampleRate { get; }

        /// <summary>
        /// Number of audio channels
        /// </summary>
        int Channels { get; }
    }
}
