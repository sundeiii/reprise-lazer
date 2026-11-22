// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>
    /// Reprise: cyperdark's "DarkAndWhite" tosu pp counter, in the game: a black card with a thin white progress line, the play's pp, and a
    /// white pill of its 100s, 50s and misses.
    /// </summary>
    public partial class DarkAndWhitePerformancePointsCounter : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        private readonly LivePlayStats stats = new LivePlayStats();

        private Box progressFill = null!;
        private CountingSpriteText pp = null!;
        private OsuSpriteText oks = null!;
        private OsuSpriteText mehs = null!;
        private OsuSpriteText misses = null!;

        public DarkAndWhitePerformancePointsCounter()
        {
            AutoSizeAxes = Axes.Both;
            Scale = new Vector2(0.4f);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                stats,
                new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 6,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Color4.Black.Opacity(0.5f),
                        Radius = 15,
                        Offset = new Vector2(0, 3),
                    },
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                        // The progress line along the top.
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 4,
                            Y = 5,
                            Padding = new MarginPadding { Horizontal = 15 },
                            Child = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                CornerRadius = 2,
                                Children = new Drawable[]
                                {
                                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.3f) },
                                    progressFill = new Box { RelativeSizeAxes = Axes.Both, Width = 0, Colour = Color4.White },
                                },
                            },
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Padding = new MarginPadding { Vertical = 20, Horizontal = 40 },
                            Spacing = new Vector2(40, 0),
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Width = 180,
                                    AutoSizeAxes = Axes.Y,
                                    Child = pp = new CountingSpriteText(n => $"{n:#,0}PP".Replace(',', ' '))
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        Font = TosuFonts.RobotoMedium(50),
                                    },
                                },
                                // The white pill, its shadow a solid grey edge under it.
                                new Container
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Width = 480,
                                    AutoSizeAxes = Axes.Y,
                                    Children = new Drawable[]
                                    {
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Y = 6,
                                            Masking = true,
                                            CornerRadius = 10,
                                            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("cccccc") },
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Masking = true,
                                            CornerRadius = 10,
                                            Children = new Drawable[]
                                            {
                                                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White },
                                                new FillFlowContainer
                                                {
                                                    Anchor = Anchor.TopCentre,
                                                    Origin = Anchor.TopCentre,
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Children = new[]
                                                    {
                                                        hit(oks = count(), "x100"),
                                                        hit(mehs = count(), "x50"),
                                                        hit(misses = count(), "x0"),
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            stats.MapProgress.BindValueChanged(p => progressFill.ResizeWidthTo((float)p.NewValue, 500, Easing.InOutSine), true);

            // Nothing shown before the first note (as the overlay does), then the pp counting up and the hits straight away.
            stats.Started.BindValueChanged(_ => update(), true);
            stats.PerformancePoints.BindValueChanged(_ => update());
            stats.Oks.BindValueChanged(_ => update());
            stats.Mehs.BindValueChanged(_ => update());
            stats.Misses.BindValueChanged(_ => update());
        }

        private void update()
        {
            bool started = stats.Started.Value;

            pp.CountTo(started ? stats.PerformancePoints.Value : 0);
            oks.Text = (started ? stats.Oks.Value : 0).ToString();
            mehs.Text = (started ? stats.Mehs.Value : 0).ToString();
            misses.Text = (started ? stats.Misses.Value : 0).ToString();
        }

        private static OsuSpriteText count() => new OsuSpriteText
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            Text = "0",
            Colour = Color4.Black,
            Font = TosuFonts.RobotoMedium(45),
        };

        // A hit count with its label after it, smaller and faded.
        private static Drawable hit(OsuSpriteText number, string label) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Padding = new MarginPadding { Horizontal = 15 },
            Children = new Drawable[]
            {
                number,
                new OsuSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Text = label,
                    Colour = Color4.Black,
                    Alpha = 0.5f,
                    Margin = new MarginPadding { Bottom = 4 },
                    Font = TosuFonts.RobotoMedium(30),
                },
            },
        };
    }
}
