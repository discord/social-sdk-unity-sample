using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Receives raw PCM audio from the Discord Social SDK and plays it through a
/// spatial AudioSource on the same GameObject.
///
/// Call FeedSamples() from the Discord UserAudioReceivedCallback.
/// Unity's audio thread drains the ring buffer via the streaming AudioClip callback.
///
/// Add this component to a remote player GameObject with an AudioSource.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VoiceAudioSource : MonoBehaviour
{
    private const int SampleRate = 48000;
    private const int RingBufferSamples = SampleRate * 2; // 2-second ring buffer
    private const float PcmNormalizationFactor = 1 / 32768f; // scaling factor for int16 to float conversion
    private const int FrameSamples = 960; // 20ms at 48kHz
    private const int MaxChannels = 2;
    private float[] _ringBuffer;
    private readonly short[] _shortBuffer = new short[FrameSamples * MaxChannels];
    private int _writePosition;
    private int _readPosition;
    private readonly object _lock = new object();

    private AudioSource _audioSource;

    void Awake()
    {
        _ringBuffer = new float[RingBufferSamples];

        _audioSource = GetComponent<AudioSource>();
        // Streaming mono clip — OnPCMRead is called by Unity's audio thread to pull samples
        _audioSource.clip = AudioClip.Create("VoiceClip", SampleRate, 1, SampleRate, true, OnPCMRead);
        _audioSource.loop = true;
        _audioSource.spatialBlend = 1f; // full 3D positioning
        _audioSource.Play();
    }

    // Feed raw int16 PCM samples received from the Discord audio callback.
    public void FeedSamples(IntPtr data, ulong samplesPerChannel, ulong channels)
    {
        if (data == IntPtr.Zero || samplesPerChannel == 0) return;

        int channelCount = (int)channels;
        int totalSamples = (int)samplesPerChannel * channelCount;

        Marshal.Copy(data, _shortBuffer, 0, totalSamples);

        lock (_lock)
        {
            for (int i = 0; i < (int)samplesPerChannel; i++)
            {
                // Mix down to mono for spatial playback
                float mono = 0f;
                for (int c = 0; c < channelCount; c++)
                {
                    mono += _shortBuffer[i * channelCount + c] * PcmNormalizationFactor;
                }
                mono /= channelCount;

                _ringBuffer[_writePosition] = mono;
                _writePosition = (_writePosition + 1) % RingBufferSamples;
            }
        }
    }

    // Called by Unity's audio thread to fill the next block of samples
    private void OnPCMRead(float[] data)
    {
        lock (_lock)
        {
            int available = (_writePosition - _readPosition + RingBufferSamples) % RingBufferSamples;

            for (int i = 0; i < data.Length; i++)
            {
                if (available > 0)
                {
                    data[i] = _ringBuffer[_readPosition];
                    _readPosition = (_readPosition + 1) % RingBufferSamples;
                    available--;
                }
                else
                {
                    data[i] = 0f; // silence when buffer is empty
                }
            }
        }
    }
}
