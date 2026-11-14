// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The "ranking-graph" of a legacy ranking screen, with the player's health over the course of the play drawn on it:
    /// green while above half, red below.
    /// </summary>
    /// <remarks>
    /// "ranking-graph" sits at (256,608) in the legacy 1024x768 layout (skin v2.0+); its box starts 7px in and is 301x141.
    /// </remarks>
    public partial class LegacyRankingGraph : CompositeDrawable
    {
        public static readonly Vector2 POSITION = new Vector2(256, 608);

        private static readonly Vector2 box_offset = new Vector2(7);
        private static readonly Vector2 box_size = new Vector2(301, 141);

        private static readonly Color4 colour_high = new Color4(153, 255, 0, 255);
        private static readonly Color4 colour_low = new Color4(255, 64, 64, 255);

        private readonly IReadOnlyList<Vector2> health;

        /// <param name="health">
        /// The health over the play, as (progress, health) points with both in 0..1, in order of progress.
        /// Empty for no line.
        /// </param>
        public LegacyRankingGraph(IReadOnlyList<Vector2> health)
        {
            this.health = health;

            Position = POSITION;
            AutoSizeAxes = Axes.Both;
        }

        /// <summary>
        /// The points for the graph from a score's recorded health (<see cref="Scoring.ScoreInfo.HealthGraph"/>, kept in its replay):
        /// at most about 100, every 2nd, 4th, ... one as stable's results screen takes them, with the last always in.
        /// </summary>
        public static IReadOnlyList<Vector2> FromHealthGraph(IReadOnlyList<HealthValue> graph)
        {
            if (graph.Count < 2)
                return [];

            double startTime = graph[0].Time;
            double timeRange = graph[^1].Time - startTime;

            if (timeRange <= 0)
                return [];

            int stride = 1;
            while (graph.Count / stride > 100)
                stride *= 2;

            var points = new List<Vector2>(graph.Count / stride + 2);

            for (int i = 0; i < graph.Count; i += stride)
                points.Add(point(graph[i]));

            if ((graph.Count - 1) % stride != 0)
                points.Add(point(graph[^1]));

            return points;

            Vector2 point(HealthValue v) => new Vector2((float)((v.Time - startTime) / timeRange), (float)System.Math.Clamp(v.Health, 0, 1));
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // Old skins: 360 rather than 380 (stable's 640x480).
            if (LegacyRankingScreen.UsesOldLayout(skin))
                Y = 360 * 1.6f;

            var lines = new Container
            {
                Position = box_offset,
                Size = box_size,
                Masking = true,
            };

            InternalChildren = new Drawable[]
            {
                new Sprite { Texture = skin.GetTexture(@"ranking-graph") },
                lines,
            };

            // One line per run of points on the same side of half health, each starting where the previous one ended.
            foreach (var run in runs())
            {
                var path = new Path
                {
                    PathRadius = 1.5f,
                    Colour = run.High ? colour_high : colour_low,
                };
                path.Vertices = run.Points.Select(p => new Vector2(p.X * box_size.X, (1 - p.Y) * box_size.Y)).ToList();
                lines.Add(path);
            }
        }

        // Each segment takes the colour of where it ends; consecutive segments of one colour make one line.
        private IEnumerable<(bool High, List<Vector2> Points)> runs()
        {
            List<Vector2>? current = null;
            bool high = false;

            for (int i = 1; i < health.Count; i++)
            {
                bool segmentHigh = health[i].Y >= 0.5f;

                if (current == null || segmentHigh != high)
                {
                    if (current != null)
                        yield return (high, current);

                    current = new List<Vector2> { health[i - 1] };
                    high = segmentHigh;
                }

                current.Add(health[i]);
            }

            if (current != null)
                yield return (high, current);
        }
    }
}
