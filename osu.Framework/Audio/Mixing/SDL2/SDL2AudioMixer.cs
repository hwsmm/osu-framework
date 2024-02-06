// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using ManagedBass;
using ManagedBass.Fx;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Statistics;
using NAudio.Dsp;

namespace osu.Framework.Audio.Mixing.SDL2
{
    /// <summary>
    /// Mixes <see cref="ISDL2AudioChannel"/> instances and applies effects on top of them.
    /// </summary>
    internal class SDL2AudioMixer : AudioMixer
    {
        private readonly object syncRoot = new object();

        /// <summary>
        /// List of <see cref="ISDL2AudioChannel"/> instances that are active.
        /// </summary>
        private readonly LinkedList<ISDL2AudioChannel> activeChannels = new LinkedList<ISDL2AudioChannel>();

        /// <summary>
        /// Creates a new <see cref="SDL2AudioMixer"/>
        /// </summary>
        /// <param name="globalMixer"><inheritdoc /></param>
        /// <param name="identifier">An identifier displayed on the audio mixer visualiser.</param>
        public SDL2AudioMixer(AudioMixer? globalMixer, string identifier)
            : base(globalMixer, identifier)
        {
            EnqueueAction(() => Effects.BindCollectionChanged(onEffectsChanged, true));
        }

        public override BindableList<IEffectParameter> Effects { get; } = new BindableList<IEffectParameter>();

        protected override void AddInternal(IAudioChannel channel)
        {
            if (channel is not ISDL2AudioChannel sdlChannel)
                return;

            lock (syncRoot)
                activeChannels.AddLast(sdlChannel);
        }

        protected override void RemoveInternal(IAudioChannel channel)
        {
            if (channel is not ISDL2AudioChannel sdlChannel)
                return;

            lock (syncRoot)
                activeChannels.Remove(sdlChannel);
        }

        protected override void UpdateState()
        {
            FrameStatistics.Add(StatisticsCounterType.MixChannels, channelCount);
            base.UpdateState();
        }

        private unsafe void mixAudio(float* dst, float* src, ref int filled, int samples, float left, float right)
        {
            if (left <= 0 && right <= 0)
            {
                return;
            }

            int i = 0;

            // Use CPU intrinsics where possible and use scalar operation if anything remains
            if (Avx.IsSupported)
            {
                i = mixAudioAvx(dst, src, 0, ref filled, samples, left, right);
            }

            if (Sse.IsSupported)
            {
                i = mixAudioSse(dst, src, i, ref filled, samples - i, left, right);
            }

            for (; i < samples; i++)
            {
                *(dst + i) = (*(src + i) * ((i % 2) == 0 ? left : right)) + (i < filled ? *(dst + i) : 0);
            }

            if (samples > filled)
            {
                filled = samples;
            }
        }

        private float savedLeft256 = -1f;
        private float savedRight256 = -1f;
        private Vector256<float> volVec256;

        private unsafe int mixAudioAvx(float* dstPtr, float* srcPtr, int start, ref int filled, int samples, float left, float right)
        {
            int i = start;
            int lastIndex = samples - (samples % 8);

            if (lastIndex <= 0)
            {
                return i;
            }

            if (left == 1 && right == 1)
            {
            }
            else if (savedLeft256 != left || savedRight256 != right)
            {
                savedLeft256 = left;
                savedRight256 = right;

                volVec256 = left != right
                    ? Vector256.Create(left, right, left, right, left, right, left, right)
                    : Vector256.Create(left);
            }

            while (i < lastIndex)
            {
                var srcVec = Avx.LoadVector256(srcPtr + i);

                if (left != 1 || right != 1)
                    srcVec = Avx.Multiply(srcVec, volVec256);

                if (i < filled)
                {
                    var dstVec = i + 8 > filled
                        ? Vector256.Create(
                            *(srcPtr + i),
                            filled > i + 1 ? *(srcPtr + i + 1) : 0,
                            filled > i + 2 ? *(srcPtr + i + 2) : 0,
                            filled > i + 3 ? *(srcPtr + i + 3) : 0,
                            filled > i + 4 ? *(srcPtr + i + 4) : 0,
                            filled > i + 5 ? *(srcPtr + i + 5) : 0,
                            filled > i + 6 ? *(srcPtr + i + 6) : 0,
                            filled > i + 7 ? *(srcPtr + i + 7) : 0)
                        : Avx.LoadVector256(dstPtr + i);

                    var mixVec = Avx.Add(dstVec, srcVec);

                    Avx.Store(dstPtr + i, mixVec);
                }
                else
                {
                    Avx.Store(dstPtr + i, srcVec);
                }

                i += 8;
            }

            if (i > filled)
            {
                filled = i;
            }

            return i;
        }

