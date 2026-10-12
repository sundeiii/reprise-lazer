// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    public partial class StableResultsPanel : CompositeDrawable
    {
        private readonly ScoreInfo score;
        private readonly bool playApplauseOnLoad;

        private OsuSpriteText scoreText = null!;
        private OsuSpriteText ppText = null!;
        private OsuSpriteText mapRankText = null!;
        private OsuSpriteText countryMapRankText = null!;
        private long displayScore;
        private double scoreAnimationStartTime;
        private Sample? panelFocusSample;
        private Sample? topAppearSample;
        private Sample? scoreTickSample;
        private long lastScoreTickBucket = -1;
        private PoolableSkinnableSample? rankApplauseSound;
        private readonly IBindable<ScoreBasedUserStatisticsUpdate?> latestStatisticsUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        public StableResultsPanel(ScoreInfo score, bool playApplauseOnLoad)
        {
            this.score = score;
            this.playApplauseOnLoad = playApplauseOnLoad;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = new Vector2(1040, 560);
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, UserStatisticsWatcher? userStatisticsWatcher)
        {
            panelFocusSample = audio.Samples.Get(@"Results/score-panel-focus");
            topAppearSample = audio.Samples.Get(@"Results/score-panel-top-appear");
            scoreTickSample = audio.Samples.Get(@"Gameplay/hitnormal");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            displayScore = scoreManager.GetBindableTotalScore(score).Value;
            scoreAnimationStartTime = Time.Current;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 3,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.62f),
                    },

                    // top dark strip
                    new Box
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 95,
                        Colour = Color4.Black.Opacity(0.22f),
                    },
                    // centred top header text
                    new ClickableContainer
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Margin = new MarginPadding { Left = 38 },
                        Width = 760,
                        Height = 95,
                        Action = openBeatmap,
                        Child = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 2),
                            Children = new Drawable[]
                            {
                                new TruncatingSpriteText
                                {
                                    Text = beatmapTitleLine(),
                                    Font = OsuFont.Torus.With(size: 24, weight: FontWeight.Bold),
                                    Colour = Color4.White,
                                    Width = 760,
                                },
                                new TruncatingSpriteText
                                {
                                    Text = beatmapDifficultyLine(),
                                    Font = OsuFont.Torus.With(size: 15, weight: FontWeight.SemiBold),
                                    Colour = Color4.White.Opacity(0.68f),
                                    Width = 760,
                                },
                            }
                        }
                    },

                    // bottom user banner strip
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 110,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new UserCoverBackground
                            {
                                RelativeSizeAxes = Axes.Both,
                                User = score.User,
                                Colour = Color4.White.Opacity(0.45f),
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black.Opacity(0.68f),
                            },
                        }
                    },

                    // left main content
                    new FillFlowContainer
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Margin = new MarginPadding { Left = 38, Top = 118 },
                        AutoSizeAxes = Axes.Y,
                        Width = 650,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 15),
                        Children = new Drawable[]
                        {
                            scoreText = new OsuSpriteText
                            {
                                Text = "0",
                                Font = OsuFont.Numeric.With(size: 58),
                                Colour = Color4.White,
                            },

                            new ModDisplay
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                ExpansionMode = ExpansionMode.AlwaysExpanded,
                                Scale = new Vector2(0.55f),
                                Current = { Value = score.Mods }
                            },

                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(42, 0),
                                Children = new Drawable[]
                                {
                                    stat("GREAT", count(HitResult.Great).ToString("N0")),
                                    stat("OK", count(HitResult.Ok).ToString("N0")),
                                    stat("MEH", count(HitResult.Meh).ToString("N0")),
                                    stat("MISS", count(HitResult.Miss).ToString("N0"), Color4Extensions.FromHex("#ff6666"), count(HitResult.Miss) > 0 ? FontWeight.Bold : FontWeight.Regular),
                                }
                            },

                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(58, 0),
                                Children = new Drawable[]
                                {
                                    stat(
                                        "MAX COMBO",
                                        $"{score.MaxCombo:N0}x",
                                        isMaxCombo() ? Color4Extensions.FromHex("#9cff6a") : null,
                                        isMaxCombo() ? FontWeight.Bold : FontWeight.Regular
                                    ),

                                    stat(BeatmapsetsStrings.ShowScoreboardHeadersAccuracy.ToString(), $"{score.Accuracy * 100:0.00}%"),

                                    ppStat()
                                }
                            },

                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(44, 0),
                                Children = new Drawable[]
                                {
                                    mapRankStat(),
                                    countryMapRankStat(),
                                }
                            },
                        }
                    },

                    // huge rank
                    new Container
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Margin = new MarginPadding { Top = 180, Right = 82 },
                        Size = new Vector2(250, 170),
                        Child = createRankDrawable(),
                    },

                    // bottom-left footer user
                    new FillFlowContainer
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Margin = new MarginPadding { Left = 38, Bottom = 28 },
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(12, 0),
                        Children = new Drawable[]
                        {
                            new UpdateableAvatar
                            {
                                User = score.User,
                                Size = new Vector2(46),
                                Masking = true,
                                CornerRadius = 8,
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 1),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = "played by",
                                        Font = OsuFont.Torus.With(size: 12, weight: FontWeight.SemiBold),
                                        Colour = Color4.White.Opacity(0.65f),
                                    },
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(7, 0),
                                        Children = new Drawable[]
                                        {
                                            new DrawableFlag(score.User.CountryCode)
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Size = new Vector2(26, 18),
                                            },
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Text = score.User.Username,
                                                Font = OsuFont.Torus.With(size: 19, weight: FontWeight.Bold),
                                                Colour = Color4.White,
                                            },
                                        }
                                    },
                                }
                            }
                        }
                    },

                    // bottom-right footer date
                    new OsuSpriteText
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        Margin = new MarginPadding { Right = 38, Bottom = 38 },
                        Text = score.Date == default ? "" : score.Date.LocalDateTime.ToString("g"),
                        Font = OsuFont.Torus.With(size: 15),
                        Colour = Color4.White.Opacity(0.72f),
                    },
                }
            };

            calculatePerformance();

            Alpha = 0;
            Scale = new Vector2(0.985f);

            this.FadeIn(220, Easing.OutQuint);
            this.ScaleTo(1, 320, Easing.OutQuint);

            topAppearSample?.Play();
            Scheduler.AddDelayed(() => panelFocusSample?.Play(), 90);
            if (playApplauseOnLoad)
                Scheduler.AddDelayed(() => playApplause(score.Rank), 250);
        }

        protected override void Update()
        {
            base.Update();

            if (scoreText == null)
                return;

            const double duration = 900;

            double progress = Math.Clamp((Time.Current - scoreAnimationStartTime) / duration, 0, 1);
            double eased = 1 - Math.Pow(1 - progress, 5);

            long current = (long)Math.Round(displayScore * eased);
            scoreText.Text = current.ToString("N0");

            long bucket = current / Math.Max(1, displayScore / 18);

            if (bucket != lastScoreTickBucket && progress < 1)
            {
                lastScoreTickBucket = bucket;
                scoreTickSample?.Play();
            }
        }

        private string beatmapTitleLine()
        {
            var parts = beatmapTitleParts();

            if (!string.IsNullOrEmpty(parts.artist))
                return $"{parts.title} - {parts.artist}";

            return parts.title;
        }

        private string beatmapDifficultyLine()
        {
            var parts = beatmapTitleParts();

            if (!string.IsNullOrEmpty(parts.difficulty) && !string.IsNullOrEmpty(parts.mapper))
                return $"{parts.difficulty} mapped by {parts.mapper}";

            if (!string.IsNullOrEmpty(parts.difficulty))
                return parts.difficulty;

            if (!string.IsNullOrEmpty(parts.mapper))
                return $"mapped by {parts.mapper}";

            return "";
        }

        private (string title, string artist, string difficulty, string mapper) beatmapTitleParts()
        {
            string text = score.GetDisplayTitle();

            const string playingMarker = " playing ";
            int playingIndex = text.IndexOf(playingMarker, StringComparison.Ordinal);

            if (playingIndex >= 0)
                text = text[(playingIndex + playingMarker.Length)..];

            string difficulty = "";
            int diffStart = text.LastIndexOf('[');
            int diffEnd = text.LastIndexOf(']');

            if (diffStart >= 0 && diffEnd > diffStart)
            {
                difficulty = text.Substring(diffStart + 1, diffEnd - diffStart - 1).Trim();
                text = text[..diffStart].Trim();
            }

            string mapper = "";
            int mapperStart = text.LastIndexOf('(');
            int mapperEnd = text.LastIndexOf(')');

            if (mapperStart >= 0 && mapperEnd > mapperStart)
            {
                mapper = text.Substring(mapperStart + 1, mapperEnd - mapperStart - 1).Trim();
                text = text[..mapperStart].Trim();
            }

            string artist = "";
            string title = text;

            int separator = text.IndexOf(" - ", StringComparison.Ordinal);

            if (separator >= 0)
            {
                artist = text[..separator].Trim();
                title = text[(separator + 3)..].Trim();
            }

            return (title, artist, difficulty, mapper);
        }

        private void openBeatmap()
        {
            if (score.BeatmapInfo == null)
                return;

            if (score.BeatmapInfo.OnlineID > 0)
            {
                game.ShowBeatmap(score.BeatmapInfo.OnlineID);
                return;
            }

            if (score.BeatmapInfo.BeatmapSet?.OnlineID > 0)
                game.ShowBeatmapSet(score.BeatmapInfo.BeatmapSet.OnlineID);
        }

        private void playApplause(ScoreRank rank)
        {
            const double applauseVolume = 0.8f;

            rankApplauseSound?.Dispose();

            var applauseSamples = new List<string>();

            if (rank >= ScoreRank.B)
                applauseSamples.Insert(0, @"applause");

            switch (rank)
            {
                default:
                case ScoreRank.D:
                    applauseSamples.Add(@"Results/applause-d");
                    break;

                case ScoreRank.C:
                    applauseSamples.Add(@"Results/applause-c");
                    break;

                case ScoreRank.B:
                    applauseSamples.Add(@"Results/applause-b");
                    break;

                case ScoreRank.A:
                    applauseSamples.Add(@"Results/applause-a");
                    break;

                case ScoreRank.S:
                case ScoreRank.SH:
                case ScoreRank.X:
                case ScoreRank.XH:
                    applauseSamples.Add(@"Results/applause-s");
                    break;
            }

            LoadComponentAsync(rankApplauseSound = new PoolableSkinnableSample(new SampleInfo(applauseSamples.ToArray())), s =>
            {
                if (s != rankApplauseSound)
                    return;

                AddInternal(rankApplauseSound);

                rankApplauseSound.VolumeTo(applauseVolume);
                rankApplauseSound.Play();
            });
        }

        public void UpdateMapRanks(int? globalRank, int? countryRank)
        {
            if (mapRankText != null)
                mapRankText.Text = globalRank.HasValue && globalRank.Value > 0
                    ? $"#{globalRank.Value:N0}"
                    : "—";

            if (countryMapRankText != null)
                countryMapRankText.Text = countryRank.HasValue && countryRank.Value > 0
                    ? $"#{countryRank.Value:N0}"
                    : "—";
        }

        private string getMapRankText()
        {
            // If your ScoreInfo has Position, this will show global map placement.
            // Country rank needs a country leaderboard fetch later.
            try
            {
                dynamic dynamicScore = score;

                int? position = dynamicScore.Position;

                if (position != null && position > 0)
                    return $"Global #{position:N0}";
            }
            catch
            {
            }

            return "Global —";
        }

        private Drawable mapRankStat() => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = "MAP RANK",
                    Font = OsuFont.Torus.With(size: 12, weight: FontWeight.Bold),
                    Colour = Color4Extensions.FromHex("#ffd966"),
                },
                mapRankText = new OsuSpriteText
                {
                    Text = "—",
                    Font = OsuFont.Torus.With(size: 21, weight: FontWeight.SemiBold),
                    Colour = Color4.White,
                }
            }
        };

        private Drawable countryMapRankStat() => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = "COUNTRY MAP RANK",
                    Font = OsuFont.Torus.With(size: 12, weight: FontWeight.Bold),
                    Colour = Color4Extensions.FromHex("#ffd966"),
                },
                countryMapRankText = new OsuSpriteText
                {
                    Text = "—",
                    Font = OsuFont.Torus.With(size: 21, weight: FontWeight.SemiBold),
                    Colour = Color4.White,
                }
            }
        };

        private Drawable ppStat() => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = BeatmapsetsStrings.ShowScoreboardHeaderspp,
                    Font = OsuFont.Torus.With(size: 12, weight: FontWeight.Bold),
                    Colour = Color4Extensions.FromHex("#ffd966"),
                },
                ppText = new OsuSpriteText
                {
                    Text = "...",
                    Font = OsuFont.Torus.With(size: 24),
                    Colour = Color4.White,
                }
            }
        };

        private void calculatePerformance()
        {
            if (score.PP.HasValue)
            {
                ppText.Text = $"{Math.Round(score.PP.Value, MidpointRounding.AwayFromZero):N0}pp";
                return;
            }

            Task.Run(async () =>
            {
                var attributes = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo!, score.Ruleset, score.Mods, CancellationToken.None).ConfigureAwait(false);
                var performanceCalculator = score.Ruleset.CreateInstance().CreatePerformanceCalculator();

                if (attributes?.DifficultyAttributes == null || performanceCalculator == null)
                    return;

                var result = await performanceCalculator.CalculateAsync(score, attributes.Value.DifficultyAttributes, CancellationToken.None).ConfigureAwait(false);
                int pp = (int)Math.Round(result.Total, MidpointRounding.AwayFromZero);

                Schedule(() => ppText.Text = $"{pp:N0}pp");
            });
        }

        private bool isMaxCombo()
        {
            int? maxCombo = score.GetMaximumAchievableCombo();
            return maxCombo.HasValue && score.MaxCombo >= maxCombo.Value;
        }

        private int count(HitResult result)
            => score.Statistics.TryGetValue(result, out int value) ? value : 0;

        private Drawable label(string text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.Torus.With(size: 24, weight: FontWeight.SemiBold),
            Colour = Color4.White,
        };


        private Drawable createRankDrawable()
        {
            Texture? texture = skin.GetTexture(stableRankTextureName());

            if (texture != null)
            {
                return new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = texture,
                    Height = 155,
                    Width = texture.Width * (155f / texture.Height),
                    FillMode = FillMode.Fit,
                };
            }

            return new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = stableRankText(),
                Font = OsuFont.Torus.With(size: 155, weight: FontWeight.Bold),
                Colour = Color4Extensions.FromHex("#7ffcff"),
            };
        }

        private string stableRankText()
        {
            return score.Rank switch
            {
                ScoreRank.XH => "SSH",
                ScoreRank.X => "SS",
                ScoreRank.SH => "SH",
                ScoreRank.S => "S",
                ScoreRank.A => "A",
                ScoreRank.B => "B",
                ScoreRank.C => "C",
                ScoreRank.D => "D",
                _ => score.Rank.ToString().ToUpperInvariant()
            };
        }

        private string stableRankTextureName()
        {
            return score.Rank switch
            {
                ScoreRank.XH => "ranking-XH-small",
                ScoreRank.X => "ranking-X-small",
                ScoreRank.SH => "ranking-SH-small",
                ScoreRank.S => "ranking-S-small",
                ScoreRank.A => "ranking-A-small",
                ScoreRank.B => "ranking-B-small",
                ScoreRank.C => "ranking-C-small",
                ScoreRank.D => "ranking-D-small",
                _ => $"ranking-{score.Rank}-small"
            };
        }

        private Drawable stat(string name, string value, Color4? valueColour = null, FontWeight valueWeight = FontWeight.Regular) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = name,
                    Font = OsuFont.Torus.With(size: 12, weight: FontWeight.Bold),
                    Colour = Color4Extensions.FromHex("#ffd966"),
                },
                new OsuSpriteText
                {
                    Text = value,
                    Font = OsuFont.Torus.With(size: 24, weight: valueWeight),
                    Colour = valueColour ?? Color4.White,
                }
            }
        };

        protected override void Dispose(bool isDisposing)
        {
            rankApplauseSound?.Stop();
            rankApplauseSound?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}


