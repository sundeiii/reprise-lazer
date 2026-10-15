// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Modern
{
    /// <summary>
    /// The top of the redesigned results screen's advanced info and ranking: the beatmap's cover with its title and artist, and its
    /// difficulty's card (stars, name, mapper, then its numbers as the play had them).
    /// </summary>
    public partial class BeatmapStrip : CompositeDrawable
    {
        private readonly ScoreInfo score;
        private readonly WorkingBeatmap working;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public BeatmapStrip(ScoreInfo score, WorkingBeatmap working)
        {
            this.score = score;
            this.working = working;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = create(working);
        }

        private Drawable create(WorkingBeatmap working)
        {
            var beatmap = score.BeatmapInfo!;
            var metadata = beatmap.BeatmapSet?.Metadata ?? beatmap.Metadata;

            // Its difficulty with the play's mods (CS, AR, OD, HP as they were played).
            var difficulty = new BeatmapDifficulty(beatmap.Difficulty);
            foreach (var mod in score.Mods.OfType<IApplicableToDifficulty>())
                mod.ApplyToDifficulty(difficulty);

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 104,
                Masking = true,
                Children = new Drawable[]
                {
                    new BeatmapBackgroundSprite(working)
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.7f), Color4.Black.Opacity(0.1f)),
                    },
                    new FillFlowContainer
                    {
                        Position = new Vector2(22, 16),
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            new BeatmapSetOnlineStatusPill { Status = beatmap.Status, TextSize = 12 },
                            new OsuSpriteText
                            {
                                Text = new RomanisableString(metadata.TitleUnicode, metadata.Title),
                                Font = OsuFont.TorusAlternate.With(size: 24, weight: FontWeight.SemiBold),
                                Shadow = true,
                            },
                            new OsuSpriteText
                            {
                                Text = new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
                                Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                                Shadow = true,
                            },
                        }
                    },
                    // The difficulty's card: stars, name, mapper; then its numbers.
                    new Container
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-8, 6),
                        Size = new Vector2(730, 90),
                        Masking = true,
                        CornerRadius = 12,
                        BorderThickness = 2,
                        BorderColour = Color4Extensions.FromHex("ff66ab").Opacity(0.6f),
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.7f },
                            new FillFlowContainer
                            {
                                Position = new Vector2(18, 10),
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = new Drawable[]
                                {
                                    new StarRatingDisplay(new StarDifficulty(beatmap.StarRating, 0), StarRatingDisplaySize.Small)
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    },
                                    new TextFlowContainer(t => t.Colour = colours.ForStarDifficulty(beatmap.StarRating))
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                    }.With(t =>
                                    {
                                        t.AddText(beatmap.DifficultyName, s => s.Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold));
                                        t.AddText(" mapped by ", s => s.Font = OsuFont.Torus.With(size: 14));
                                        t.AddText(metadata.Author.Username, s => s.Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold));
                                    }),
                                }
                            },
                            new FillFlowContainer
                            {
                                Position = new Vector2(18, 44),
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(16, 0),
                                ChildrenEnumerable = beatmapNumbers(difficulty).Select(n => new BeatmapNumber(n.Name, n.Value)),
                            },
                        }
                    },
                }
            };
        }

        private IEnumerable<(LocalisableString Name, string Value)> beatmapNumbers(BeatmapDifficulty difficulty)
        {
            var beatmap = score.BeatmapInfo!;

            if (score.Ruleset.OnlineID == 0)
            {
                // (Counts not known for a beatmap that isn't stored: -1.)
                bool known = beatmap.TotalObjectCount >= 0 && beatmap.EndTimeObjectCount >= 0;
                yield return ("Circles", known ? (beatmap.TotalObjectCount - beatmap.EndTimeObjectCount).ToString("N0", CultureInfo.CurrentCulture) : "-");
                yield return ("Sliders & Spinners", known ? beatmap.EndTimeObjectCount.ToString("N0", CultureInfo.CurrentCulture) : "-");
            }

            yield return ("Circle Size", difficulty.CircleSize.ToString("0.#", CultureInfo.CurrentCulture));
            yield return ("Approach Rate", difficulty.ApproachRate.ToString("0.#", CultureInfo.CurrentCulture));
            yield return ("Accuracy", difficulty.OverallDifficulty.ToString("0.#", CultureInfo.CurrentCulture));
            yield return ("HP Drain", difficulty.DrainRate.ToString("0.#", CultureInfo.CurrentCulture));
        }

        /// <summary>A beatmap number in the difficulty's card: its name, the value, a small bar over them.</summary>
        private partial class BeatmapNumber : FillFlowContainer
        {
            public BeatmapNumber(LocalisableString name, string value)
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 2);
                Children = new Drawable[]
                {
                    new Box { Size = new Vector2(70, 2), Colour = Color4Extensions.FromHex("ff66ab").Opacity(0.7f) },
                    new OsuSpriteText { Text = name, Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold), Colour = ModernResultsScreen.SUB_CONTENT },
                    new OsuSpriteText { Text = value, Font = OsuFont.Torus.With(size: 14) },
                };
            }
        }
    }
}
