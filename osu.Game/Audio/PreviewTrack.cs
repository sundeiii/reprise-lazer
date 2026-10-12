// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Threading;

namespace osu.Game.Audio
{
    [LongRunningLoad]
    public abstract partial class PreviewTrack : Component
    {
        /// <summary>
        /// Invoked when this <see cref="PreviewTrack"/> has stopped playing.
        /// Not invoked in a thread-safe context.
        /// </summary>
        public event Action? Stopped;

        /// <summary>
        /// Invoked when this <see cref="PreviewTrack"/> has started playing.
        /// Not invoked in a thread-safe context.
        /// </summary>
        public event Action? Started;

        /// <summary>
        /// How long a preview plays at most (from its start), fading in at its start and out at its end.
        /// </summary>
        public const double MAX_LENGTH = 30000;

        public const double FADE_IN_DURATION = 1000;
        public const double FADE_OUT_DURATION = 2500;

        protected Track? Track { get; private set; }

        private bool hasStarted;

        private readonly BindableDouble fadeVolume = new BindableDouble(1);

        /// <summary>
        /// How loud the preview is for its fades (0 to 1).
        /// </summary>
        public double FadeVolume => fadeVolume.Value;

        [BackgroundDependencyLoader]
        private void load()
        {
            Track = GetTrack();

            if (Track != null)
            {
                Track.Completed += Stop;
                Track.Looping = looping;
                Track.AddAdjustment(AdjustableProperty.Volume, fadeVolume);
            }
        }

        /// <summary>
        /// Where in the track the preview starts (a downloaded map's preview point; online previews are already cut there).
        /// </summary>
        protected virtual double StartTime => 0;

        // The preview's end: 30 seconds in, or the track's end before that.
        private double endTime => Math.Min(StartTime + MAX_LENGTH, Track?.Length ?? 0);

        /// <summary>
        /// Length of the preview.
        /// </summary>
        public double Length => Math.Max(0, endTime - StartTime);

        /// <summary>
        /// The current time in the preview.
        /// </summary>
        public double CurrentTime => Math.Max(0, (Track?.CurrentTime ?? 0) - StartTime);

        // Fading in over its first second and out over its last seconds, stopping at its end.
        protected override void Update()
        {
            base.Update();

            if (Track == null || !hasStarted || !Track.IsLoaded || Track.Length <= 0)
                return;

            double time = Track.CurrentTime;

            if (!looping && time >= endTime)
            {
                Stop();
                return;
            }

            double fadeIn = Math.Clamp((time - StartTime) / FADE_IN_DURATION, 0, 1);
            double fadeOut = looping ? 1 : Math.Clamp((endTime - time) / FADE_OUT_DURATION, 0, 1);

            // Eased, for a smooth swell and fade.
            fadeVolume.Value = Math.Sin(fadeIn * Math.PI / 2) * Math.Sin(fadeOut * Math.PI / 2);
        }

        /// <summary>
        /// Whether the track is loaded.
        /// </summary>
        public bool TrackLoaded => Track?.IsLoaded ?? false;

        /// <summary>
        /// Whether the track is playing.
        /// </summary>
        public bool IsRunning => Track?.IsRunning ?? false;

        private bool looping;

        /// <summary>
        /// Whether the track should loop.
        /// </summary>
        public bool Looping
        {
            get => looping;
            set
            {
                looping = value;

                Track?.Looping = looping;
            }
        }

        private ScheduledDelegate? startDelegate;

        /// <summary>
        /// Starts playing this <see cref="PreviewTrack"/>.
        /// </summary>
        /// <returns>Whether the track is started or already playing.</returns>
        public bool Start()
        {
            if (Track == null)
                return false;

            startDelegate = Schedule(() =>
            {
                if (hasStarted)
                    return;

                hasStarted = true;

                fadeVolume.Value = 0;
                Track.Stop();
                Track.Seek(StartTime);
                Track.Start();
                Started?.Invoke();
            });

            return true;
        }

        /// <summary>
        /// Stops playing this <see cref="PreviewTrack"/>.
        /// </summary>
        public void Stop()
        {
            startDelegate?.Cancel();

            if (Track == null)
                return;

            if (!hasStarted)
                return;

            hasStarted = false;

            // This pre-check is important, fixes a BASS deadlock in some scenarios.
            if (!Track.HasCompleted)
            {
                Track.Stop();

                // Ensure the track is reset immediately on stopping, so the next time it is started it has a correct time value.
                Track.Seek(0);
            }

            Stopped?.Invoke();
        }

        /// <summary>
        /// Retrieves the audio track.
        /// </summary>
        protected abstract Track? GetTrack();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            Stop();
            Track?.Dispose();
        }
    }
}
