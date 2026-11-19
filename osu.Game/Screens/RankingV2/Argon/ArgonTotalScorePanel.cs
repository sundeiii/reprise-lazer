// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Skinning;
using osuTK;
using Realms;

namespace osu.Game.Screens.RankingV2.Argon
{
    public partial class ArgonTotalScorePanel : CompositeDrawable, ISerialisableDrawable, IAnimatableSkinnable
    {
        public static readonly ColourInfo TEXT_GRADIENT = ColourInfo.GradientVertical(Colour4.White, Colour4.FromHex(@"B2E5FE"));

        public static readonly ColourInfo PERFECT_GRADIENT = new ColourInfo
        {
            TopLeft = Colour4.FromHex(@"00FFAA"),
            TopRight = Colour4.FromHex(@"7CF6FF"),
            BottomLeft = Colour4.FromHex(@"7CF6FF"),
            BottomRight = Colour4.FromHex(@"FF9AD7"),
        };

        private Container scoreContainer = null!;
        private Drawable perfectIndicator = null!;
        private Container personalBestIndicator = null!;
        private Box personalBestFlash = null!;
        private OsuSpriteText personalBestMarginText = null!;
        private TotalScoreCounter totalScoreText = null!;

        [Resolved]
        private IBindable<IScoreInfo> score { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private Bindable<ScoringMode> scoringMode { get; } = new Bindable<ScoringMode>();

        /// <summary>
        /// Reprise: whether the score is the player's best on the beatmap, when the screen showing it knows (the server's, which can come
        /// in after the reveal has started; null while it doesn't know yet). Without it, the best of the scores stored locally.
        /// </summary>
        public IBindable<bool?>? PersonalBest { get; init; }

        /// <summary>
        /// Reprise: the player's best on the beatmap before this score (the server's), once known: a personal best says by how much it beat it.
        /// </summary>
        public IBindable<SoloScoreInfo?>? PreviousBest { get; init; }

        // When the reveal brings in the personal best indicator (Reprise: it's shown then, or once it's known if that's later).
        private double? personalBestRevealTime;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, TextureStore textures, OsuConfigManager configManager)
        {
            Width = ArgonBeatmapInfoPanel.WIDTH * 1.3f;
            Height = 115;

            InternalChildren =
            [
                scoreContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Shear = OsuGame.SHEAR,
                    CornerRadius = ShearedButton.CORNER_RADIUS,
                    Masking = true,
                    EdgeEffect = ArgonBeatmapInfoPanel.CreateShadowEdgeEffect(),
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background5.Opacity(0.98f),
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Padding = new MarginPadding
                            {
                                Vertical = 15,
                                Right = 30,
                            },
                            Spacing = new Vector2(20),
                            Shear = -OsuGame.SHEAR,
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Children =
                            [
                                new Container
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Size = new Vector2(65),
                                    Child = perfectIndicator = new PerfectIndicator()
                                },
                                totalScoreText = new TotalScoreCounter
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                },
                            ],
                        },
                    ],
                },
                personalBestIndicator = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Origin = Anchor.Centre,
                    RelativeAnchorPosition = new Vector2(0.95f, 0),
                    Shear = OsuGame.SHEAR,
                    CornerRadius = ShearedButton.CORNER_RADIUS,
                    Masking = true,
                    EdgeEffect = ArgonBeatmapInfoPanel.CreateShadowEdgeEffect(),
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(Colour4.FromHex(@"#FFE7A8"), Colour4.FromHex(@"#FFB800")),
                        },
                        // Reprise: and by how much it beat the last best, once that's known.
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Shear = -OsuGame.SHEAR,
                            Margin = new MarginPadding
                            {
                                Horizontal = 12,
                                Vertical = 6,
                            },
                            Children =
                            [
                                new OsuSpriteText
                                {
                                    Colour = colourProvider.Background5,
                                    Text = ResultsScreenStrings.PersonalBest.ToUpper(),
                                    UseFullGlyphHeight = false,
                                    Spacing = new Vector2(1.5f),
                                    Font = OsuFont.Style.Body.With(weight: FontWeight.Bold),
                                },
                                personalBestMarginText = new OsuSpriteText
                                {
                                    Colour = colourProvider.Background5,
                                    UseFullGlyphHeight = false,
                                    Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                },
                            ],
                        },
                        personalBestFlash = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.White,
                            Alpha = 0,
                            Blending = BlendingParameters.Additive,
                        },
                    ],
                },
            ];

            configManager.BindWith(OsuSetting.ScoreDisplayMode, scoringMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            score.BindValueChanged(_ => updateState());
            scoringMode.BindValueChanged(_ => updateState(), true);

            PersonalBest?.BindValueChanged(_ => updatePersonalBest());
            PreviousBest?.BindValueChanged(_ => updatePersonalBestMargin());
        }

        // Reprise: "+84,299" on the badge (in the score display's mode, classic or standardised) for a play that beat a best set before.
        private void updatePersonalBestMargin()
        {
            var previous = PreviousBest?.Value;
            long margin = previous == null ? 0 : score.Value.GetDisplayScore(scoringMode.Value) - previous.GetDisplayScore(scoringMode.Value);

            personalBestMarginText.Text = $"+{margin.ToLocalisableString(@"N0")}";
            personalBestMarginText.Alpha = margin > 0 ? 1 : 0;
        }

        // Reprise: the server's answer came in. Before the reveal gets to the indicator, it brings it in as usual; after, it comes in now.
        private void updatePersonalBest()
        {
            if (personalBestRevealTime == null)
            {
                personalBestIndicator.Alpha = isScorePersonalBest(score.Value) ? 1 : 0;
                return;
            }

            personalBestIndicator.ClearTransforms();
            personalBestFlash.ClearTransforms();
            animatePersonalBest(Math.Max(personalBestRevealTime.Value, Time.Current));
        }

        private void animatePersonalBest(double revealTime)
        {
            if (isScorePersonalBest(score.Value))
            {
                personalBestIndicator.FadeOut()
                                     .ScaleTo(new Vector2(1.6f));

                using (BeginAbsoluteSequence(revealTime))
                {
                    personalBestIndicator.FadeIn(400, Easing.OutQuint)
                                         .ScaleTo(Vector2.One, 400, Easing.In);
                    personalBestFlash
                        .Delay(400)
                        .FadeOutFromOne(2600, Easing.OutQuint);
                }
            }
            else
            {
                personalBestIndicator.FadeOut()
                                     .ScaleTo(Vector2.One);
            }
        }

        private void updateState()
        {
            // keep the size of the total score text manageable when classic scoring mode is enabled
            // 7 digits is the assumed maximum for standardised - scale any longer score totals proportionally
            long displayScore = score.Value.GetDisplayScore(scoringMode.Value);
            int scoreDigits = (int)Math.Ceiling(Math.Log10(displayScore));
            totalScoreText.SetCountWithoutRolling(displayScore);
            totalScoreText.Scale = new Vector2(7f / Math.Max(7, scoreDigits));

            perfectIndicator.Alpha = score.Value.IsPerfect() ? 1 : 0;
            personalBestIndicator.Alpha = isScorePersonalBest(score.Value) ? 1 : 0;
            updatePersonalBestMargin();
        }

        private bool isScorePersonalBest(IScoreInfo newScore)
        {
            if (PersonalBest != null)
                return PersonalBest.Value == true;

            if (newScore.Beatmap == null)
                return false;

            if (api.LocalUser.Value.OnlineID > 0 && !newScore.User.MatchesOnlineID(api.LocalUser.Value))
                return false;

            var personalBest = realm.Run(r =>
                r.GetAllLocalScoresForUser(api.LocalUser.Value.Id)
                 .Filter($@"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.OnlineID)} == $0", newScore.Beatmap.OnlineID)
                 .AsEnumerable()
                 .OrderByDescending(score => score.TotalScore)
                 .ThenBy(score => score.Date)
                 .FirstOrDefault());

            return newScore.MatchesOnlineID(personalBest) || (newScore is ScoreInfo localScore && localScore.Equals(personalBest));
        }

        #region ISerialisableDrawable

        public bool UsesFixedAnchor { get; set; }

        #endregion

        #region IAnimatableSkinnable

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.AnimationSequence))]
        public BindableInt GroupNumber { get; } = new BindableInt(2)
        {
            MinValue = 0,
            MaxValue = 10,
        };

        int IAnimatableSkinnable.GroupNumber => GroupNumber.Value;

        public double StartAnimating(double startTime)
        {
            const double transition_duration = 500;

            FinishAnimating();

            scoreContainer.FadeOut()
                          .MoveToOffset(new Vector2(-50, 0));

            double latestTransformEndTime = startTime;

            using (BeginAbsoluteSequence(latestTransformEndTime))
            {
                scoreContainer.FadeIn(transition_duration, Easing.OutQuint)
                              .MoveToOffset(new Vector2(50, 0), transition_duration, Easing.OutQuint);
                totalScoreText.ResetCount();

                using (BeginDelayedSequence(ArgonScoreRankDisplay.SWOOSH_PRE_DELAY))
                {
                    totalScoreText.Current.Value = score.Value.GetDisplayScore(scoringMode.Value);
                    latestTransformEndTime = totalScoreText.LatestTransformEndTime;
                }
            }

            if (score.Value.MaxCombo == score.Value.GetMaximumAchievableCombo())
            {
                perfectIndicator.FadeOut()
                                .ScaleTo(new Vector2(0.7f));

                using (BeginAbsoluteSequence(latestTransformEndTime - 650))
                {
                    perfectIndicator.FadeIn(150, Easing.OutQuint)
                                    .ScaleTo(Vector2.One, 750, Easing.OutElasticHalf);

                    latestTransformEndTime = perfectIndicator.LatestTransformEndTime;

                    perfectIndicator.FlashColour(Colour4.White, 1000, Easing.OutSine);
                }
            }
            else
            {
                perfectIndicator.FadeOut()
                                .RotateTo(0)
                                .ScaleTo(Vector2.One);
            }

            personalBestRevealTime = latestTransformEndTime - 250;
            animatePersonalBest(personalBestRevealTime.Value);

            if (isScorePersonalBest(score.Value))
                latestTransformEndTime = personalBestIndicator.LatestTransformEndTime;

            return latestTransformEndTime;
        }

        public void FinishAnimating()
        {
            scoreContainer.FinishTransforms();
            perfectIndicator.FinishTransforms();
            personalBestIndicator.FinishTransforms();
            personalBestFlash.FinishTransforms();
            totalScoreText.StopRolling();
        }

        #endregion

        private partial class PerfectIndicator : CompositeDrawable
        {
            private Sprite star = null!;
            private Sprite sparkle = null!;
            private Sprite textAdditive = null!;

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;

                Size = new Vector2(75);

                Colour = PERFECT_GRADIENT;

                InternalChildren = new Drawable[]
                {
                    star = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = textures.Get(@"Icons/Ranking/perfect-star"),
                    },
                    sparkle = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = textures.Get(@"Icons/Ranking/perfect-sparkle"),
                    },
                    new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = textures.Get(@"Icons/Ranking/perfect-text"),
                    },
                    textAdditive = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Blending = BlendingParameters.Additive,
                        Texture = textures.Get(@"Icons/Ranking/perfect-text"),
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                sparkle.Spin(36000, RotationDirection.Clockwise);

                star.RotateTo(4).Then()
                    .RotateTo(-4, 2000, Easing.InOutSine).Then()
                    .RotateTo(4, 2000, Easing.InOutSine).Loop();

                textAdditive.FadeOut()
                            .FadeIn(150, Easing.OutQuint)
                            .Then()
                            .FadeOut(3000, Easing.OutQuint)
                            .Then()
                            .Delay(3000)
                            .Loop();
            }
        }

        public partial class TotalScoreCounter : RollingCounter<long>
        {
            public const double ROLLING_DURATION = 3000;
            public const Easing ROLLING_EASING = Easing.OutPow10;

            protected override double RollingDuration => ROLLING_DURATION;

            protected override Easing RollingEasing => ROLLING_EASING;

            protected override IHasText CreateText()
            {
                return new OsuSpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Font = OsuFont.TorusAlternate.With(size: 120, weight: FontWeight.Light, fixedWidth: true),
                    Spacing = new Vector2(-5),
                    Colour = TEXT_GRADIENT,
                    Margin = new MarginPadding { Bottom = 15, },
                };
            }

            protected override LocalisableString FormatCount(long count) => count.ToString(@"N0");
        }
    }
}
