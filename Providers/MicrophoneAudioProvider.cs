using System;
using System.IO;
using CameraRecordingService.Interfaces;
using NAudio.Wave;

namespace CameraRecordingService.Providers
{
    /// <summary>
    /// Audio capture provider using microphone input via NAudio
    /// </summary>
    public class MicrophoneAudioProvider : IAudioCaptureProvider
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _waveWriter;
        private bool _isCapturing;
        private int _sampleRate;
        private int _channels;

        public bool IsCapturing => _isCapturing;
        public int SampleRate => _sampleRate;
        public int Channels => _channels;

        public bool Initialize(int sampleRate = 48000, int channels = 2)
        {
            try
            {
                _sampleRate = sampleRate;
                _channels = channels;

                // Test if we can create a WaveInEvent (microphone available)
                using (var testWaveIn = new WaveInEvent())
                {
                    if (testWaveIn.DeviceNumber < 0)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void StartCapture(string outputFilePath)
        {
            if (_isCapturing)
                throw new InvalidOperationException("Audio capture is already in progress");

            if (string.IsNullOrWhiteSpace(outputFilePath))
                throw new ArgumentNullException(nameof(outputFilePath));

            try
            {
                // Initialize WaveIn for microphone capture
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(_sampleRate, _channels)
                };

                // Create WAV file writer
                _waveWriter = new WaveFileWriter(outputFilePath, _waveIn.WaveFormat);

                // Hook up the data available event to write to file
                _waveIn.DataAvailable += (sender, e) =>
                {
                    if (_waveWriter != null)
                    {
                        _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
                    }
                };

                // Start recording
                _waveIn.StartRecording();
                _isCapturing = true;
            }
            catch
            {
                // Clean up on error
                _waveWriter?.Dispose();
                _waveWriter = null;
                _waveIn?.Dispose();
                _waveIn = null;
                throw;
            }
        }

        public void StopCapture()
        {
            if (!_isCapturing)
                return;

            try
            {
                _waveIn?.StopRecording();
                _isCapturing = false;

                // Important: Dispose in correct order
                _waveIn?.Dispose();
                _waveIn = null;

                _waveWriter?.Dispose();
                _waveWriter = null;
            }
            catch
            {
                // Ensure cleanup even on error
                _waveIn?.Dispose();
                _waveIn = null;
                _waveWriter?.Dispose();
                _waveWriter = null;
                throw;
            }
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}
