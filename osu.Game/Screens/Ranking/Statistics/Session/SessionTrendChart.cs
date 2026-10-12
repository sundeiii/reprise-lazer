// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Layout;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics.Session
{
    /// <summary>
    /// A line chart of one metric over a series of plays, with labelled extremes,
    /// a change indicator for the latest play, and a tooltip on every point.
    /// </summary>
    public partial class SessionTrendChart : CompositeDrawable
    {
        private const float line_thickness = 2;
        private const float dot_size = 6;
        private const float latest_dot_size = 10;
        private const int max_points = 60;

        private readonly float plotHeight;
        private readonly float axisWidth;
        private readonly float padding;

        private readonly string title;
        private readonly Func<double, string> format;
        private readonly bool lowerIsBetter;
        private readonly List<(SessionPlayRecord Play, double Value)> points;

        private readonly List<Container> segments = new List<Container>();
        private readonly List<Circle> dots = new List<Circle>();
        private readonly List<ChartHoverColumn> columns = new List<ChartHoverColumn>();
        private readonly List<Box> gridLines = new List<Box>();
        private SpriteIcon? bestStar;
        private int bestIndex;
        private readonly LayoutValue layoutCache = new LayoutValue(Invalidation.DrawSize);

        private Container? plotArea;
        private Box? guideLine;
        private Vector2[] positions = Array.Empty<Vector2>();
        private double min;
        private double max;

        /// <param name="title">The heading of the chart.</param>
        /// <param name="plays">The plays to chart, oldest first. Plays without a value are skipped.</param>
        /// <param name="selector">Selects the charted value of a play.</param>
        /// <param name="format">Formats a value (or a difference between two values) for display.</param>
        /// <param name="lowerIsBetter">Whether a decrease is an improvement, which decides how the latest change is coloured.</param>
        /// <param name="compact">Whether to use a smaller plot, for places short on vertical space.</param>
        public SessionTrendChart(string title, IReadOnlyList<SessionPlayRecord> plays, Func<SessionPlayRecord, double?> selector, Func<double, string> format, bool lowerIsBetter, bool compact = false)
        {
            this.title = title;
            this.format = format;
            this.lowerIsBetter = lowerIsBetter;

            plotHeight = compact ? 56 : 90;
            axisWidth = compact ? 46 : 52;
            padding = compact ? 7 : 10;

            points = plays.Select(p => (Play: p, Value: selector(p)))
                          .Where(p => p.Value != null)
                          .Select(p => (p.Play, p.Value!.Value))
                          .TakeLast(max_points)
                          .ToList();

            AddLayout(layoutCache);
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new[]
                {
                    createHeader(colours),
                    points.Count < 2 ? createPlaceholder() : createPlot(colours),
                }
            };
        }

        private Drawable createHeader(OsuColour colours)
        {
            var header = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = new OsuSpriteText
                {
                    Text = title,
                    Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.SemiBold),
                }
            };

            if (points.Count == 0)
                return header;

            var latest = new FillFlowContainer
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
            };

            double last = points[^1].Value;

            latest.Add(new OsuSpriteText
            {
                Text = format(last),
                Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.Bold),
            });

            if (points.Count > 1)
            {
                double delta = last - points[^2].Value;
                bool improved = lowerIsBetter ? delta < 0 : delta > 0;

                latest.Add(new OsuSpriteText
                {
                    Text = $"{(delta >= 0 ? "+" : "-")}{format(Math.Abs(delta))}",
                    Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.SemiBold),
                    Colour = delta == 0 ? Color4.White : improved ? colours.Green : colours.Red,
                });
            }

            header.Add(latest);
            return header;
        }

        private Drawable createPlaceholder() => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 30,
            Child = new OsuSpriteText
            {
                Text = "Play at least two maps to see a trend.",
                Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE),
                Colour = Color4.White.Opacity(0.6f),
            }
        };

        private Drawable createPlot(OsuColour colours)
        {
            min = points.Min(p => p.Value);
            max = points.Max(p => p.Value);

            // avoid a degenerate range when every play has the same value.
            if (max - min < 1e-9)
            {
                min -= 1;
                max += 1;
            }

            // the best play so far (the first one, if several are equally good) gets a star.
            bestIndex = 0;

            for (int i = 1; i < points.Count; i++)
            {
                if (lowerIsBetter ? points[i].Value < points[bestIndex].Value : points[i].Value > points[bestIndex].Value)
                    bestIndex = i;
            }

            var lineContainer = new Container { RelativeSizeAxes = Axes.Both };

            // shown at the hovered point.
            guideLine = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 1,
                Origin = Anchor.TopCentre,
                Colour = Color4.White,
                Alpha = 0,
            };
            var dotContainer = new Container { RelativeSizeAxes = Axes.Both };
            var columnContainer = new Container { RelativeSizeAxes = Axes.Both };

            for (int i = 0; i < 3; i++)
            {
                var line = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Colour = Color4.White.Opacity(0.1f),
                };

                gridLines.Add(line);
                lineContainer.Add(line);
            }

            for (int i = 0; i < points.Count; i++)
            {
                var (play, value) = points[i];
                bool isLatest = i == points.Count - 1;

                if (i < points.Count - 1)
                {
                    var segment = new Container
                    {
                        Masking = true,
                        CornerRadius = line_thickness / 2,
                        MaskingSmoothness = 1,
                        Height = line_thickness,
                        Origin = Anchor.CentreLeft,
                        Colour = colours.Blue,
                        Child = new Box { RelativeSizeAxes = Axes.Both },
                    };

                    segments.Add(segment);
                    lineContainer.Add(segment);
                }

                var dot = new Circle
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(isLatest ? latest_dot_size : dot_size),
                    Colour = isLatest ? colours.Yellow : colours.Blue,
                };

                dots.Add(dot);
                dotContainer.Add(dot);

                var column = new ChartHoverColumn($"{play.Beatmap}\n{(i == bestIndex ? "★ Best · " : string.Empty)}{format(value)} · {play.PlayedAt.LocalDateTime:HH:mm}");

                int index = i;
                column.HoverChanged += hovered => setHovered(index, hovered);

                columns.Add(column);
                columnContainer.Add(column);
            }

            dotContainer.Add(bestStar = new SpriteIcon
            {
                Icon = FontAwesome.Solid.Star,
                Size = new Vector2(11),
                Origin = Anchor.Centre,
                Colour = colours.Yellow,
            });

            plotArea = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Left = axisWidth },
                Children = new Drawable[] { guideLine, lineContainer, dotContainer, columnContainer },
            };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = plotHeight,
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = format(max),
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.CentreLeft,
                        Y = padding,
                        Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE - 2),
                        Colour = Color4.White.Opacity(0.6f),
                    },
                    new OsuSpriteText
                    {
                        Text = format(min),
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.CentreLeft,
                        Y = -padding,
                        Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE - 2),
                        Colour = Color4.White.Opacity(0.6f),
                    },
                    plotArea,
                }
            };
        }

        private void setHovered(int index, bool hovered)
        {
            if (guideLine == null || index >= positions.Length)
                return;

            if (hovered)
                guideLine.X = positions[index].X;

            guideLine.FadeTo(hovered ? 0.3f : 0, 100);
            dots[index].ScaleTo(hovered ? 1.6f : 1, 100, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();

            if (!layoutCache.IsValid && layout())
                layoutCache.Validate();
        }

        private bool layout()
        {
            if (plotArea == null || points.Count < 2)
                return true;

            float width = plotArea.ChildSize.X;
            float height = plotArea.ChildSize.Y;

            // sizes are not known until the chart has been laid out at least once.
            if (width <= 0 || height <= 0)
                return false;

            positions = new Vector2[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                float t = (float)((points[i].Value - min) / (max - min));

                positions[i] = new Vector2(
                    padding + i / (float)(points.Count - 1) * (width - 2 * padding),
                    padding + (1 - t) * (height - 2 * padding));
            }

            for (int i = 0; i < points.Count; i++)
            {
                dots[i].Position = positions[i];

                if (i == bestIndex && bestStar != null)
                    bestStar.Position = positions[i] - new Vector2(0, 11);

                // neighbouring columns meet halfway between their points, and the outer ones stop at the edge of the plot,
                // so a column never reaches outside the chart it belongs to.
                float left = i == 0 ? 0 : (positions[i - 1].X + positions[i].X) / 2;
                float right = i == points.Count - 1 ? width : (positions[i].X + positions[i + 1].X) / 2;

                columns[i].Position = new Vector2(left, 0);
                columns[i].Size = new Vector2(right - left, height);

                if (i < points.Count - 1)
                {
                    var delta = positions[i + 1] - positions[i];

                    segments[i].Position = positions[i];
                    segments[i].Width = delta.Length;
                    segments[i].Rotation = MathHelper.RadiansToDegrees(MathF.Atan2(delta.Y, delta.X));
                }
            }

            gridLines[0].Y = padding;
            gridLines[1].Y = height / 2;
            gridLines[2].Y = height - padding;

            return true;
        }
    }
}
