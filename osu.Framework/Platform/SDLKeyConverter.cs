// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using osu.Framework.Input.Bindings;
using osu.Framework.Logging;
using osu.Framework.Platform.SDL3;
using SDL;

namespace osu.Framework.Platform
{
    public static class SDLKeyConverter
    {
        /// <summary>
        /// This function finds where the <paramref name="key"/> is actually located on a keyboard,
        /// and returns the corresponding key that is used in current system keyboard layout.
        /// </summary>
        /// <param name="key">A key to convert</param>
        /// <returns>Converted key with current keyboard layout</returns>
        public static InputKey GetPositionalKeyInKeymap(InputKey key)
        {
            if (FrameworkEnvironment.UseSDL3 && SDL.SDL3.SDL_WasInit(SDL_InitFlags.SDL_INIT_VIDEO) != 0)
            {
                SDL_Scancode scancode = key.ToScancode();
                SDL_Keycode keycode = SDL.SDL3.SDL_GetKeyFromScancode(scancode, SDL_Keymod.SDL_KMOD_NONE, true);
                InputKey convertedKey = KeyCombination.FromKey(keycode.ToKey());

                // InputKey.None is usually when `key` is Ctrl/Shift, where we don't know whether it is left or right, or mouse.
                return convertedKey == InputKey.None ? key : convertedKey;
            }
            else if (!FrameworkEnvironment.UseSDL3 && global::SDL2.SDL.SDL_WasInit(global::SDL2.SDL.SDL_INIT_VIDEO) != 0)
            {
                // SDL2 conversion table is not available yet.
                return key;
            }

            Logger.Log("SDL needs to be initialized first before converting keycode");
            return key;
        }

        /// <summary>
        /// This is a helper function for <see cref="GetPositionalKeyInKeymap"/>.
        /// Returns a converted <paramref name="keyBindings"/> with <see cref="GetPositionalKeyInKeymap"/>.
        /// </summary>
        /// <param name="keyBindings">An enumerable to convert</param>
        /// <returns>Converted enumerable</returns>
        public static IEnumerable<KeyBinding> GetPositionalKeyBindings(IEnumerable<KeyBinding> keyBindings)
        {
            foreach (var bind in keyBindings)
            {
                bind.KeyCombination = new KeyCombination(bind.KeyCombination.Keys.Select(GetPositionalKeyInKeymap).Order().ToImmutableArray());
                yield return bind;
            }
        }
    }
}
