// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The "ranking-perfect" stamp of a legacy ranking screen, shown for a full combo.
    /// </summary>
    /// <remarks>
    /// Centred at (416,688) in the legacy 1024x768 layout (skin v2.0+). Only the first frame of an animated one is shown.
    /// </remarks>
    public partial class LegacyRankingPerfect : CompositeDrawable
    {
        public static readonly Vector2 POSITION = new Vector2(416, 688);

        private readonly ScoreInfo score;

        public LegacyRankingPerfect(ScoreInfo score)
        {
            this.score = score;

            Position = POSITION;
            Origin = Anchor.Centre;
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // Old skins: 200 across rather than 260 (stable's 640x480).
            if (LegacyRankingScreen.UsesOldLayout(skin))
                X = 200 * 1.6f;

            if (score.MaxCombo != score.GetMaximumAchievableCombo())
                return;

            var stamp = skin.GetAnimation(@"ranking-perfect", false, false);

            if (stamp != null)
                InternalChild = stamp;
        }
    }
}
