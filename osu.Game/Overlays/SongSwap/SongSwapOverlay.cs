// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.SongSwap;
using osu.Game.Configuration;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.SongSwap
{
    /// <summary>
    /// Reprise: a beatmap played to another song (SlowDogged's SongSwapper, in the game): any song, stretched to the map's tempo (the nearest
    /// of its same, half, double or quarter time), its best part on the map's and every beat on the map's, looped if it's too short, in place of
    /// the map's own song. Only the audio changes, so plays on the map count as ever. In the first-run
    /// setup's style; opened from a beatmap's menu in song select.
    /// </summary>
    [Cached]
    public partial class SongSwapOverlay : WizardOverlay
    {
        [Resolved]
        private SongSwapManager swaps { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> working { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        /// <summary>The map whose song's being swapped.</summary>
        public BeatmapInfo? Map { get; private set; }

        public SongSwapOverlay()
            : base(OverlayColourScheme.Purple)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddStep<ScreenSongSwap>();

            Header.Title = "song swap";
            Header.Description = "play a map to another song";
        }

        /// <summary>Opens it for a map.</summary>
        public void ShowFor(BeatmapInfo map)
        {
            Map = map;

            // A fresh list for this map (the step's made again when it opens: see WizardOverlay).
            if (State.Value == osu.Framework.Graphics.Containers.Visibility.Visible)
                Hide();

            Show();
            (CurrentScreen as ScreenSongSwap)?.Refresh();
        }

        // Its button swaps to the song picked (in the background) as it closes.
        protected override void ShowNextStep()
        {
            (CurrentScreen as ScreenSongSwap)?.StopPreview();

            if (CurrentScreen is ScreenSongSwap screen && Map != null && screen.Picked != null)
                Swap(Map, screen.Picked);

            base.ShowNextStep();
        }

        protected override void PopOut()
        {
            (CurrentScreen as ScreenSongSwap)?.StopPreview();
            base.PopOut();
        }

        /// <summary>Swaps the map's song (in the background), with a notification of how it went.</summary>
        public void Swap(BeatmapInfo map, BeatmapInfo song)
        {
            var progress = new ProgressNotification
            {
                Text = $"Swapping the song of {map.Metadata.Title} for {song.Metadata.Title}...",
                CompletionText = $"{map.Metadata.Title} now plays to {song.Metadata.Artist} - {song.Metadata.Title}.",
                State = ProgressNotificationState.Active,
            };

            notifications?.Post(progress);

            // Done from here (the screen's gone by then); a hidden overlay doesn't run what it schedules, so it keeps running until then.
            AlwaysPresent = true;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // Made in the background; put in place here (it writes to the database).
                    bool instrumental = config.Get<bool>(OsuSetting.SongSwapInstrumental);
                    var prepared = swaps.Prepare(map, song, config.Get<bool>(OsuSetting.SongSwapLoop), config.Get<bool>(OsuSetting.SongSwapKeepPitch),
                        instrumental, p => Schedule(() =>
                        {
                            progress.Text = $"Taking the vocals out of {song.Metadata.Title} ({p:0%})...";
                            progress.Progress = (float)(p * 0.9);
                        }));

                    Schedule(() =>
                    {
                        try
                        {
                            swaps.Apply(prepared);
                            reloadIfPlaying(map);
                            progress.State = ProgressNotificationState.Completed;
                        }
                        catch (Exception e)
                        {
                            failed(e);
                        }
                    });
                }
                catch (Exception e)
                {
                    Schedule(() => failed(e));
                }
            });

            void failed(Exception e)
            {
                Logger.Error(e, $"Couldn't swap the song of {map.Metadata.Title}");
                progress.Text = $"Couldn't swap the song: {e.Message}";
                progress.State = ProgressNotificationState.Cancelled;
                AlwaysPresent = false;
            }
        }

        /// <summary>Puts a set's own song back.</summary>
        public void Restore(BeatmapInfo map)
        {
            if (map.BeatmapSet == null)
                return;

            swaps.Restore(map.BeatmapSet);
            reloadIfPlaying(map);

            notifications?.Post(new SimpleNotification
            {
                Text = $"{map.Metadata.Title} plays to its own song again.",
                Icon = FontAwesome.Solid.Music,
            });
        }

        // The song in use reloaded, if it's this set's (else the old one plays on until it's picked again).
        private void reloadIfPlaying(BeatmapInfo map)
        {
            AlwaysPresent = false;

            if (working.Value?.BeatmapSetInfo?.ID != map.BeatmapSet?.ID)
                return;

            bool wasPlaying = music.IsPlaying;
            bool wasLooping = music.CurrentTrack.Looping;
            double position = music.CurrentTrack.CurrentTime;

            working.Value = beatmaps.GetWorkingBeatmap(working.Value!.BeatmapInfo, true);
            working.Value.PrepareTrackForPreview(wasLooping);

            // A freshly loaded track starts stopped. Resume here (where it was, faded in) rather than waiting for a song-select/leaderboard
            // refresh, or starting it over.
            if (wasPlaying)
            {
                music.SeekTo(position);
                music.Play();
                music.CurrentTrack.VolumeTo(0).Then().VolumeTo(1, 400, Easing.OutQuad);
            }
        }
    }
}
