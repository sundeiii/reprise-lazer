// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>
    /// Reprise: jassper0's "Simplistic" tosu pp counter, in the game: the play's grade, then a dark box with its pp and its 100s, 50s and misses,
    /// a purple progress line under it, and badges for its full combo pp and slider breaks once it's broken combo.
    /// </summary>
    public partial class SimplisticPerformancePointsCounter : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        private static readonly Color4 accent = new Color4(88, 55, 236, 255);
        private static readonly Color4 accent2 = new Color4(48, 29, 131, 255);

        private const float box_width = 500;
        private const float box_height = 90;
        private const float corner = 10;

        private readonly LivePlayStats stats = new LivePlayStats();

        private GlowText rank = null!;
        private Container progressFill = null!;
        private CountingSpriteText pp = null!;
        private CountingSpriteText oks = null!;
        private CountingSpriteText mehs = null!;
        private CountingSpriteText misses = null!;
        private Container fullComboBadge = null!;
        private CountingSpriteText fullComboPp = null!;
        private Container sliderBreakBadge = null!;
        private CountingSpriteText sliderBreaks = null!;

        public SimplisticPerformancePointsCounter()
        {
            AutoSizeAxes = Axes.Both;
            Scale = new Vector2(0.45f);
        }

        [osu.Framework.Allocation.BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                stats,
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Size = new Vector2(100),
                            Child = rank = new GlowText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Font = TosuFonts.Arial(75),
                                Spacing = new Vector2(-2, 0),
                            },
                        },
                        new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(box_width, box_height),
                            Children = new Drawable[]
                            {
                                // The progress line: peeking out under the box, purple as far into the map as the play is.
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Y = 5,
                                    Masking = true,
                                    CornerRadius = corner,
                                    EdgeEffect = new EdgeEffectParameters
                                    {
                                        Type = EdgeEffectType.Shadow,
                                        Colour = Color4.Black.Opacity(0.6f),
                                        Radius = 20,
                                        Offset = new Vector2(0, 5),
                                    },
                                    Children = new Drawable[]
                                    {
                                        new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Gray },
                                        progressFill = new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Width = 0,
                                            Masking = true,
                                            CornerRadius = corner,
                                            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = ColourInfo.GradientHorizontal(accent, accent2) },
                                        },
                                    },
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Masking = true,
                                    CornerRadius = corner,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = ColourInfo.GradientHorizontal(Color4Extensions.FromHex("313131"), Color4Extensions.FromHex("161616")),
                                        },
                                        new Container
                                        {
                                            X = 20,
                                            Width = 200,
                                            RelativeSizeAxes = Axes.Y,
                                            Child = new FillFlowContainer
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                AutoSizeAxes = Axes.Both,
                                                Direction = FillDirection.Horizontal,
                                                Children = new Drawable[]
                                                {
                                                    pp = new CountingSpriteText(n => n.ToString("#,0").Replace(',', ' '))
                                                    {
                                                        Anchor = Anchor.BottomLeft,
                                                        Origin = Anchor.BottomLeft,
                                                        Font = TosuFonts.Arial(44),
                                                    },
                                                    new GlowText
                                                    {
                                                        Anchor = Anchor.BottomLeft,
                                                        Origin = Anchor.BottomLeft,
                                                        Text = "pp",
                                                        Font = TosuFonts.Arial(44, bold: true),
                                                        TextColour = accent,
                                                        GlowColour = accent2,
                                                    },
                                                },
                                            },
                                        },
                                        // Where the overlay's flexbox puts them: spaced evenly over 300px after the pp.
                                        hit(290, oks = count(), Color4Extensions.FromHex("adff2f")),
                                        hit(370, mehs = count(), Color4Extensions.FromHex("8a2be2")),
                                        hit(450, misses = count(), Color4.Red),
                                    },
                                },
                                fullComboBadge = badge(70, ColourInfo.GradientHorizontal(accent, accent2), accent,
                                    fullComboPp = new CountingSpriteText(n => $"{n:#,0}pp".Replace(',', ' '))),
                                sliderBreakBadge = badge(405, ColourInfo.GradientHorizontal(Color4.Gray, Color4Extensions.FromHex("313131")),
                                    new Color4(77, 77, 77, 255), sliderBreaks = new CountingSpriteText(n => $"{n}xSB")),
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            stats.Rank.BindValueChanged(r => showRank(r.NewValue), true);
            stats.SongProgress.BindValueChanged(p => progressFill.ResizeWidthTo((float)p.NewValue, 100), true);
            stats.PerformancePoints.BindValueChanged(v => pp.CountTo(v.NewValue), true);
            stats.Oks.BindValueChanged(v => oks.CountTo(v.NewValue), true);
            stats.Mehs.BindValueChanged(v => mehs.CountTo(v.NewValue), true);
            stats.Misses.BindValueChanged(v => misses.CountTo(v.NewValue), true);
            stats.FullComboPerformancePoints.BindValueChanged(v => fullComboPp.CountTo(v.NewValue), true);
            stats.SliderBreaks.BindValueChanged(v => sliderBreaks.CountTo(v.NewValue), true);

            // The badges pop in once the play's broken combo (a miss or a slider break), as the overlay's do.
            stats.Misses.BindValueChanged(_ => updateBadges());
            stats.SliderBreaks.BindValueChanged(_ => updateBadges(), true);
        }

        private void updateBadges()
        {
            bool broken = stats.Misses.Value > 0 || stats.SliderBreaks.Value > 0;
            fullComboBadge.ScaleTo(broken ? 1 : 0, 200, Easing.Out).FadeTo(broken ? 1 : 0, 300, Easing.Out);

            bool slidersBroken = stats.SliderBreaks.Value > 0;
            sliderBreakBadge.ScaleTo(slidersBroken ? 1 : 0, 200, Easing.Out).FadeTo(slidersBroken ? 1 : 0, 300, Easing.Out);
        }

        private void showRank(ScoreRank grade)
        {
            Color4 colour = grade switch
            {
                ScoreRank.X or ScoreRank.S => Color4Extensions.FromHex("d6c253"),
                ScoreRank.A => Color4Extensions.FromHex("7ed653"),
                ScoreRank.B => Color4Extensions.FromHex("53d4d6"),
                ScoreRank.C => Color4Extensions.FromHex("d6538e"),
                ScoreRank.D or ScoreRank.F => Color4Extensions.FromHex("f04848"),
                _ => Color4Extensions.FromHex("e0e0e0"),
            };

            rank.Text = grade switch
            {
                ScoreRank.X or ScoreRank.XH => "SS",
                ScoreRank.S or ScoreRank.SH => "S",
                ScoreRank.F => "D",
                _ => grade.ToString(),
            };
            rank.TextColour = colour;
            rank.GlowColour = colour;
        }

        private static CountingSpriteText count() => new CountingSpriteText
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = TosuFonts.Arial(36),
        };

        // A hit count over a little glowing pill in its colour.
        private static Drawable hit(float x, CountingSpriteText number, Color4 colour) => new FillFlowContainer
        {
            X = x,
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.Centre,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 2),
            Children = new Drawable[]
            {
                number,
                new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Size = new Vector2(10, 5),
                    Masking = true,
                    CornerRadius = 2.5f,
                    EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow, Colour = colour.Opacity(0.8f), Radius = 5 },
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                },
            },
        };

        // A small rounded badge over the box's top edge.
        private static Container badge(float x, ColourInfo background, Color4 shadow, CountingSpriteText text)
        {
            text.Anchor = Anchor.Centre;
            text.Origin = Anchor.Centre;
            text.Font = TosuFonts.Arial(20);

            return new Container
            {
                X = x,
                Y = -15 + 12.5f,
                Height = 25,
                AutoSizeAxes = Axes.X,
                Origin = Anchor.CentreLeft,
                Masking = true,
                CornerRadius = 10,
                Scale = Vector2.Zero,
                Alpha = 0,
                EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = shadow, Radius = 5, Offset = new Vector2(0, 2) },
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = background },
                    new Container
                    {
                        AutoSizeAxes = Axes.X,
                        RelativeSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = 10 },
                        Child = text,
                    },
                },
            };
        }
    }
}
