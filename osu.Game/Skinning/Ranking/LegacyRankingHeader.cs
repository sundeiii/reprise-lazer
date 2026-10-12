// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The top bar of a legacy ranking screen: the beatmap, its mapper and who played it when, on a dark strip,
    /// with "ranking-title" on the right.
    /// </summary>
    /// <remarks>
    /// Measured against stable's ranking screen at 1024x768. Place this in a container spanning the screen.
    /// </remarks>
    public partial class LegacyRankingHeader : CompositeDrawable
    {
        private const float bar_height = 96;
        private const float text_left = 5;
        private const float title_from_right = 32;

        private readonly ScoreInfo score;

        public LegacyRankingHeader(ScoreInfo score)
        {
            this.score = score;

            RelativeSizeAxes = Axes.X;
            Height = bar_height;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var beatmap = score.BeatmapInfo;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.8f,
                },
                // Lines placed where stable's are (their tops at about 6, 36 and 58); stable's font is wider than Torus,
                // so matching its widths too would make the lines taller than the bar.
                new OsuSpriteText
                {
                    Position = new Vector2(text_left, 2),
                    Text = beatmap == null ? string.Empty : $"{beatmap.Metadata.Artist} - {beatmap.Metadata.Title} [{beatmap.DifficultyName}]",
                    Font = OsuFont.GetFont(size: 32),
                },
                new OsuSpriteText
                {
                    Position = new Vector2(text_left, 33),
                    Text = beatmap == null ? string.Empty : $"Beatmap by {beatmap.Metadata.Author.Username}",
                    Font = OsuFont.GetFont(size: 26),
                },
                new OsuSpriteText
                {
                    Position = new Vector2(text_left, 56),
                    Text = $"Played by {score.User.Username} on {score.Date.ToLocalTime().ToString("G", CultureInfo.CurrentCulture)}.",
                    Font = OsuFont.GetFont(size: 26),
                },
                new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    X = -title_from_right,
                    Texture = skin.GetTexture(@"ranking-title"),
                },
            };
        }
    }
}
