// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    public partial class StableProfileDeltaPanel : CompositeDrawable
    {
        private readonly ScoreInfo score;
        private readonly bool showForThisScore;

        private readonly IBindable<ScoreBasedUserStatisticsUpdate?> latestStatisticsUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();

        private OsuSpriteText rankText = null!;
        private OsuSpriteText ppText = null!;
        private OsuSpriteText accuracyText = null!;
        private OsuSpriteText statusText = null!;

        public StableProfileDeltaPanel(ScoreInfo score, bool showForThisScore)
        {
            this.score = score;
            this.showForThisScore = showForThisScore;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = new Vector2(245, 235);
            Alpha = showForThisScore ? 1 : 0;
        }

        [BackgroundDependencyLoader]
        private void load(UserStatisticsWatcher? userStatisticsWatcher)
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 5,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.68f),
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 46,
                        Colour = Color4.Black.Opacity(0.28f),
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Margin = new MarginPadding { Left = 20, Top = 16, Right = 20 },
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 14),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = "PROFILE UPDATE",
                                Font = OsuFont.Torus.With(size: 15, weight: FontWeight.Bold),
                                Colour = Color4Extensions.FromHex("#ffd966"),
                            },
                            statusText = new OsuSpriteText
                            {
                                Text = showForThisScore ? "waiting..." : "—",
                                Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold),
                                Colour = Color4.White.Opacity(0.65f),
                            },
                            profileRow("GLOBAL RANK", out rankText),
                            profileRow("PERFORMANCE", out ppText),
                            profileRow("ACCURACY", out accuracyText),
                        }
                    }
                }
            };

            if (!showForThisScore)
                return;

            if (userStatisticsWatcher != null)
            {
                latestStatisticsUpdate.BindTo(userStatisticsWatcher.LatestUpdate);
                latestStatisticsUpdate.BindValueChanged(update =>
                {
                    if (update.NewValue?.Score.MatchesOnlineID(score) == true)
                        updateProfileDelta(update.NewValue);
                }, true);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!showForThisScore)
                return;

            Alpha = 0;
            this.MoveToX(20);

            this.FadeIn(250, Easing.OutQuint);
            this.MoveToX(0, 350, Easing.OutQuint);
        }

        private Drawable profileRow(string label, out OsuSpriteText valueText)
        {
            OsuSpriteText createdValueText;

            var row = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = label,
                        Font = OsuFont.Torus.With(size: 11, weight: FontWeight.Bold),
                        Colour = Color4.White.Opacity(0.55f),
                    },
                    createdValueText = new OsuSpriteText
                    {
                        Text = "—",
                        Font = OsuFont.Torus.With(size: 20, weight: FontWeight.Bold),
                        Colour = Color4.White,
                    }
                }
            };

            valueText = createdValueText;
            return row;
        }

        private void updateProfileDelta(ScoreBasedUserStatisticsUpdate update)
        {
            var before = update.Before;
            var after = update.After;

            statusText.Text = "updated";
            statusText.Colour = Color4Extensions.FromHex("#9cff6a");

            rankText.Text = formatGlobalRankChange(before.GlobalRank, after.GlobalRank);
            ppText.Text = formatPPChange(before.PP, after.PP);
            accuracyText.Text = formatAccuracyChange(before.Accuracy, after.Accuracy);

            rankText.Colour = colourForRank(before.GlobalRank, after.GlobalRank);
            ppText.Colour = colourForNumber(before.PP, after.PP);
            accuracyText.Colour = colourForNumber(before.Accuracy, after.Accuracy);
        }

        private string formatGlobalRankChange(int? before, int? after)
        {
            if (after == null)
                return "—";

            if (before == null)
                return $"#{after.Value:N0}";

            int change = before.Value - after.Value;

            if (change > 0)
                return $"#{after.Value:N0}  ▲ {change:N0}";

            if (change < 0)
                return $"#{after.Value:N0}  ▼ {Math.Abs(change):N0}";

            return $"#{after.Value:N0}";
        }

        private string formatPPChange(decimal? before, decimal? after)
        {
            if (after == null)
                return "—";

            int current = (int)Math.Round(after.Value, MidpointRounding.AwayFromZero);

            if (before == null)
                return $"{current:N0}pp";

            int previous = (int)Math.Round(before.Value, MidpointRounding.AwayFromZero);
            int change = current - previous;

            if (change > 0)
                return $"{current:N0}pp  +{change:N0}";

            if (change < 0)
                return $"{current:N0}pp  {change:N0}";

            return $"{current:N0}pp";
        }

        private string formatAccuracyChange(double before, double after)
        {
            double change = after - before;

            if (change > 0)
                return $"{after:0.00}%  +{change:0.00}%";

            if (change < 0)
                return $"{after:0.00}%  {change:0.00}%";

            return $"{after:0.00}%";
        }

        private Color4 colourForRank(int? before, int? after)
        {
            if (before == null || after == null)
                return Color4.White;

            int change = before.Value - after.Value;

            if (change > 0)
                return Color4Extensions.FromHex("#9cff6a");

            if (change < 0)
                return Color4Extensions.FromHex("#ff6666");

            return Color4.White;
        }

        private Color4 colourForNumber(decimal? before, decimal? after)
        {
            if (before == null || after == null)
                return Color4.White;

            if (after > before)
                return Color4Extensions.FromHex("#9cff6a");

            if (after < before)
                return Color4Extensions.FromHex("#ff6666");

            return Color4.White;
        }

        private Color4 colourForNumber(double before, double after)
        {
            if (after > before)
                return Color4Extensions.FromHex("#9cff6a");

            if (after < before)
                return Color4Extensions.FromHex("#ff6666");

            return Color4.White;
        }
    }
}