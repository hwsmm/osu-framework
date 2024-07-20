﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using osu.Framework.Audio.Mixing.SDL3;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using SDL;

namespace osu.Framework.Audio.Sample
{
    internal class SampleSDL3Factory : SampleFactory
    {
        private bool isLoaded;
        public override bool IsLoaded => isLoaded;

        private readonly SDL3AudioMixer mixer;
        private readonly SDL_AudioSpec spec;

        private float[] decodedAudio = Array.Empty<float>();

        private Stream? stream;

        public SampleSDL3Factory(Stream stream, string name, SDL3AudioMixer mixer, int playbackConcurrency, SDL_AudioSpec spec)
            : base(name, playbackConcurrency)
        {
            this.stream = stream;
            this.mixer = mixer;
            this.spec = spec;
        }

        protected override void LoadSample()
        {
            Debug.Assert(CanPerformInline);
            Debug.Assert(!IsLoaded);

            if (stream == null)
                return;

            try
            {
                byte[] audio = AudioDecoderManager.DecodeAudio(spec.freq, spec.channels, spec.format, stream, out int size);

                if (size > 0)
                {
                    decodedAudio = new float[size / 4];
                    Buffer.BlockCopy(audio, 0, decodedAudio, 0, size);
                }

                Length = size / 4d / spec.freq / spec.channels * 1000d;
                isLoaded = true;
            }
            finally
            {
                stream.Dispose();
                stream = null;
            }
        }

        public SampleSDL3AudioPlayer CreatePlayer()
        {
            LoadSampleTask?.WaitSafely();

            return new SampleSDL3AudioPlayer(decodedAudio, spec.freq, spec.channels);
        }

        public override Sample CreateSample() => new SampleSDL3(this, mixer) { OnPlay = SampleFactoryOnPlay };

        protected override void UpdatePlaybackConcurrency(ValueChangedEvent<int> concurrency)
        {
        }

        ~SampleSDL3Factory()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            stream?.Dispose();
            stream = null;

            decodedAudio = Array.Empty<float>();

            base.Dispose(disposing);
        }
    }
}