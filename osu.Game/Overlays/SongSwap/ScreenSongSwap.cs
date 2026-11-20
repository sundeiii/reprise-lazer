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
    /// The songs a map can be played to (closest tempo matches first), one to pick; and, when it's swapped
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

        /// <summary>A song that fits, represented by the longest visible difficulty using that audio file.</summary>
        public record Candidate(BeatmapInfo Song, double Factor, double Stretch)
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

                    return $@"{Song.BPM:0.#} BPM  ·  {pulse}";
                }
            }

            public string Speed => $"{Stretch:0.00}x base speed";

            public bool Matches(string[] terms) => terms.All(t => Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                                                                  || Song.Metadata.Source.Contains(t, StringComparison.OrdinalIgnoreCase)
                                                                  || Song.Metadata.Tags.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        [BackgroundDependencyLoader]
        private void load()
        {
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
                        selectionText = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 15))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Colour = OverlayColourProvider.Content1,
                            Padding = new MarginPadding(12),
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
                foreach (var row in list)
                    row.Selected = row.Candidate == picked.Value;

                selectionText.Text = picked.Value is Candidate candidate
                    ? $"{candidate.Title}  ·  {candidate.Speed}\nBeats are aligned and tempo changes are matched automatically. Pitch is preserved."
                    : "Automatic beat sync  ·  Pitch preserved\nPick a song. Its tempo adapts to this map, including BPM changes.";

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
            picked.TriggerChange();
            search.Current.Value = string.Empty;

            mapTitle.Text = $"{map.Metadata.Artist} - {map.Metadata.Title}";
            description.Text = $"[{map.DifficultyName}]  ·  {map.BPM:0.#} BPM\n"
                               + "Choose another song to play this map to. Its speed follows the map's beat, with pitch preserved. Your map and scores stay unchanged.";

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
                                   .SelectMany(s => s.Beatmaps.Where(b => !b.Hidden && b.BPM > 0 && !string.IsNullOrEmpty(b.Metadata.AudioFile))
                                                    .GroupBy(b => b.Metadata.AudioFile, StringComparer.OrdinalIgnoreCase)
                                                    .Select(g => g.OrderByDescending(b => b.Length).First()))
                                   .Select(b => (beatmap: b, fit: SongSwapping.Fit(bpm, b.BPM)))
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
            resultCount.Text = $"{matching.Count} {(matching.Count == 1 ? "song" : "songs")}  ·  Closest tempo first";

            list.Clear();

            foreach (var c in matching.Take(max_rows))
                list.Add(new CandidateRow(c, () => picked.Value = c) { Selected = c == picked.Value });

            moreText.Text = candidates.Count == 0
                ? "No songs long enough for this map were found. Try a shorter difficulty."
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
