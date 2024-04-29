// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Dsp;
using osu.Framework.Audio.Mixing.SDL;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace osu.Framework.Audio.Track
{
    public sealed class TrackSDL : Track, ISDLAudioChannel
    {
        public IntPtr Handle { get; private set; }

        public override bool IsDummyDevice => false;

        private volatile bool isLoaded;
        public override bool IsLoaded => isLoaded;

        private double currentTime;
        public override double CurrentTime => currentTime;

        private volatile bool isRunning;
        public override bool IsRunning => isRunning;

        private volatile bool hasCompleted;
        public override bool HasCompleted => hasCompleted;

        private volatile int bitrate;
        public override int? Bitrate => bitrate;

        public TrackSDL(string name)
            : base(name)
        {
            // SoundTouch limitation
            const float tempo_minimum_supported = 0.05f;
            AggregateTempo.ValueChanged += t =>
            {
                if (t.NewValue < tempo_minimum_supported)
                    throw new ArgumentException($"{nameof(TrackSDL)} does not support {nameof(Tempo)} specifications below {tempo_minimum_supported}. Use {nameof(Frequency)} instead.");
            };

            Handle = SDLAudioWrapper.CreateTrack();
        }

        private AudioDecoderManager.AudioDecoder? decodeData;

        private object disposalSync = new object();
        // decoder thread runs separately to the audio thread because audio thread hz is only 1000hz, too slow than busy loop until decoding ends
        // it can check disposal at start, but may occur UB if decoder calls track while audio thr is disposing this track!

        internal void ReceiveAudioData(byte[] audio, int length, AudioDecoderManager.AudioDecoder data, bool done)
        {
            lock (disposalSync)
            {
                if (IsDisposed)
                    return;

                int ret = 0;

                if (!SDLAudioWrapper.TrackIsLoading(Handle))
                {
                    if ((ret = SDLAudioWrapper.TrackInitBuffer(Handle, (ulong)data.ByteLength)) <= 0)
                    {
                        Logger.Log($"Failed initalizing buffer for track! {ret}");
                        return;
                    }
                }

                unsafe
                {
                    fixed (byte* ptr = audio)
                    {
                        if ((ret = SDLAudioWrapper.TrackPutData(Handle, ptr, (ulong)length)) <= 0)
                            Logger.Log($"Failed putting data to track player! {ret}");
                    }
                }

                if (done)
                    SDLAudioWrapper.TrackDonePutting(Handle);

                if (!isLoaded)
                    decodeData = data;
            }
        }

        private volatile bool amplitudeRequested;
        private double lastTime;

        private ChannelAmplitudes currentAmplitudes = ChannelAmplitudes.Empty;
        private float[]? samples;
        private Complex[]? fftSamples;
        private float[]? fftResult;

        public override ChannelAmplitudes CurrentAmplitudes
        {
            get
            {
                if (!amplitudeRequested)
                    amplitudeRequested = true;

                return isRunning ? currentAmplitudes : ChannelAmplitudes.Empty;
            }
        }

        private void updateCurrentAmplitude()
        {
            samples ??= new float[(int)(SDLAudioManager.AUDIO_FREQ * (1f / 60)) * SDLAudioManager.AUDIO_CHANNELS];
            fftSamples ??= new Complex[ChannelAmplitudes.AMPLITUDES_SIZE * 2];
            fftResult ??= new float[ChannelAmplitudes.AMPLITUDES_SIZE];

            unsafe
            {
                fixed (float* ptr = samples)
                    SDLAudioWrapper.TrackPeek(Handle, ptr, samples.Length, lastTime);
            }

            float leftAmplitude = 0;
            float rightAmplitude = 0;
            int secondCh = SDLAudioManager.AUDIO_CHANNELS < 2 ? 0 : 1;
            int fftIndex = 0;

            for (int i = 0; i < samples.Length; i += SDLAudioManager.AUDIO_CHANNELS)
            {
                leftAmplitude = Math.Max(leftAmplitude, Math.Abs(samples[i]));
                rightAmplitude = Math.Max(rightAmplitude, Math.Abs(samples[i + secondCh]));

                if (fftIndex < fftSamples.Length)
                {
                    fftSamples[fftIndex].Y = 0;
                    fftSamples[fftIndex++].X = (samples[i] + samples[i + secondCh]) * 0.5f;
                }
            }

            FastFourierTransform.FFT(true, (int)Math.Log2(fftSamples.Length), fftSamples);

            for (int i = 0; i < fftResult.Length; i++)
                fftResult[i] = fftSamples[i].ComputeMagnitude();

            currentAmplitudes = new ChannelAmplitudes(Math.Min(1f, leftAmplitude), Math.Min(1f, rightAmplitude), fftResult);
        }

        protected override void UpdateState()
        {
            base.UpdateState();

            if (IsDisposed)
                return;

            if (decodeData != null)
            {
                if (!isLoaded)
                {
                    Length = decodeData.Length;
                    bitrate = decodeData.Bitrate;
                    isLoaded = true;
                }

                if (SDLAudioWrapper.TrackIsLoaded(Handle))
                    decodeData = null;
            }

            if (SDLAudioWrapper.TrackIsDone(Handle) && isRunning)
            {
                if (Looping)
                {
                    seekInternal(RestartPoint);
                    startInternal();
                }
                else
                {
                    isRunning = false;
                    hasCompleted = true;
                    RaiseCompleted();
                }
            }

            Interlocked.Exchange(ref currentTime, SDLAudioWrapper.TrackGetPosition(Handle));

            SDLAudioWrapper.UpdateTrack(Handle);

            // Not sure if I need to split this up to another class since this featrue is only exclusive to Track
            if (amplitudeRequested && isRunning && currentTime != lastTime)
            {
                lastTime = currentTime;

                updateCurrentAmplitude();
            }
        }

        public override bool Seek(double seek) => SeekAsync(seek).GetResultSafely();

        public override async Task<bool> SeekAsync(double seek)
        {
            double conservativeLength = Length == 0 ? double.MaxValue : Length;
            double conservativeClamped = Math.Clamp(seek, 0, conservativeLength);

            await EnqueueAction(() => seekInternal(seek)).ConfigureAwait(false);

            return conservativeClamped == seek;
        }

        private void seekInternal(double seek)
        {
            if (IsDisposed)
                return;

            SDLAudioWrapper.TrackSetPosition(Handle, seek);

            if (seek < Length)
                hasCompleted = false;

            Interlocked.Exchange(ref currentTime, SDLAudioWrapper.TrackGetPosition(Handle));
        }

        public override void Start()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(ToString(), "Can not start disposed tracks.");

            StartAsync().WaitSafely();
        }

        public override Task StartAsync() => EnqueueAction(startInternal);

        private void startInternal()
        {
            if (IsDisposed)
                return;

            SDLAudioWrapper.TrackPlay(Handle);
            isRunning = true;
            hasCompleted = false;
        }

        public override void Stop() => StopAsync().WaitSafely();

        public override Task StopAsync() => EnqueueAction(() =>
        {
            if (IsDisposed)
                return;

            SDLAudioWrapper.TrackPause(Handle);
            isRunning = false;
        });

        internal override void OnStateChanged()
        {
            base.OnStateChanged();

            if (IsDisposed)
                return;

            SDLAudioWrapper.TrackSetVolume(Handle, AggregateVolume.Value, AggregateBalance.Value);
            SDLAudioWrapper.TrackSetFreqTempo(Handle, AggregateFrequency.Value, AggregateTempo.Value);
        }

        public bool IsActive => !IsDisposed;

        ~TrackSDL()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            lock (disposalSync)
            {
                if (IsDisposed)
                    return;

                isRunning = false;
                (Mixer as SDLAudioMixer)?.StreamFree(this);

                decodeData?.Stop();

                base.Dispose(disposing);

                SDLAudioWrapper.FreeTrack(Handle);
            }
        }
    }
}
