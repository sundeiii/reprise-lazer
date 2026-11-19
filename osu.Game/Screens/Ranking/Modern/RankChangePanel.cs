// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Overlays;
using osu.Game.Screens.RankingV2.Argon;
using osuTK;

namespace osu.Game.Screens.Ranking.Modern
{
    /// <summary>
    /// Reprise: what a play did to the player's global rank and total pp, next to the player on the results screen: each counting from
    /// before to after, with how much it went up (or down). Shows once the server's update for the play is in (<see cref="Show(ScoreBasedUserStatisticsUpdate)"/>).
    /// </summary>
    public partial class RankChangePanel : CompositeDrawable
    {
        public const float WIDTH = 360;
        // A little shorter than the player panel beside it, clear of the personal best badge below.
        private const float height = 52;
        private const double count_duration = 1500;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private Container content = null!;
        private OsuSpriteText rankText = null!;
        private OsuSpriteText rankChangeText = null!;
        private OsuSpriteText ppText = null!;
        private OsuSpriteText ppChangeText = null!;

        private double shownRank;
        private double shownPP;

        /// <summary>The global rank as it counts (rounded to show).</summary>
        public double ShownRank
        {
            get => shownRank;
            set
            {
                shownRank = value;
                rankText.Text = $"#{((long)Math.Round(value)).ToLocalisableString(@"N0")}";
            }
        }

        /// <summary>The total pp as it counts (rounded to show).</summary>
        public double ShownPP
        {
            get => shownPP;
            set
            {
                shownPP = value;
                ppText.Text = $"{Math.Round(value).ToLocalisableString(@"N0")}pp";
            }
        }

        /// <summary>Whether a change is shown (the server's update had one).</summary>
        public bool HasChange { get; private set; }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Width = WIDTH;
            Height = height;
            Alpha = 0;

            InternalChild = content = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Shear = OsuGame.SHEAR,
                CornerRadius = ShearedButton.CORNER_RADIUS,
                EdgeEffect = ArgonBeatmapInfoPanel.CreateShadowEdgeEffect(),
                Masking = true,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Shear = -OsuGame.SHEAR,
                        Padding = new MarginPadding { Horizontal = 20 },
                        ColumnDimensions = [new Dimension(), new Dimension()],
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                createStat("Global rank", out rankText, out rankChangeText, colourProvider),
                                createStat("Performance", out ppText, out ppChangeText, colourProvider),
                            }
                        },
                    },
                ],
            };
        }

        private static Drawable createStat(string label, out OsuSpriteText value, out OsuSpriteText change, OverlayColourProvider colourProvider) =>
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Direction = FillDirection.Vertical,
                Children =
                [
                    new OsuSpriteText
                    {
                        Text = label,
                        Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                        Colour = colourProvider.Content2,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Children =
                        [
                            value = new OsuSpriteText
                            {
                                Font = OsuFont.Style.Heading2.With(size: 22),
                            },
                            change = new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Margin = new MarginPadding { Bottom = 2 },
                                Font = OsuFont.Style.Body.With(size: 15, weight: FontWeight.Bold),
                            },
                        ],
                    },
                ],
            };

        /// <summary>
        /// Shows what the play did (counting from before to after), if it changed the rank or pp; otherwise stays hidden.
        /// </summary>
        public void Show(ScoreBasedUserStatisticsUpdate update)
        {
            ClearTransforms(false, nameof(ShownRank));
            ClearTransforms(false, nameof(ShownPP));

            int? rankBefore = update.Before.GlobalRank, rankAfter = update.After.GlobalRank;
            double ppBefore = (double)(update.Before.PP ?? 0), ppAfter = (double)(update.After.PP ?? 0);

            HasChange = rankAfter != null && (rankAfter != rankBefore || Math.Round(ppAfter) != Math.Round(ppBefore));

            if (!HasChange)
            {
                this.FadeOut(200);
                return;
            }

            // Up the rankings is a smaller number. Without a rank before (the first ranked play), it counts from the after.
            int rankGain = rankBefore != null ? rankBefore.Value - rankAfter!.Value : 0;
            setChange(rankChangeText, rankGain, $"{(rankGain > 0 ? "▲" : "▼")}{Math.Abs(rankGain).ToLocalisableString(@"N0")}");

            double ppGain = Math.Round(ppAfter) - Math.Round(ppBefore);
            setChange(ppChangeText, ppGain, $"{(ppGain > 0 ? "+" : "−")}{Math.Abs(ppGain).ToLocalisableString(@"N0")}");

            ShownRank = rankBefore ?? rankAfter!.Value;
            ShownPP = ppBefore;

            this.FadeIn(300, Easing.OutQuint);
            content.MoveToX(-20).MoveToX(0, 500, Easing.OutQuint);

            this.TransformTo(nameof(ShownRank), (double)rankAfter!.Value, count_duration, Easing.OutQuint);
            this.TransformTo(nameof(ShownPP), ppAfter, count_duration, Easing.OutQuint);

            rankChangeText.FadeOut().Delay(count_duration * 0.6).FadeIn(300);
            ppChangeText.FadeOut().Delay(count_duration * 0.6).FadeIn(300);
        }

        private void setChange(OsuSpriteText text, double gain, string formatted)
        {
            text.Alpha = gain == 0 ? 0 : 1;
            text.Text = gain == 0 ? string.Empty : formatted;
            text.Colour = gain > 0 ? colours.Lime1 : colours.Red1;
        }
    }
}
