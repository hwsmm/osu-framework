// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using osu.Framework.Audio.Mixing.SDL;
using osu.Framework.Bindables;
using osu.Framework.Extensions;

namespace osu.Framework.Audio.Sample
{
    internal class SampleSDLFactory : SampleFactory
    {
        private bool isLoaded;
        public override bool IsLoaded => isLoaded;

        private readonly SDLAudioMixer mixer;

        private IntPtr decodedAudio = IntPtr.Zero;
        private int size;

        private Stream? stream;

        public SampleSDLFactory(Stream stream, string name, SDLAudioMixer mixer, int playbackConcurrency)
            : base(name, playbackConcurrency)
        {
            this.stream = stream;
            this.mixer = mixer;
        }

        protected override void LoadSample()
        {
            Debug.Assert(CanPerformInline);
            Debug.Assert(!IsLoaded);

            if (stream == null)
                return;

            try
            {
                byte[] audio = AudioDecoderManager.DecodeAudio(SDLAudioManager.AUDIO_FREQ, SDLAudioManager.AUDIO_CHANNELS, SDLAudioManager.AUDIO_FORMAT, stream, out int got);

                if (got > 0)
                {
                    unsafe
                    {
                        fixed (byte* ptr = audio)
                            decodedAudio = SDLAudioWrapper.PrepareData(ptr, got);
                    }

                    if (decodedAudio != IntPtr.Zero)
                        size = got;
                }

                Length = size / 4d / SDLAudioManager.AUDIO_FREQ / SDLAudioManager.AUDIO_CHANNELS * 1000d;
                isLoaded = true;
            }
            finally
            {
                stream.Dispose();
                stream = null;
            }
        }

        public IntPtr CreatePlayer()
        {
            LoadSampleTask?.WaitSafely();

            return SDLAudioWrapper.CreateSample(decodedAudio, size);
        }

        public override Sample CreateSample() => new SampleSDL(this, mixer) { OnPlay = SampleFactoryOnPlay };

        protected override void UpdatePlaybackConcurrency(ValueChangedEvent<int> concurrency)
        {
        }

        ~SampleSDLFactory()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            stream?.Dispose();
            stream = null;

            SDLAudioWrapper.FreeData(decodedAudio);

            base.Dispose(disposing);
        }
    }
}
