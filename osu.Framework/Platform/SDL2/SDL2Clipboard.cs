// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Framework.Extensions;
using SixLabors.ImageSharp;
using static SDL2.SDL;

namespace osu.Framework.Platform.SDL2
{
    public class SDL2Clipboard : SDLClipboard
    {
        // SDL cannot differentiate between string.Empty and no text (eg. empty clipboard or an image)
        // doesn't matter as text editors don't really allow copying empty strings.
        // assume that empty text means no text.
        public override Task<string?> GetTextAsync() => EnqueueAction(() => SDL_HasClipboardText() == SDL_bool.SDL_TRUE ? SDL_GetClipboardText() : null);

        public override string? GetText() => GetTextAsync().GetResultSafely();

        public override void SetText(string text) => EnqueueAction(() => SDL_SetClipboardText(text));

        public override Image<TPixel>? GetImage<TPixel>()
        {
            return null;
        }

        public override void SetImage(Image image)
        {
            return;
        }
    }
}
