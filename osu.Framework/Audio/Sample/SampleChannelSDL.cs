// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio.Mixing.SDL;

namespace osu.Framework.Audio.Sample
{
    internal sealed class SampleChannelSDL : SampleChannel, ISDLAudioChannel
    {
        public IntPtr Handle { get; private set; }

        public override bool Playing => !IsDisposed && SDLAudioWrapper.SampleIsPlaying(Handle);

        public override bool Looping
        {
            get => !IsDisposed && SDLAudioWrapper.SampleGetLoop(Handle);
            set => EnqueueAction(() =>
            {
                if (!IsDisposed)
                    SDLAudioWrapper.SampleSetLoop(Handle, value);
            });
        }

        public bool IsActive => IsAlive;

        public SampleChannelSDL(SampleSDL sample, IntPtr handle)
            : base(sample.Name)
        {
            Handle = handle;
        }

        public override void Play()
        {
            if (!IsDisposed)
                SDLAudioWrapper.SamplePlay(Handle); // potentially thread unsafe but prob fine

            base.Play();
        }

        public override void Stop()
        {
            base.Stop();

            EnqueueAction(() =>
            {
                if (!IsDisposed)
                    SDLAudioWrapper.SamplePause(Handle);
            });
        }

        internal override void OnStateChanged()
        {
            base.OnStateChanged();

            if (IsDisposed)
                return;

            SDLAudioWrapper.SampleSetVolume(Handle, AggregateVolume.Value, AggregateBalance.Value);
            SDLAudioWrapper.SampleSetFrequency(Handle, AggregateFrequency.Value);
        }

        ~SampleChannelSDL()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            (Mixer as SDLAudioMixer)?.StreamFree(this);

            base.Dispose(disposing);

            SDLAudioWrapper.FreeSample(Handle);
        }
    }
}
