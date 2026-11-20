// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Extensions;
using osu.Framework.Input.Events;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.SongSwap;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Overlays.SongSwap
{
    /// <summary>
    /// The songs a map can be played to (every one, closest tempo matches first), one to pick and hear first; whether short ones loop and
    /// stretched ones keep their pitch; and, when it's swapped already, its own song to put back.
    /// </summary>
    [LocalisableDescription(typeof(ScreenSongSwap), nameof(Title))]
    public partial class ScreenSongSwap : WizardScreen
    {
        public static LocalisableString Title => "Pick a song to play it to";

        /// <summary>Rows shown at once (the search narrows the rest down).</summary>
        private const int max_rows = 100;

        public override LocalisableString? NextStepText => Picked == null ? "Close" : "Swap song";

        /// <summary>The song picked (its map).</summary>
        public BeatmapInfo? Picked => picked.Value?.Song;

        [Resolved]
        private SongSwapOverlay overlay { get; set; } = null!;

        [Resolved]
        private SongSwapManager swaps { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        private readonly Bindable<Candidate?> picked = new Bindable<Candidate?>();
        private Bindable<bool> loop = null!;
        private Bindable<bool> keepPitch = null!;

        private RoundedButton previewButton = null!;
        private ITrackStore? previewStore;
        private Track? previewTrack;
        private DrawableTrack? mutedTrack;

        // Odd: a preview being made or playing. Bumped (to even) to stop it, so a preview made late is dropped.
        private int previewToken;

        private OsuTextFlowContainer description = null!;
        private TruncatingSpriteText mapTitle = null!;
        private OsuTextFlowContainer selectionText = null!;
        private OsuSpriteText resultCount = null!;
        private Container swappedBox = null!;
        private OsuTextFlowContainer swappedText = null!;
        private SearchTextBox search = null!;
        private FillFlowContainer<CandidateRow> list = null!;
        private OsuTextFlowContainer moreText = null!;
        private ScheduledDelegate? filterDelegate;

        private IReadOnlyList<Candidate> candidates = Array.Empty<Candidate>();

        /// <summary>A song, represented by the longest visible difficulty using that audio file.</summary>
        /// <param name="Loops">Whether it's shorter than the map (once stretched), so it'd be looped.</param>
        public record Candidate(BeatmapInfo Song, double Factor, double Stretch, bool Loops = false)
        {
            public string Title => $@"{Song.Metadata.Artist} - {Song.Metadata.Title}";

            public string Detail
            {
                get
                {
                    string pulse = Factor switch
                    {
                        4 => "Quarter-time match",
                        2 => "Half-time match",
                        0.5 => "Double-time match",
                        0.25 => "Quadruple-time match",
                        _ => "1:1 beat match",
                    };

                    return $@"{Song.BPM:0.#} BPM  ·  {pulse}{(Loops ? "  ·  loops (shorter than the map)" : string.Empty)}";
                }
            }

            public string Speed => $"{Stretch:0.00}x speed";

            /// <summary>How far its pitch moves when it's not kept (semitones).</summary>
            public string Pitch => $"{12 * Math.Log2(Stretch):+0.0;-0.0;0} semitones";

            public bool Matches(string[] terms) => terms.All(t => Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                                                                  || Song.Metadata.Source.Contains(t, StringComparison.OrdinalIgnoreCase)
                                                                  || Song.Metadata.Tags.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            loop = config.GetBindable<bool>(OsuSetting.SongSwapLoop);
            keepPitch = config.GetBindable<bool>(OsuSetting.SongSwapKeepPitch);

            Content.Children = new Drawable[]
            {
                mapTitle = new TruncatingSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Font = OsuFont.Default.With(size: 22, weight: FontWeight.SemiBold),
                    Colour = OverlayColourProvider.Content1,
                },
                description = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                swappedBox = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 10,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = OverlayColourProvider.Background4 },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding(15),
                            ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 10), new Dimension(GridSizeMode.Absolute, 220) },
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    swappedText = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                    },
                                    Empty(),
                                    new RoundedButton
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        Height = 40,
                                        Text = "Restore original song",
                                        Action = restore,
                                    },
                                }
                            }
                        },
                    }
                },
                new FormCheckBox
                {
                    Caption = "Loop songs shorter than the map",
                    HintText = "A bar-aligned part of the song (its chorus, when it has one) repeats to the end, crossfaded on the beat.",
                    Current = loop,
                },
                new FormCheckBox
                {
                    Caption = "Keep the song's pitch",
                    HintText = "Off: a sped up song sounds higher and a slowed down one lower, like nightcore or a record.",
                    Current = keepPitch,
                },
                search = new BasicSearchTextBox
                {
                    RelativeSizeAxes = Axes.X,
                    PlaceholderText = "Search songs, artists or tags",
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 10,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = OverlayColourProvider.Background4 },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding(12),
                            ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 10), new Dimension(GridSizeMode.Absolute, 160) },
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    selectionText = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 15))
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Colour = OverlayColourProvider.Content1,
                                    },
                                    Empty(),
                                    previewButton = new RoundedButton
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        Height = 40,
                                        Text = "Preview",
                                        Action = togglePreview,
                                    },
                                }
                            }
                        },
                    },
                },
                resultCount = new OsuSpriteText
                {
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                    Colour = OverlayColourProvider.Content2,
                },
                list = new FillFlowContainer<CandidateRow>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                },
                moreText = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content2,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    TextAnchor = Anchor.TopCentre,
                },
            };

            search.Current.BindValueChanged(_ =>
            {
                filterDelegate?.Cancel();
                filterDelegate = Scheduler.AddDelayed(filter, 150);
            });

            picked.BindValueChanged(_ =>
            {
                StopPreview();

                foreach (var row in list)
                    row.Selected = row.Candidate == picked.Value;

                updateSelectionText();

                // The wizard's button says what it'll do.
                overlay.NextButton?.Text = NextStepText ?? default;
            });

            keepPitch.BindValueChanged(_ =>
            {
                StopPreview();
                updateSelectionText();
            });

            // Short songs are offered (to loop) or not.
            loop.BindValueChanged(_ =>
            {
                StopPreview();
                Refresh();
            });
        }

        private void updateSelectionText(string? status = null)
        {
            string pitch = keepPitch.Value ? "pitch kept" : "pitch follows its speed";

            selectionText.Text = picked.Value is Candidate candidate
                ? $"{candidate.Title}  ·  {candidate.Speed}{(keepPitch.Value ? string.Empty : $" ({candidate.Pitch})")}\n"
                  + (status ?? $"Its best part lands on the map's, every beat on the map's beats, {pitch}{(candidate.Loops ? ", looped to the end" : string.Empty)}.")
                : $"Automatic beat sync  ·  {pitch}\nPick a song. Its tempo adapts to this map, including BPM changes.";

            previewButton.Enabled.Value = picked.Value != null;
        }

        private void togglePreview()
        {
            if (previewToken % 2 == 1)
                StopPreview();
            else
                startPreview();
        }

        private void startPreview()
        {
            if (overlay.Map is not BeatmapInfo map || picked.Value is not Candidate candidate)
                return;

            int token = ++previewToken;
            bool loops = loop.Value;
            bool pitch = keepPitch.Value;

            previewButton.Text = "Stop";
            updateSelectionText("Making a preview of the map's best part...");

            Task.Run(() => swaps.RenderPreview(map, candidate.Song, loops, pitch)).ContinueWith(t => Schedule(() =>
            {
                if (token != previewToken)
                    return;

                if (t.IsFaulted)
                {
                    Logger.Error(t.Exception, "Couldn't preview the song swap");
                    StopPreview();
                    updateSelectionText($"Can't play it to this song: {t.Exception?.InnerException?.Message}");
                    return;
                }

                play(t.GetResultSafely());
            }));
        }

        private void play(SongSwapManager.Preview preview)
        {
            string by = preview.By switch
            {
                SongSwapTiming.Anchor.Kiai => "its chorus (kiai) on the map's",
                SongSwapTiming.Anchor.Preview => "its preview point on the map's",
                _ => "its first notes on the map's",
            };

            updateSelectionText($"Previewing: {by}{(preview.Loops > 0 ? $", looped {preview.Loops}x" : string.Empty)}. Ticks are the map's notes.");

            // The song playing faded out under the preview, and back in after it.
            mutedTrack = music.CurrentTrack;
            mutedTrack.VolumeTo(0, 300, Easing.OutQuad);

            previewStore = audio.GetTrackStore(new SingleFileStore(preview.Song));
            previewTrack = previewStore.Get(SingleFileStore.NAME);
            previewTrack.Completed += () => Schedule(StopPreview);
            previewTrack.Start();
        }

        /// <summary>Stops a preview (being made or playing), and brings back the song that was playing.</summary>
        public void StopPreview()
        {
            stopPlayback(fade: true);

            if (IsLoaded)
            {
                previewButton.Text = "Preview";
                updateSelectionText();
            }
        }

        private void stopPlayback(bool fade)
        {
            if (previewToken % 2 == 1)
                previewToken++;

            previewTrack?.Stop();
            previewTrack?.Dispose();
            previewTrack = null;
            previewStore?.Dispose();
            previewStore = null;

            // Faded back in, or (off the update thread, while disposing) just put back.
            if (fade)
                mutedTrack?.VolumeTo(1, 300, Easing.OutQuad);
            else
                mutedTrack?.Volume.Value = 1;
            mutedTrack = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            stopPlayback(fade: false);
            base.Dispose(isDisposing);
        }

        /// <summary>A preview's wave file, as the one track of a track store.</summary>
        private class SingleFileStore : IResourceStore<byte[]>
        {
            public const string NAME = "song-swap-preview";

            private readonly byte[] data;

            public SingleFileStore(byte[] data) => this.data = data;

            public byte[] Get(string name) => data;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(data);

            public Stream GetStream(string name) => new MemoryStream(data);

            public IEnumerable<string> GetAvailableResources() => new[] { NAME };

            public void Dispose()
            {
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        /// <summary>The map's songs read again (the map's the overlay's).</summary>
        public void Refresh()
        {
            if (!IsLoaded || overlay.Map is not BeatmapInfo map)
                return;

            picked.Value = null;
            picked.TriggerChange();
            search.Current.Value = string.Empty;

            mapTitle.Text = $"{map.Metadata.Artist} - {map.Metadata.Title}";
            description.Text = $"[{map.DifficultyName}]  ·  {map.BPM:0.#} BPM\n"
                               + "Choose any song to play this map to. Its tempo follows the map's beat and its best part lands on the map's. Your map and scores stay unchanged.";

            string? swappedWith = map.BeatmapSet != null ? swaps.SwappedWith(map.BeatmapSet) : null;
            swappedBox.Alpha = swappedWith != null ? 1 : 0;
            swappedText.Text = swappedWith != null ? $"It plays to {swappedWith} at the moment." : string.Empty;

            candidates = ReadCandidates(realm, map, loop.Value);
            filter();
        }

        /// <summary>The songs (a map of each other set) a map can be played to, closest in BPM first.</summary>
        /// <param name="includeShort">Whether songs shorter than the map (once stretched) are offered, to loop.</param>
        public static IReadOnlyList<Candidate> ReadCandidates(RealmAccess realm, BeatmapInfo map, bool includeShort = false)
        {
            Guid ownSet = map.BeatmapSet?.ID ?? Guid.Empty;
            double bpm = map.BPM;

            return realm.Run(r => r.All<BeatmapSetInfo>()
                                   .Where(s => !s.DeletePending && !s.Protected)
                                   .AsEnumerable()
                                   .Where(s => s.ID != ownSet)
                                   .SelectMany(s => s.Beatmaps.Where(b => !b.Hidden && b.BPM > 0 && !string.IsNullOrEmpty(b.Metadata.AudioFile))
                                                    .GroupBy(b => b.Metadata.AudioFile, StringComparer.OrdinalIgnoreCase)
                                                    .Select(g => g.OrderByDescending(b => b.Length).First()))
                                   .Select(b => (beatmap: b, fit: SongSwapping.Fit(bpm, b.BPM)))
                                   // A difficulty's play span is a conservative estimate of its song's length.
                                   // Check after stretching: speeding a song up also makes it shorter.
                                   .Where(c => c.fit != null)
                                   .Select(c => (c.beatmap, c.fit, loops: !SongSwapping.LongEnough(c.beatmap.Length, c.fit!.Value.Stretch, map.Length)))
                                   .Where(c => includeShort || !c.loops)
                                   .OrderBy(c => Math.Abs(Math.Log(c.fit!.Value.Stretch)))
                                   .Select(c => new Candidate(c.beatmap.Detach(), c.fit!.Value.Factor, c.fit.Value.Stretch, c.loops))
                                   .ToList());
        }

        private void filter()
        {
            string[] terms = search.Current.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matching = candidates.Where(c => c.Matches(terms)).ToList();
            resultCount.Text = $"{matching.Count} {(matching.Count == 1 ? "song" : "songs")}  ·  Closest tempo first";

            list.Clear();

            foreach (var c in matching.Take(max_rows))
                list.Add(new CandidateRow(c, () => picked.Value = c) { Selected = c == picked.Value });

            moreText.Text = candidates.Count == 0
                ? loop.Value ? "No other songs were found." : "No songs long enough for this map were found. Try turning on looping."
                : matching.Count == 0
                    ? "No songs match your search. Try another artist, title or tag."
                    : matching.Count > max_rows
                        ? $"and {matching.Count - max_rows} more (search to find them)"
                        : string.Empty;
        }

        private void restore()
        {
            if (overlay.Map is not BeatmapInfo map)
                return;

            overlay.Restore(map);
            Refresh();
        }

        /// <summary>A song to pick: its name, then its BPM and how it'd be stretched.</summary>
        private partial class CandidateRow : OsuClickableContainer
        {
            public readonly Candidate Candidate;

            private Box background = null!;
            private SpriteIcon tick = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private bool selected;

            public bool Selected
            {
                get => selected;
                set
                {
                    selected = value;

                    if (IsLoaded)
                        updateState();
                }
            }

            public CandidateRow(Candidate candidate, Action pick)
            {
                Candidate = candidate;
                Action = pick;

                RelativeSizeAxes = Axes.X;
                Height = 64;
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    tick = new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 15,
                        Size = new Vector2(14),
                        Icon = FontAwesome.Solid.Music,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -15,
                        Text = Candidate.Speed,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                        Colour = colourProvider.Content1,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 42, Right = 155 },
                        Spacing = new Vector2(0, 4),
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = Candidate.Title,
                                Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                            },
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = Candidate.Detail,
                                Font = OsuFont.Default.With(size: 13),
                                Colour = colourProvider.Content2,
                            },
                        }
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateState();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            private void updateState()
            {
                background.FadeColour(selected ? colourProvider.Highlight1.Opacity(0.35f) : IsHovered ? colourProvider.Background3 : colourProvider.Background4, 150);
                tick.Icon = selected ? FontAwesome.Solid.Check : FontAwesome.Solid.Music;
                tick.FadeColour(selected ? colourProvider.Highlight1 : colourProvider.Content2, 150);
            }
        }
    }
}
