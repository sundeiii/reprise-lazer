// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osuTK;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The left side of a legacy ranking screen: the "ranking-panel" background, with the total score, the judgement counts,
    /// and the max combo and accuracy of a score drawn on top of it.
    /// </summary>
    /// <remarks>
    /// Positions are in the legacy 1024x768 layout (the space the osu! wiki documents legacy elements in), relative to the panel's top-left.
    /// </remarks>
    public partial class LegacyRankingPanel : CompositeDrawable
    {
        // The panel's top-left on screen (legacy skin v2.0+).
        public static readonly Vector2 PANEL_POSITION = new Vector2(0, 102);

        // Measured against stable's ranking screen at 1024x768 (default skin).

        // Centred on (352, 150) on screen, like stable's (220, 94 in its 640x480): wider score fonts grow both ways.
        private static readonly Vector2 score_position = new Vector2(352, 150.4f) - PANEL_POSITION;
        private const float score_scale = 1.3f;

        // Judgement rows: an icon (centred, at the skin's size) and its count (left-aligned beside it), in two columns of three.
        private static readonly Vector2 left_column = new Vector2(64, 154);
        private static readonly Vector2 right_column = new Vector2(384, 154);
        private const float row_spacing = 96;
        private const float count_offset = 64;
        private const float icon_scale = 1;
        private const float count_scale = 1.1f;

        // ranking-maxcombo / ranking-accuracy (legacy skin v2.0+: (8,480) / (291,480) on screen), with the values below them.
        private static readonly Vector2 max_combo_label_position = new Vector2(8, 480) - PANEL_POSITION;
        private static readonly Vector2 accuracy_label_position = new Vector2(291, 480) - PANEL_POSITION;
        private static readonly Vector2 value_offset = new Vector2(18, 52);

        private readonly ScoreInfo score;

        private LegacySpriteText scoreText = null!;
        private readonly Bindable<ScoringMode> scoringMode = new Bindable<ScoringMode>();

        public LegacyRankingPanel(ScoreInfo score)
        {
            this.score = score;

            Position = PANEL_POSITION;
            AutoSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The game's score display (standardised or classic), as everywhere else.
            scoringMode.BindValueChanged(mode => scoreText.Text = score.GetDisplayScore(mode.NewValue).ToString(@"00000000", CultureInfo.InvariantCulture), true);
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ScoreDisplayMode, scoringMode);

            // Old skins (stable's 640x480: the panel at 46 rather than 64, the labels 12 lower, the score at 1.05).
            bool old = LegacyRankingScreen.UsesOldLayout(skin);
            var labelOffset = new Vector2(0, old ? 12 * 1.6f : 0);

            InternalChildren = new Drawable[]
            {
                new Sprite
                {
                    Texture = skin.GetTexture(@"ranking-panel"),
                    Y = old ? (46 - 64) * 1.6f : 0,
                },
                scoreText = new LegacySpriteText(LegacyFont.Score)
                {
                    Origin = Anchor.Centre,
                    Position = score_position,
                    Scale = new Vector2(old ? 1.05f : score_scale),
                },
                createRow(@"hit300", score.GetCount300(), left_column),
                createRow(@"hit100", score.GetCount100(), left_column + new Vector2(0, row_spacing)),
                createRow(@"hit50", score.GetCount50(), left_column + new Vector2(0, row_spacing * 2)),
                createRow(@"hit300g", score.GetCountGeki(), right_column),
                createRow(@"hit100k", score.GetCountKatu(), right_column + new Vector2(0, row_spacing)),
                createRow(@"hit0", score.GetCountMiss(), right_column + new Vector2(0, row_spacing * 2)),
                createLabel(@"ranking-maxcombo", max_combo_label_position + labelOffset),
                createValue(score.MaxCombo.ToString(CultureInfo.InvariantCulture) + @"x", max_combo_label_position + value_offset),
                createLabel(@"ranking-accuracy", accuracy_label_position + labelOffset),
                createValue((score.Accuracy * 100).ToString(@"0.00", CultureInfo.InvariantCulture) + @"%", accuracy_label_position + value_offset),
            };

            Drawable createRow(string judgement, int? count, Vector2 position) => new Container
            {
                Position = position,
                AutoSizeAxes = Axes.Both,
                Children = new[]
                {
                    // Only the first frame of an animated judgement is shown here.
                    skin.GetAnimation(judgement, false, false)?.With(d =>
                    {
                        d.Anchor = Anchor.TopLeft;
                        d.Origin = Anchor.Centre;
                        d.Scale = new Vector2(icon_scale);
                    }) ?? Empty(),
                    new LegacySpriteText(LegacyFont.Score)
                    {
                        Origin = Anchor.CentreLeft,
                        X = count_offset,
                        Scale = new Vector2(count_scale),
                        Text = (count ?? 0).ToString(CultureInfo.InvariantCulture) + @"x",
                    },
                }
            };

            Drawable createLabel(string texture, Vector2 position) => new Sprite
            {
                Texture = skin.GetTexture(texture),
                Position = position,
            };

            Drawable createValue(string text, Vector2 position) => new LegacySpriteText(LegacyFont.Score)
            {
                Text = text,
                Position = position,
                Scale = new Vector2(count_scale),
            };
        }
    }
}
