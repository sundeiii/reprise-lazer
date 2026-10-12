// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics.Session
{
    /// <summary>
    /// Shows how hit errors were distributed, from the histogram bins saved with each <see cref="SessionPlayRecord"/>.
    /// </summary>
    public partial class HitErrorHistogramChart : CompositeDrawable
    {
        private const float plot_height = 90;

        private const float idle_alpha = 0.75f;

        private readonly int[] bins;
        private readonly List<Box> bars = new List<Box>();

        /// <param name="bins">Hit counts per bin, laid out as described by <see cref="SessionPlayRecord.HitErrorHistogram"/>.</param>
        public HitErrorHistogramChart(int[] bins)
        {
            this.bins = bins;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            int total = bins.Sum();

            var header = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = "Hit error distribution",
                        Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.SemiBold),
                    },
                    new OsuSpriteText
                    {
                        Text = $"{total:N0} hits",
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE, weight: FontWeight.Bold),
                    },
                }
            };

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new[]
                {
                    header,
                    total == 0 ? createPlaceholder() : createPlot(colours, total),
                }
            };
        }

        private static Drawable createPlaceholder() => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 30,
            Child = new OsuSpriteText
            {
                Text = "No hit data yet.",
                Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE),
                Colour = Color4.White.Opacity(0.6f),
            }
        };

        private Drawable createPlot(OsuColour colours, int total)
        {
            int binCount = bins.Length;
            int half = SessionPlayRecord.HISTOGRAM_HALF_BINS;
            int max = bins.Max();

            var plot = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = plot_height,
            };

            for (int i = 0; i < binCount; i++)
            {
                bool isCentre = i == half;

                var bar = new Box
                {
                    Alpha = idle_alpha,
                    RelativePositionAxes = Axes.X,
                    RelativeSizeAxes = Axes.Both,
                    X = (i + 0.1f) / binCount,
                    Width = 0.8f / binCount,
                    // never let a non-empty bin disappear.
                    Height = bins[i] == 0 ? 0 : Math.Max(bins[i] / (float)max, 0.02f),
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Colour = isCentre ? Color4.White : colours.Blue,
                };

                bars.Add(bar);
                plot.Add(bar);
            }

            for (int i = 0; i < binCount; i++)
            {
                var column = new ChartHoverColumn($"{describeBin(i)}: {bins[i]:N0} hits ({bins[i] / (double)total:P1})")
                {
                    RelativePositionAxes = Axes.X,
                    RelativeSizeAxes = Axes.Both,
                    X = i / (float)binCount,
                    Width = 1f / binCount,
                };

                int index = i;
                column.HoverChanged += hovered => bars[index].FadeTo(hovered ? 1f : idle_alpha, 100);

                plot.Add(column);
            }

            var axis = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 14,
            };

            foreach (int ms in new[] { -100, -50, 0, 50, 100 })
            {
                int bin = ms / SessionPlayRecord.HISTOGRAM_BIN_WIDTH + half;

                axis.Add(new OsuSpriteText
                {
                    Text = ms > 0 ? $"+{ms}" : $"{ms}",
                    RelativePositionAxes = Axes.X,
                    X = (bin + 0.5f) / binCount,
                    Origin = Anchor.TopCentre,
                    Font = OsuFont.GetFont(size: StatisticItem.FONT_SIZE - 2),
                    Colour = Color4.White.Opacity(0.6f),
                });
            }

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[] { plot, axis },
            };
        }

        private string describeBin(int bin)
        {
            int half = SessionPlayRecord.HISTOGRAM_HALF_BINS;
            int ms = (bin - half) * SessionPlayRecord.HISTOGRAM_BIN_WIDTH;

            // the outermost bins also hold everything beyond them.
            if (bin == 0)
                return $"{ms} ms or earlier";

            if (bin == bins.Length - 1)
                return $"+{ms} ms or later";

            return ms > 0 ? $"+{ms} ms" : $"{ms} ms";
        }
    }
}
