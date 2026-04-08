using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Receives raw PCM audio from the Discord Social SDK and plays it through a
/// spatial AudioSource on the same GameObject.
///
/// Call FeedSamples() from the Discord UserAudioReceivedCallback (any thread).
/// Unity's audio thread drains the ring buffer via the streaming AudioClip callback.
///
/// Add this component to the RemotePlayer prefab alongside an AudioSource.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VoiceAudioSource : MonoBehaviour
{
    private const int SampleRate = 48000;
    private const int RingBufferSamples = SampleRate * 2; // 2-second ring buffer
    // Target buffer depth: how many samples to keep ahead of the read pointer.
    // Lower = less latency, but more risk of underrun glitches on a slow machine.
    private const int TargetBufferSamples = 2400; // 50ms

    private float[] _ring;
    private int _writePos;
    private int _readPos;
    private readonly object _lock = new object();

    private AudioSource _audioSource;

    void Awake()
    {
        _ring = new float[RingBufferSamples];

        _audioSource = GetComponent<AudioSource>();
        // Streaming mono clip — OnPCMRead is called by Unity's audio thread to pull samples
        _audioSource.clip = AudioClip.Create("VoiceClip", SampleRate, 1, SampleRate, true, OnPCMRead);
        _audioSource.loop = true;
        _audioSource.spatialBlend = 1f; // full 3D positioning
        _audioSource.Play();
    }

    /// <summary>
    /// Feed raw int16 PCM samples received from the Discord audio callback.
    /// Thread-safe; may be called from any thread.
    /// </summary>
    public void FeedSamples(IntPtr data, ulong samplesPerChannel, int sampleRate, ulong channels)
    {
        if (data == IntPtr.Zero || samplesPerChannel == 0) return;

        int chans = (int)channels;
        int totalSamples = (int)samplesPerChannel * chans;

        short[] shorts = new short[totalSamples];
        Marshal.Copy(data, shorts, 0, totalSamples);

        lock (_lock)
        {
            for (int i = 0; i < (int)samplesPerChannel; i++)
            {
                // Mix down to mono for spatial playback
                float mono = 0f;
                for (int c = 0; c < chans; c++)
                    mono += shorts[i * chans + c] / 32768f;
                mono /= chans;

                _ring[_writePos] = mono;
                _writePos = (_writePos + 1) % RingBufferSamples;
            }
        }
    }

    // Called by Unity's audio thread to fill the next block of samples
    private void OnPCMRead(float[] data)
    {
        lock (_lock)
        {
            int available = (_writePos - _readPos + RingBufferSamples) % RingBufferSamples;

            // If the buffer has drifted beyond the target depth, skip ahead.
            // This keeps latency from compounding over the session at the cost
            // of a brief glitch — better than ever-increasing delay.
            if (available > TargetBufferSamples)
            {
                int excess = available - TargetBufferSamples;
                _readPos = (_readPos + excess) % RingBufferSamples;
                available = TargetBufferSamples;
            }

            for (int i = 0; i < data.Length; i++)
            {
                if (available > 0)
                {
                    data[i] = _ring[_readPos];
                    _readPos = (_readPos + 1) % RingBufferSamples;
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
