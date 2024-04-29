// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using ManagedBass;
using ManagedBass.Fx;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Statistics;
using SDL;

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
            EnqueueAction(() => Effects.BindCollectionChanged(onEffectsChanged, true));
        }

        public override BindableList<IEffectParameter> Effects { get; } = new BindableList<IEffectParameter>();

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

            if (isFilterUpdated)
            {
                IntPtr[] newarr = audioFilters.ToArray();

                unsafe
                {
                    fixed (IntPtr* ptr = newarr)
                        SDLAudioWrapper.ReplaceFilterList(Handle, ptr, newarr.Length);
                }

                isFilterUpdated = false;
            }

            FrameStatistics.Add(StatisticsCounterType.MixChannels, activeChannels.Count);
            base.UpdateState();
        }

        private readonly List<IntPtr> audioFilters = new List<IntPtr>();

        private bool isFilterUpdated;

        private void onEffectsChanged(object? sender, NotifyCollectionChangedEventArgs e) => EnqueueAction(() =>
        {
            if (IsDisposed)
                return;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                {
                    Debug.Assert(e.NewItems != null);
                    int startIndex = Math.Max(0, e.NewStartingIndex);
                    audioFilters.InsertRange(startIndex, e.NewItems.OfType<IEffectParameter>().Select(eff => getFilter(eff)));
                    break;
                }

                case NotifyCollectionChangedAction.Move:
                {
                    IntPtr effect = audioFilters[e.OldStartingIndex];
                    audioFilters.RemoveAt(e.OldStartingIndex);
                    audioFilters.Insert(e.NewStartingIndex, effect);
                    break;
                }

                case NotifyCollectionChangedAction.Remove:
                {
                    Debug.Assert(e.OldItems != null);

                    for (int i = 0; i < e.OldItems.Count; i++)
                        SDLAudioWrapper.RemoveFilter(Handle, e.OldStartingIndex + i);

                    audioFilters.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                    break;
                }

                case NotifyCollectionChangedAction.Replace:
                {
                    Debug.Assert(e.NewItems != null);

                    SDLAudioWrapper.RemoveFilter(Handle, e.NewStartingIndex);

                    IntPtr newFilter = getFilter((IEffectParameter)e.NewItems[0].AsNonNull());
                    audioFilters[e.NewStartingIndex] = newFilter;
                    break;
                }

                case NotifyCollectionChangedAction.Reset:
                {
                    SDLAudioWrapper.RemoveFilter(Handle, -2);

                    audioFilters.Clear();
                    break;
                }
            }

            isFilterUpdated = true;

            static IntPtr getFilter(IEffectParameter param)
            {
                if (param is BQFParameters bqfp)
                {
                    SDLAudioWrapper.BiQuadType type;

                    switch (bqfp.lFilter)
                    {
                        case BQFType.LowPass:
                            type = SDLAudioWrapper.BiQuadType.LPF;
                            break;

                        case BQFType.HighPass:
                            type = SDLAudioWrapper.BiQuadType.HPF;
                            break;

                        case BQFType.BandPass:
                            type = SDLAudioWrapper.BiQuadType.BPF;
                            break;

                        case BQFType.BandPassQ:
                            type = SDLAudioWrapper.BiQuadType.BPQ;
                            break;

                        case BQFType.Notch:
                            type = SDLAudioWrapper.BiQuadType.NOTCH;
                            break;

                        case BQFType.PeakingEQ:
                            type = SDLAudioWrapper.BiQuadType.PEQ;
                            break;

                        case BQFType.LowShelf:
                            type = SDLAudioWrapper.BiQuadType.LSH;
                            break;

                        case BQFType.HighShelf:
                            type = SDLAudioWrapper.BiQuadType.HSH;
                            break;

                        case BQFType.AllPass:
                        default:
                            type = SDLAudioWrapper.BiQuadType.APF;
                            break;
                    }

                    IntPtr bqf = SDLAudioWrapper.BiQuadNew(type, bqfp.fGain, bqfp.fCenter, SDLAudioManager.AUDIO_FREQ, bqfp.fQ);
                    return bqf;
                }

                return IntPtr.Zero;
            }
        });

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
    }
}
