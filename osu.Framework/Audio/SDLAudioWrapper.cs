// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;

namespace osu.Framework.Audio
{
    /// <summary>
    /// Unmanaged wrapper for SDL Audio
    /// </summary>
    internal unsafe class SDLAudioWrapper
    {
        private const string lib_name = "ofsdlaudiow";

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr AllocAudioManager();

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeAudioManager(IntPtr manager);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern SDL.SDL_AudioDeviceID OpenAudioDevice(IntPtr manager);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CloseAudioDevice(IntPtr manager);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern int AddMixer(IntPtr manager, IntPtr mixer);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern int RemoveMixer(IntPtr manager, IntPtr mixer);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetAudioFormat(out int freq, out int channels, out SDL.SDL_AudioFormat format);

        // MIXER

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateMixer();

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeMixer(IntPtr mixer);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern int AddTrack(IntPtr mixer, IntPtr channel);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern int RemoveTrack(IntPtr mixer, IntPtr channel);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern int AddSample(IntPtr mixer, IntPtr channel);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern int RemoveSample(IntPtr mixer, IntPtr channel);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ReplaceFilterList(IntPtr mixer, IntPtr* filter_list, int list_size);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveFilter(IntPtr mixer, int index);

        // BIQUAD

        public enum BiQuadType
        {
            LPF, /* low pass filter */
            HPF, /* High pass filter */
            BPF, /* band pass filter */
            BPQ, /* band pass filter (constant skirt gain) */
            NOTCH, /* Notch Filter */
            PEQ, /* Peaking band EQ filter */
            LSH, /* Low shelf filter */
            HSH, /* High shelf filter */
            APF /* all pass filter */
        }

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr BiQuadNew(BiQuadType type, double dbGain, double freq, double srate, double q);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void BiQuadFree(IntPtr filter);

        // TRACK

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateTrack();

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeTrack(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern int TrackInitBuffer(IntPtr track, ulong size);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern int TrackPutData(IntPtr track, void* audio, ulong size);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void TrackDonePutting(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void TrackSetFreqTempo(IntPtr track, double freq, double tempo);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool TrackIsLoading(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool TrackIsLoaded(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool TrackIsDone(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void TrackPlay(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void TrackPause(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool TrackIsPlaying(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void TrackSetVolume(IntPtr track, double volume, double balance);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern double TrackGetPosition(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void TrackSetPosition(IntPtr track, double position);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void UpdateTrack(IntPtr track);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern int TrackPeek(IntPtr track, float* buffer, int bufferSize, double position);

        // SAMPLE

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr PrepareData(void* data, int size);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeData(IntPtr data);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateSample(IntPtr data, int size);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeSample(IntPtr sample);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ResetSample(IntPtr sample);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SamplePlay(IntPtr sample);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SamplePause(IntPtr sample);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SampleIsPlaying(IntPtr sample);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SampleSetVolume(IntPtr sample, double volume, double balance);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern double SampleGetFrequency(IntPtr sample);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SampleSetFrequency(IntPtr sample, double rfreq);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SampleGetLoop(IntPtr sample);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SampleSetLoop(IntPtr sample, bool loop);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SampleIsDone(IntPtr sample);
    }
}
