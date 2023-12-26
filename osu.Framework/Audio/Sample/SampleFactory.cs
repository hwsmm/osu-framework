// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Framework.Audio.Sample
{
    /// <summary>
    /// A factory for <see cref="SampleBass"/> objects sharing a common sample ID (and thus playback concurrency).
    /// </summary>
    internal abstract class SampleFactory : AudioCollectionManager<AdjustableAudioComponent>
    {
        /// <summary>
        /// A name identifying the sample to be created by this factory.
        /// </summary>
        public string Name { get; }

        public double Length { get; private protected set; }

        /// <summary>
        /// Todo: Expose this to support per-sample playback concurrency once ManagedBass has been updated (https://github.com/ManagedBass/ManagedBass/pull/85).
        /// </summary>
        internal readonly Bindable<int> PlaybackConcurrency = new Bindable<int>(Sample.DEFAULT_CONCURRENCY);

        protected SampleFactory(string name, int playbackConcurrency)
        {
            Name = name;
            PlaybackConcurrency.Value = playbackConcurrency;

            EnqueueAction(LoadSample);

            PlaybackConcurrency.BindValueChanged(UpdatePlaybackConcurrency);
        }

        private protected abstract void UpdatePlaybackConcurrency(ValueChangedEvent<int> concurrency);

        internal override void UpdateDevice(int deviceIndex)
        {
            // The sample may not have already loaded if a device wasn't present in a previous load attempt.
            if (!IsLoaded)
                LoadSample();
        }

        private protected abstract void LoadSample();

        public abstract Sample CreateSample();

        private protected abstract void FreeSample();

        protected void SampleFactoryOnPlay(Sample sample)
        {
            AddItem(sample);
        }

        ~SampleFactory()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (IsLoaded)
                FreeSample();

            base.Dispose(disposing);
        }
    }
}