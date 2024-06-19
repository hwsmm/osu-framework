// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Statistics;

namespace osu.Framework.Audio.Mixing.SDL
{
    /// <summary>
    /// Mixes <see cref="ISDLAudioChannel"/> instances and applies effects on top of them.
    /// </summary>
    internal class SDLAudioMixer : AudioMixer
    {
        public readonly IntPtr Handle;

        /// <summary>
        /// List of <see cref="ISDLAudioChannel"/> instances that are active.
        /// </summary>
        private readonly LinkedList<ISDLAudioChannel> activeChannels = new LinkedList<ISDLAudioChannel>();

        /// <summary>
        /// Creates a new <see cref="SDLAudioMixer"/>
        /// </summary>
        /// <param name="globalMixer"><inheritdoc /></param>
        /// <param name="identifier">An identifier displayed on the audio mixer visualiser.</param>
        public SDLAudioMixer(AudioMixer? globalMixer, string identifier)
            : base(globalMixer, identifier)
        {
            Handle = SDLAudioWrapper.CreateMixer();
        }

        protected override void AddInternal(IAudioChannel channel)
        {
            if (channel is not ISDLAudioChannel sdlChannel)
                return;

            activeChannels.AddFirst(sdlChannel);

            if (!IsDisposed)
            {
                if (channel is TrackSDL track)
                    SDLAudioWrapper.AddTrack(Handle, track.Handle);
                else if (channel is SampleChannelSDL sample)
                    SDLAudioWrapper.AddSample(Handle, sample.Handle);
            }
        }

        protected override void RemoveInternal(IAudioChannel channel)
        {
            if (channel is not ISDLAudioChannel sdlChannel)
                return;

            activeChannels.Remove(sdlChannel);
            removeChannelWrapper(sdlChannel);
        }

        private void removeChannelWrapper(ISDLAudioChannel sdlChannel)
        {
            if (IsDisposed)
                return;

            if (sdlChannel is TrackSDL)
                SDLAudioWrapper.RemoveTrack(Handle, sdlChannel.Handle);
            else if (sdlChannel is SampleChannelSDL)
                SDLAudioWrapper.RemoveSample(Handle, sdlChannel.Handle);
        }

        protected override void UpdateState()
        {
            var node = activeChannels.First;
            while (node != null)
            {
                var next = node.Next;

                if (!node.Value.IsActive)
                {
                    activeChannels.Remove(node);
                    removeChannelWrapper(node.Value);
                }

                node = next;
            }

            FrameStatistics.Add(StatisticsCounterType.MixChannels, activeChannels.Count);
            base.UpdateState();
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            // Move all contained channels back to the default mixer.
            foreach (var channel in activeChannels.ToArray())
                Remove(channel);

            base.Dispose(disposing);

            SDLAudioWrapper.FreeMixer(Handle);
        }

        public void StreamFree(ISDLAudioChannel channel)
        {
            removeChannelWrapper(channel);
            Remove(channel, false);
        }

        public override AudioEffect GetNewEffect(int priority = 0) => new SDLAudioEffect(this, priority);
    }
}
