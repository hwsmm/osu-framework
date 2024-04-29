// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Framework.Audio.Mixing.SDL
{
    /// <summary>
    /// Interface for audio channels that feed audio to <see cref="SDLAudioMixer"/>.
    /// </summary>
    internal interface ISDLAudioChannel : IAudioChannel
    {
        IntPtr Handle { get; }

        bool IsActive { get; }
    }
}
