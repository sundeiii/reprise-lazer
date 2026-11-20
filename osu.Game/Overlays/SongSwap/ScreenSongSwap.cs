// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.SongSwap;
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
    /// Reprise: the songs a map can be played to (the same, half or double BPM, closest first), one to pick; and, when it's swapped
    /// already, its own song to put back.
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

        private readonly Bindable<Candidate?> picked = new Bindable<Candidate?>();

        private OsuTextFlowContainer description = null!;
        private Container swappedBox = null!;
        private OsuTextFlowContainer swappedText = null!;
        private SearchTextBox search = null!;
        private FillFlowContainer<CandidateRow> list = null!;
        private OsuTextFlowContainer moreText = null!;
        private ScheduledDelegate? filterDelegate;

        private IReadOnlyList<Candidate> candidates = Array.Empty<Candidate>();

        /// <summary>A song that fits, by its set's longest visible map with a song.</summary>
        public record Candidate(BeatmapInfo Song, double Factor, double Stretch)
        {
            public string Title => $@"{Song.Metadata.Artist} - {Song.Metadata.Title}";

            public string Detail
            {
                get
                {
                    string time = Factor switch
                    {
                        2 => " (as double time)",
                        0.5 => " (as half time)",
                        _ => string.Empty,
                    };

                    string stretch = Math.Abs(Stretch - 1) < 0.005 ? "as it is" : $"at {Stretch:0.##}x speed";
                    return $@"{Song.BPM:0.#} BPM{time}, {stretch}";
                }
            }

            public bool Matches(string[] terms) => terms.All(t => Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                                                                  || Song.Metadata.Source.Contains(t, StringComparison.OrdinalIgnoreCase)
                                                                  || Song.Metadata.Tags.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Content.Children = new Drawable[]
            {
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
                search = new BasicSearchTextBox
                {
                    RelativeSizeAxes = Axes.X,
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
                foreach (var row in list)
                    row.Selected = row.Candidate == picked.Value;

                // The wizard's button says what it'll do.
                overlay.NextButton?.Text = NextStepText ?? default;
            });
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
            search.Current.Value = string.Empty;

            description.Text = $"{map.Metadata.Artist} - {map.Metadata.Title} [{map.DifficultyName}] is {map.BPM:0.#} BPM. Pick a song of the same, half or "
                               + "double BPM to play it to: it's stretched to fit (its pitch kept) and lined up on the beat. Only the song changes, "
                               + "not the map, so plays on it still count. Only songs long enough to cover the whole map are listed.";

            string? swappedWith = map.BeatmapSet != null ? swaps.SwappedWith(map.BeatmapSet) : null;
            swappedBox.Alpha = swappedWith != null ? 1 : 0;
            swappedText.Text = swappedWith != null ? $"It plays to {swappedWith} at the moment." : string.Empty;

            candidates = ReadCandidates(realm, map);
            filter();
        }

        /// <summary>The songs (a map of each other set) a map can be played to, closest in BPM first.</summary>
        public static IReadOnlyList<Candidate> ReadCandidates(RealmAccess realm, BeatmapInfo map)
        {
            Guid ownSet = map.BeatmapSet?.ID ?? Guid.Empty;
            double bpm = map.BPM;

            return realm.Run(r => r.All<BeatmapSetInfo>()
                                   .Where(s => !s.DeletePending && !s.Protected)
                                   .AsEnumerable()
                                   .Where(s => s.ID != ownSet)
                                   .Select(s => s.Beatmaps.Where(b => !b.Hidden && b.BPM > 0 && !string.IsNullOrEmpty(b.Metadata.AudioFile))
                                                 .OrderByDescending(b => b.Length)
                                                 .FirstOrDefault())
                                   .Where(b => b != null)
                                   .Select(b => (beatmap: b!, fit: SongSwapping.Fit(bpm, b!.BPM)))
                                   // A difficulty's play span is a conservative estimate of its song's length.
                                   // Check after stretching: speeding a song up also makes it shorter.
                                   .Where(c => c.fit != null && SongSwapping.LongEnough(c.beatmap.Length, c.fit.Value.Stretch, map.Length))
                                   .OrderBy(c => Math.Abs(Math.Log(c.fit!.Value.Stretch)))
                                   .Select(c => new Candidate(c.beatmap.Detach(), c.fit!.Value.Factor, c.fit.Value.Stretch))
                                   .ToList());
        }

        private void filter()
        {
            string[] terms = search.Current.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matching = candidates.Where(c => c.Matches(terms)).ToList();

            list.Clear();

            foreach (var c in matching.Take(max_rows))
                list.Add(new CandidateRow(c, () => picked.Value = c) { Selected = c == picked.Value });

            moreText.Text = candidates.Count == 0
                ? "No other song of yours fits this map's BPM (the same, half or double, within a quarter) and is long enough to cover it."
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
                Height = 50;
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
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 42, Right = 15 },
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = Candidate.Title,
                                Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                            },
                            new OsuSpriteText
                            {
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
                tick.FadeColour(selected ? colourProvider.Highlight1 : colourProvider.Content2, 150);
            }
        }
    }
}
