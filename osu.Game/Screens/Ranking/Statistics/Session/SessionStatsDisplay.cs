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
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics.Session
{
    /// <summary>
    /// Shows how a set of plays went: summary numbers, unstable rate and accuracy trends and,
    /// when detailed, the hit error distribution, a comparison between setups and the individual plays.
    /// </summary>
    public partial class SessionStatsDisplay : CompositeDrawable
    {
        private const float column_spacing = 30;
        private const int max_listed_plays = 100;

        [Resolved]
        private SessionStatsStore? store { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly IReadOnlyList<SessionPlayRecord>? plays;
        private readonly bool detailed;
        private readonly Action<SessionPlayRecord>? playClicked;


        private IReadOnlyList<SessionPlayRecord> shownPlays = null!;

        /// <param name="plays">The plays to show, oldest first. Defaults to the plays of the current session.</param>
        /// <param name="detailed">
        /// Whether to also show the combined hit error distribution, the comparison between setups and a list of individual plays.
        /// Leave off where a single play's own statistics are already shown next to it, like the results screen.
        /// </param>
        /// <param name="playClicked">Invoked when a play in the list of individual plays is clicked. Only used when <paramref name="detailed"/>.</param>
        public SessionStatsDisplay(IReadOnlyList<SessionPlayRecord>? plays = null, bool detailed = false, Action<SessionPlayRecord>? playClicked = null)
        {
            this.plays = plays;
            this.detailed = detailed;
            this.playClicked = playClicked;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            shownPlays = plays ?? store?.CurrentSessionPlays ?? [];
            var summary = SessionSummary.Create(shownPlays);

            var table = new SimpleStatisticTable(2, new SimpleStatisticItem[]
            {
                new TextItem("Plays", summary.PlayCount.ToString()),
                new TextItem("Average accuracy", formatAccuracy(summary.AverageAccuracy)),
                new TextItem("Average unstable rate", formatNumber(summary.AverageUnstableRate)),
                new TextItem("Best unstable rate", formatNumber(summary.BestUnstableRate)),
                new TextItem("Average hit error", formatHitError(summary.AverageHitError)),
                new TextItem("Total misses", summary.TotalMisses.ToString()),
            });

            InternalChild = detailed ? createDetailed(table) : createCompact(table);
        }

        /// <summary>
        /// A single short row, so the results screen (which already has plenty of other statistics) can still show everything on one screen.
        /// </summary>
        private Drawable createCompact(Drawable table)
        {
            if (shownPlays.Count == 0)
                return table;

            Drawable left = table;
            var badges = createPersonalBestBadges();

            if (badges != null)
            {
                left = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Children = new[] { badges, table },
                };
            }

            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Relative, 0.32f),
                    new Dimension(GridSizeMode.Absolute, column_spacing),
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, column_spacing),
                    new Dimension(),
                },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[]
                {
                    new[]
                    {
                        left,
                        new Container(),
                        createUnstableRateChart(compact: true),
                        new Container(),
                        createAccuracyChart(compact: true),
                    }
                },
            };
        }

        /// <summary>
        /// Points out when the latest play of the session beat every earlier play of the same map and kind of setup.
        /// Only shown for the current session, i.e. next to a play which has just finished.
        /// </summary>
        private Drawable? createPersonalBestBadges()
        {
            if (plays != null || store == null || shownPlays.Count == 0)
                return null;

            var personalBests = store.GetPersonalBests(shownPlays[^1]);

            if (!personalBests.HasAnyNewBest)
                return null;

            var badges = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(14, 0),
            };

            if (personalBests.NewBestUnstableRate)
                badges.Add(createBadge("New best unstable rate on this map"));

            if (personalBests.NewBestAccuracy)
                badges.Add(createBadge("New best accuracy on this map"));

            return badges;
        }

        private Drawable createBadge(string text) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(5, 0),
            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    Icon = FontAwesome.Solid.Star,
                    Size = new Vector2(12),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Colour = colours.Yellow,
                },
                new OsuSpriteText
                {
                    Text = text,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.Bold),
                    Colour = colours.Yellow,
                },
            }
        };

        private Drawable createDetailed(Drawable table)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 16),
                Child = table,
            };

            if (shownPlays.Count > 0)
            {
                flow.Add(createTwoColumns(
                    createUnstableRateChart(compact: false),
                    createAccuracyChart(compact: false)));

                flow.Add(createTwoColumns(
                    new HitErrorHistogramChart(SessionSummary.CombineHistograms(shownPlays)),
                    new Container()));

                flow.Add(createPlayList());
            }

            return flow;
        }

        private SessionTrendChart createUnstableRateChart(bool compact)
            => new SessionTrendChart(compact ? "Unstable rate" : "Unstable rate (lower is better)", shownPlays, p => p.UnstableRate, v => v.ToString("N2"), lowerIsBetter: true, compact);

        private SessionTrendChart createAccuracyChart(bool compact)
            => new SessionTrendChart("Accuracy", shownPlays, p => p.Accuracy, formatAccuracy, lowerIsBetter: false, compact);

        private static Drawable createTwoColumns(Drawable left, Drawable right) => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            ColumnDimensions = new[]
            {
                new Dimension(),
                new Dimension(GridSizeMode.Absolute, column_spacing),
                new Dimension(),
            },
            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
            Content = new[] { new[] { left, new Container(), right } },
        };

        private Drawable createPlayList()
        {
            var rows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
            };

            rows.Add(new OsuSpriteText
            {
                Text = shownPlays.Count > max_listed_plays ? $"Plays (latest {max_listed_plays} of {shownPlays.Count})" : "Plays",
                Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.SemiBold),
            });

            rows.Add(createPlayRow("Time", "Beatmap", "Mods", "Accuracy", "UR", "Hit error", "Misses", isHeader: true));

            foreach (var play in shownPlays.Reverse().Take(max_listed_plays))
            {
                var row = createPlayRow(
                    play.PlayedAt.LocalDateTime.ToString("d MMM HH:mm"),
                    play.Beatmap,
                    play.Mods.Length == 0 ? "NM" : string.Concat(play.Mods),
                    formatAccuracy(play.Accuracy),
                    formatNumber(play.UnstableRate),
                    formatHitError(play.AverageHitError),
                    play.MissCount.ToString(),
                    isHeader: false);

                var captured = play;
                rows.Add(new PlayRow(row, playClicked == null ? null : () => playClicked(captured)));
            }

            return rows;
        }

        private static Drawable createPlayRow(string time, string beatmap, string mods, string accuracy, string unstableRate, string hitError, string misses, bool isHeader)
        {
            var font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: isHeader ? FontWeight.Bold : FontWeight.Regular);
            Color4 colour = isHeader ? Color4.White.Opacity(0.6f) : Color4.White;

            // long texts are truncated (with the full text available as a tooltip) rather than spilling into the next column.
            Drawable cell(string text, bool truncate = false) => truncate
                ? new TruncatingSpriteText
                {
                    Text = text,
                    Font = font,
                    Colour = colour,
                    RelativeSizeAxes = Axes.X,
                }
                : new OsuSpriteText
                {
                    Text = text,
                    Font = font,
                    Colour = colour,
                };

            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 110),
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, 140),
                    new Dimension(GridSizeMode.Absolute, 80),
                    new Dimension(GridSizeMode.Absolute, 70),
                    new Dimension(GridSizeMode.Absolute, 120),
                    new Dimension(GridSizeMode.Absolute, 60),
                },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[]
                {
                    new[]
                    {
                        cell(time),
                        cell(beatmap, truncate: true),
                        cell(mods, truncate: true),
                        cell(accuracy),
                        cell(unstableRate),
                        cell(hitError),
                        cell(misses),
                    }
                },
            };
        }

        // the game's own formatter, so accuracy never shows a different number than the results screen does.
        private static string formatAccuracy(double accuracy) => accuracy.FormatAccuracy().ToString();

        private static string formatNumber(double? value) => value?.ToString("N2") ?? "-";

        private static string formatHitError(double? value)
            => value == null ? "-" : $"{Math.Abs(value.Value):N2} ms {(value.Value < 0 ? "early" : "late")}";

        private partial class TextItem : SimpleStatisticItem<string>
        {
            public TextItem(LocalisableString name, string value)
                : base(name)
            {
                Value = value;
            }

            protected override LocalisableString DisplayValue(string value) => value;
        }

        /// <summary>
        /// A row of the play list, which lights up under the mouse and does something when clicked (if there is anything to do).
        /// </summary>
        private partial class PlayRow : OsuClickableContainer
        {
            private readonly Box highlight;

            public PlayRow(Drawable content, Action? action)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                if (action != null)
                {
                    Action = action;
                    TooltipText = "Open this play";
                }

                Children = new[]
                {
                    highlight = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    content,
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                highlight.FadeTo(0.08f, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);
                highlight.FadeOut(150);
            }
        }
    }
}
