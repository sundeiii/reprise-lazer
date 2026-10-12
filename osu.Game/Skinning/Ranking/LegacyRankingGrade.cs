// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The large grade of a legacy ranking screen ("ranking-X", "ranking-S", ...).
    /// </summary>
    /// <remarks>
    /// Centred 192px from the right edge of the screen and 320px from the top (legacy skin v2.0+, 1024x768 layout).
    /// Place this in a container spanning the screen.
    /// </remarks>
    public partial class LegacyRankingGrade : CompositeDrawable
    {
        public static readonly Vector2 POSITION_FROM_TOP_RIGHT = new Vector2(-192, 320);

        private readonly ScoreRank rank;

        public LegacyRankingGrade(ScoreRank rank)
        {
            this.rank = rank;

            Anchor = Anchor.TopRight;
            Origin = Anchor.Centre;
            Position = POSITION_FROM_TOP_RIGHT;
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // Old skins: 170 rather than 200 (stable's 640x480).
            if (LegacyRankingScreen.UsesOldLayout(skin))
                Y = 170 * 1.6f;

            InternalChild = new Sprite
            {
                Texture = skin.GetTexture(DrawableRank.GetLegacyRankTextureName(rank)),
            };
        }
    }
}