        private float savedLeft128 = -1f;
        private float savedRight128 = -1f;
        private Vector128<float> volVec128;

        private unsafe int mixAudioSse(float* dstPtr, float* srcPtr, int start, ref int filled, int samples, float left, float right)
        {
            int i = start;
            int lastIndex = samples - (samples % 4);

            if (lastIndex <= 0)
            {
                return i;
            }

            if (left == 1 && right == 1)
            {
            }
            else if (savedLeft128 != left || savedRight128 != right)
            {
                savedLeft128 = left;
                savedRight128 = right;

                volVec128 = left != right
                    ? Vector128.Create(left, right, left, right)
                    : Vector128.Create(left);
            }

            while (i < lastIndex)
            {
                var srcVec = Sse.LoadVector128(srcPtr + i);

                if (left != 1 || right != 1)
                    srcVec = Sse.Multiply(srcVec, volVec128);

                if (i < filled)
                {
                    var dstVec = i + 4 > filled
                        ? Vector128.Create(
                            *(srcPtr + i),
                            filled > i + 1 ? *(srcPtr + i + 1) : 0,
                            filled > i + 2 ? *(srcPtr + i + 2) : 0,
                            filled > i + 3 ? *(srcPtr + i + 3) : 0)
                        : Sse.LoadVector128(dstPtr + i);

                    var mixVec = Sse.Add(dstVec, srcVec);

                    Sse.Store(dstPtr + i, mixVec);
                }
                else
                {
                    Sse.Store(dstPtr + i, srcVec);
                }

                i += 4;
            }

            if (i > filled)
            {
                filled = i;
            }

            return i;
        }

        public static string? GetIntrinsicsType()
        {
            string? ret = null;

            if (Avx.IsSupported)
                ret += "AVX ";

            if (Sse.IsSupported)
                ret += "SSE ";

            return ret?.TrimEnd();
        }

        private float[]? ret;

        private float[]? filterArray;

        private volatile int channelCount;

        /// <summary>
        /// Mix <see cref="activeChannels"/> into a float array given as an argument.
        /// </summary>
        /// <param name="data">A float pointer that audio will be mixed into.</param>
        /// <param name="filledSamples">Mixer will mix samples up to this index, assuming that there are garbage samples afterwards.</param>
        /// <param name="sampleCount">Length of data</param>
        public unsafe void MixChannelsInto(float* data, ref int filledSamples, int sampleCount)
        {
            lock (syncRoot)
            {
                if (ret == null || sampleCount != ret.Length)
                {
                    ret = new float[sampleCount];
                }

                bool useFilters = audioFilters.Count > 0;

                if (useFilters && (filterArray == null || filterArray.Length != sampleCount))
                {
                    filterArray = new float[sampleCount];
                }

                int filterArrayFilled = 0;

                var node = activeChannels.First;

                while (node != null)
                {
                    var next = node.Next;
                    var channel = node.Value;

                    if (!(channel is AudioComponent ac && ac.IsAlive))
                    {
                        activeChannels.Remove(node);
                    }
                    else if (channel.Playing)
                    {
                        int size = channel.GetRemainingSamples(ret);

                        if (size > 0)
                        {
                            float left = 1;
                            float right = 1;

                            if (channel.Balance < 0)
                            {
                                right += channel.Balance;
                            }
                            else if (channel.Balance > 0)
                            {
                                left -= channel.Balance;
                            }

                            right *= channel.Volume;
                            left *= channel.Volume;

                            if (!useFilters)
                            {
                                fixed (float* retPtr = ret)
                                {
                                    mixAudio(data, retPtr, ref filledSamples, size, left, right);
                                }
                            }
                            else
                            {
                                fixed (float* filterArrPtr = filterArray)
                                fixed (float* retPtr = ret)
                                {
                                    mixAudio(filterArrPtr, retPtr, ref filterArrayFilled, size, left, right);
                                }
                            }
                        }
                    }

                    node = next;
                }

                channelCount = activeChannels.Count;

                if (useFilters)
                {
                    for (int i = 0; i < filterArrayFilled; i++)
                    {
                        foreach (var filter in audioFilters)
                        {
                            if (filter.BiQuadFilter != null)
                            {
                                filterArray![i] = filter.BiQuadFilter.Transform(filterArray[i]);
                            }
                        }
                    }

                    fixed (float* filterArrPtr = filterArray)
                    {
                        mixAudio(data, filterArrPtr, ref filledSamples, filterArrayFilled, 1, 1);
                    }
                }
            }
        }

