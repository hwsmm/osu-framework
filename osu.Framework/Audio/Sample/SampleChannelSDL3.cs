// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using mysoundlib_cs;
using osu.Framework.Audio.Mixing.SDL3;
using osu.Framework.Extensions;

namespace osu.Framework.Audio.Sample
{
    internal sealed class SampleChannelSDL3 : SampleChannel, ISDL3AudioChannel
    {
        private volatile IntPtr handle;

        public IntPtr Handle => handle;

        public override bool Playing => enqueuedPlaybackStart || MySoundLibrary.mslSampleIsPlaying(Handle).ToBool();

        private volatile bool looping;

        public override bool Looping
        {
            get => !IsDisposed && looping;
            set => EnqueueAction(() =>
            {
                if (!IsDisposed && looping != value)
                {
                    looping = value;
                    MySoundLibrary.mslSampleSetLoop(Handle, value.ToIntBool());
                }
            });
        }

        public bool IsActive => IsAlive;

        private readonly SampleSDL3 sample;

        // IAudioChannel also defines Mixer, but osu!framework tries to play the audio even when SampleFactory is not loaded.
        // My sound library doesn't support changing audio data of sample after its creation (shouldn't support such operation, it's too much for a sample)
        // So, cache mixer and add once it's loaded
        private readonly Action<SampleChannelSDL3> addToMixer;

        public SampleChannelSDL3(SampleSDL3 sample, Action<SampleChannelSDL3> addToMixer)
            : base(sample.Name)
        {
            this.sample = sample;
            this.addToMixer = addToMixer;

            if (sample.IsLoaded)
            {
                handle = sample.CreatePlayer().ThrowIfNull();
                addToMixer(this);
            }
        }

        protected override void UpdateState()
        {
            if (enqueuedPlaybackStart)
            {
                if (handle != IntPtr.Zero)
                {
                    // race condition when enqueuedPlaybackStart becomes true again if Play() is called AGAIN while the below snippet is running
                    // it can be easily handled by setting it to false again
                    enqueuedPlaybackStart = false;
                }
                else if (sample.IsLoaded)
                {
                    Interlocked.Exchange(ref handle, sample.CreatePlayer().ThrowIfNull());
                    MySoundLibrary.mslSampleSetLoop(handle, looping.ToIntBool());
                    addToMixer(this);

                    playInternal();
                    enqueuedPlaybackStart = false;
                }
            }

            base.UpdateState();
        }

        private void playInternal()
        {
            InvalidateState();

            MySoundLibrary.mslSamplePlay(handle);
        }

        /// <summary>
        /// Whether the playback start has been enqueued.
        /// </summary>
        private volatile bool enqueuedPlaybackStart;

        public override void Play()
        {
            base.Play();

            if (!IsDisposed)
            {
                if (handle == IntPtr.Zero)
                {
                    enqueuedPlaybackStart = true;
                    return;
                }

                playInternal();
            }
            else
            {
                throw new ObjectDisposedException("This SampleChannelSDL3 is disposed.");
            }
        }

        public override void Stop()
        {
            base.Stop();

            EnqueueAction(() =>
            {
                enqueuedPlaybackStart = false;

                if (!IsDisposed)
                    MySoundLibrary.mslSamplePause(Handle);
            });
        }

        internal override void OnStateChanged()
        {
            base.OnStateChanged();

            if (IsDisposed)
                return;

            MySoundLibrary.mslSampleSetVolume(Handle, AggregateVolume.Value, AggregateBalance.Value);
            MySoundLibrary.mslSampleSetFrequency(Handle, AggregateFrequency.Value);
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            base.Dispose(disposing);

            MySoundLibrary.mslDestroySample(Interlocked.Exchange(ref handle, IntPtr.Zero));
        }
    }
}
