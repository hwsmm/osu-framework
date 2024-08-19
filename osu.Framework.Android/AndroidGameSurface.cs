// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Android.Content;
using Android.Runtime;
using Org.Libsdl.App;
using osu.Framework.Bindables;

namespace osu.Framework.Android
{
    internal class AndroidGameSurface : SDLSurface
    {
        public BindableSafeArea SafeAreaPadding { get; } = new BindableSafeArea();

        public AndroidGameSurface(Context? context)
            : base(context)
        {
            init();
        }

        protected AndroidGameSurface(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
            init();
        }

        private void init()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                // disable ugly green border when view is focused via hardware keyboard/mouse.
                DefaultFocusHighlightEnabled = false;
            }
        }

        private volatile bool isSurfaceReady;

        public bool IsSurfaceReady => isSurfaceReady;

        public override void HandlePause()
        {
            base.HandlePause();
            isSurfaceReady = false;
        }

        public override void HandleResume()
        {
            base.HandleResume();
            isSurfaceReady = true;
        }
    }
}