        private readonly List<EffectBox> audioFilters = new List<EffectBox>();

        private void onEffectsChanged(object? sender, NotifyCollectionChangedEventArgs e) => EnqueueAction(() =>
        {
            lock (syncRoot)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                    {
                        Debug.Assert(e.NewItems != null);
                        int startIndex = Math.Max(0, e.NewStartingIndex);
                        audioFilters.InsertRange(startIndex, e.NewItems.OfType<IEffectParameter>().Select(eff => new EffectBox(eff)));
                        break;
                    }

                    case NotifyCollectionChangedAction.Move:
                    {
                        EffectBox effect = audioFilters[e.OldStartingIndex];
                        audioFilters.RemoveAt(e.OldStartingIndex);
                        audioFilters.Insert(e.NewStartingIndex, effect);
                        break;
                    }

                    case NotifyCollectionChangedAction.Remove:
                    {
                        Debug.Assert(e.OldItems != null);

                        audioFilters.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        break;
                    }

                    case NotifyCollectionChangedAction.Replace:
                    {
                        Debug.Assert(e.NewItems != null);

                        EffectBox newFilter = new EffectBox((IEffectParameter)e.NewItems[0].AsNonNull());
                        audioFilters[e.NewStartingIndex] = newFilter;
                        break;
                    }

                    case NotifyCollectionChangedAction.Reset:
                    {
                        audioFilters.Clear();
                        break;
                    }
                }
            }
        });

        internal class EffectBox
        {
            public readonly BiQuadFilter? BiQuadFilter;

            public EffectBox(IEffectParameter param)
            {
                // allowing non-bqf to keep index of list
                if (param is BQFParameters bqfp)
                    BiQuadFilter = getFilter(SDL2AudioManager.AUDIO_FREQ, bqfp);
            }
        }

        private static BiQuadFilter getFilter(float freq, BQFParameters bqfp)
        {
            BiQuadFilter filter;

            switch (bqfp.lFilter)
            {
                case BQFType.LowPass:
                    filter = BiQuadFilter.LowPassFilter(freq, bqfp.fCenter, bqfp.fQ);
                    break;

                case BQFType.HighPass:
                    filter = BiQuadFilter.HighPassFilter(freq, bqfp.fCenter, bqfp.fQ);
                    break;

                case BQFType.BandPass:
                    filter = BiQuadFilter.BandPassFilterConstantPeakGain(freq, bqfp.fCenter, bqfp.fQ);
                    break;

                case BQFType.BandPassQ:
                    filter = BiQuadFilter.BandPassFilterConstantSkirtGain(freq, bqfp.fCenter, bqfp.fQ);
                    break;

                case BQFType.Notch:
                    filter = BiQuadFilter.NotchFilter(freq, bqfp.fCenter, bqfp.fQ);
                    break;

                case BQFType.PeakingEQ:
                    filter = BiQuadFilter.PeakingEQ(freq, bqfp.fCenter, bqfp.fQ, bqfp.fGain);
                    break;

                case BQFType.LowShelf:
                    filter = BiQuadFilter.LowShelf(freq, bqfp.fCenter, bqfp.fS, bqfp.fGain);
                    break;

                case BQFType.HighShelf:
                    filter = BiQuadFilter.HighShelf(freq, bqfp.fCenter, bqfp.fS, bqfp.fGain);
                    break;

                case BQFType.AllPass:
                default: // NAudio BiQuadFilter covers all, this default is kind of meaningless
                    filter = BiQuadFilter.AllPassFilter(freq, bqfp.fCenter, bqfp.fQ);
                    break;
            }

            return filter;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // Move all contained channels back to the default mixer.
            foreach (var channel in activeChannels.ToArray())
                Remove(channel);
        }

        public void StreamFree(IAudioChannel channel)
        {
            Remove(channel, false);
        }
    }
}
