// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio.Mixing.SDL;

namespace osu.Framework.Audio.Sample
{
    internal sealed class SampleSDL : Sample
    {
        public override bool IsLoaded => factory.IsLoaded;

        private readonly SampleSDLFactory factory;
        private readonly SDLAudioMixer mixer;

        public SampleSDL(SampleSDLFactory factory, SDLAudioMixer mixer)
            : base(factory)
        {
            this.factory = factory;
            this.mixer = mixer;
        }

        protected override SampleChannel CreateChannel()
        {
            var channel = new SampleChannelSDL(this, factory.CreatePlayer());
            mixer.Add(channel);
            return channel;
        }
    }
}
