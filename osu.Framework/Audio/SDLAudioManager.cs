// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.IO;
using osu.Framework.Audio.Mixing;
using osu.Framework.Audio.Mixing.SDL;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform.Linux.Native;
using osu.Framework.Threading;
using SDL;

namespace osu.Framework.Audio
{
    public class SDLAudioManager : AudioManager
    {
        public static readonly int AUDIO_FREQ;
        public static readonly int AUDIO_CHANNELS;
        public static readonly SDL_AudioFormat AUDIO_FORMAT;

        static SDLAudioManager()
        {
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                Library.Load("libofsdlaudiow.so", Library.LoadFlags.RTLD_LAZY | Library.LoadFlags.RTLD_GLOBAL);
                Library.Load("libsamplerate.so", Library.LoadFlags.RTLD_LAZY | Library.LoadFlags.RTLD_GLOBAL);
                Library.Load("libSoundTouchDll.so", Library.LoadFlags.RTLD_LAZY | Library.LoadFlags.RTLD_GLOBAL);
            }

            SDLAudioWrapper.GetAudioFormat(out AUDIO_FREQ, out AUDIO_CHANNELS, out AUDIO_FORMAT);
        }

        public readonly IntPtr Handle;

        private volatile SDL_AudioDeviceID deviceId;
        private int bufferSize = (int)(AUDIO_FREQ * 0.01);

        private static readonly AudioDecoderManager decoder = new AudioDecoderManager();

        /// <summary>
        /// Creates a new <see cref="SDLAudioManager"/>.
        /// </summary>
        /// <param name="audioThread">The host's audio thread.</param>
        /// <param name="trackStore">The resource store containing all audio tracks to be used in the future.</param>
        /// <param name="sampleStore">The sample store containing all audio samples to be used in the future.</param>
        public SDLAudioManager(AudioThread audioThread, ResourceStore<byte[]> trackStore, ResourceStore<byte[]> sampleStore)
            : base(audioThread, trackStore, sampleStore)
        {
            Handle = SDLAudioWrapper.AllocAudioManager();

            AudioScheduler.Add(() =>
            {
                // comment below lines if you want to use FFmpeg to decode audio, AudioDecoder will use FFmpeg if no BASS device is available
                ManagedBass.Bass.Configure((ManagedBass.Configuration)68, 1);
                audioThread.InitDevice(ManagedBass.Bass.NoSoundDevice);
            });
        }

        private string currentDeviceName = "Not loaded";

        public override string ToString()
        {
            return $@"{GetType().ReadableName()} ({currentDeviceName})";
        }

        protected override AudioMixer AudioCreateAudioMixer(AudioMixer fallbackMixer, string identifier)
        {
            var mixer = new SDLAudioMixer(fallbackMixer, identifier);
            AddItem(mixer);
            return mixer;
        }

        protected override void ItemAdded(AudioComponent item)
        {
            base.ItemAdded(item);

            if (IsDisposed)
                return;

            if (item is SDLAudioMixer mixer)
                SDLAudioWrapper.AddMixer(Handle, mixer.Handle);
        }

        protected override void ItemRemoved(AudioComponent item)
        {
            base.ItemRemoved(item);

            if (IsDisposed)
                return;

            if (item is SDLAudioMixer mixer)
                SDLAudioWrapper.RemoveMixer(Handle, mixer.Handle);
        }

        protected override unsafe bool SetAudioDevice(string deviceName = null)
        {
            deviceId = SDLAudioWrapper.OpenAudioDevice(Handle);

            if (deviceId == 0)
            {
                Logger.Log("No audio device can be used! Check your audio system.", level: LogLevel.Error);
                return false;
            }

            int sampleFrameSize = 0;
            SDL_AudioSpec temp; // this has 'real' device info which is useless since SDL converts audio according to the spec we provided
            if (SDL3.SDL_GetAudioDeviceFormat(deviceId, &temp, &sampleFrameSize) == 0)
                bufferSize = sampleFrameSize * (int)Math.Ceiling((double)AUDIO_FREQ / temp.freq);

            currentDeviceName = "Default"; // Device selection is not available for now

            Logger.Log($@"🔈 SDL Audio initialised
                            Driver:      {SDL3.SDL_GetCurrentAudioDriver()}
                            Device Name: {currentDeviceName}
                            Format:      {AUDIO_FREQ}hz {AUDIO_CHANNELS}ch
                            Buffer size: {bufferSize}");

            return true;
        }

        protected override bool SetAudioDevice(int deviceIndex)
        {
            return SetAudioDevice();
        }

        protected override bool IsCurrentDeviceValid() => deviceId > 0 && SDL3.SDL_AudioDevicePaused(deviceId) == SDL3.SDL_FALSE;

        internal override Track.Track GetNewTrack(Stream data, string name)
        {
            TrackSDL track = new TrackSDL(name);
            EnqueueAction(() => decoder.StartDecodingAsync(AUDIO_FREQ, AUDIO_CHANNELS, AUDIO_FORMAT, data, track.ReceiveAudioData));
            return track;
        }

        internal override SampleFactory GetSampleFactory(Stream data, string name, AudioMixer mixer, int playbackConcurrency)
            => new SampleSDLFactory(data, name, (SDLAudioMixer)mixer, playbackConcurrency);

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            base.Dispose(disposing);

            decoder?.Dispose();

            SDLAudioWrapper.FreeAudioManager(Handle);
        }
    }
}
