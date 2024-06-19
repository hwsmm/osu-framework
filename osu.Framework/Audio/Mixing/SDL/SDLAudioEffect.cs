// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using ManagedBass.Fx;

namespace osu.Framework.Audio.Mixing.SDL
{
    internal class SDLAudioEffect : AudioEffect
    {
        private readonly SDLAudioMixer mixer;

        private volatile IntPtr bqFilter = IntPtr.Zero;

        public SDLAudioEffect(SDLAudioMixer mixer, int priority = 0)
            : base(priority)
        {
            this.mixer = mixer;
        }

        public override unsafe void Apply()
        {
            if (EffectParameter is BQFParameters bqfp)
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

                SDLAudioWrapper.BiQuadCoeff coeff = new SDLAudioWrapper.BiQuadCoeff();
                IntPtr coeffIntPtr = new IntPtr(&coeff);

                SDLAudioWrapper.BiQuadUpdate(coeffIntPtr, type, bqfp.fGain, bqfp.fCenter, SDLAudioManager.AUDIO_FREQ, bqfp.fQ);
                bqFilter = SDLAudioWrapper.ApplyBiquadFilter(mixer.Handle, bqFilter, coeffIntPtr, Priority);
            }
        }

        public override void Remove()
        {
            SDLAudioWrapper.RemoveBiquadFilter(mixer.Handle, Interlocked.Exchange(ref bqFilter, IntPtr.Zero));
        }
    }
}
