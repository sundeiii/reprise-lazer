// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Header.Components
{
    /// <summary>
    /// Reprise: one of the profile's three statistics cards (the website's profile-detail-stats-card). They're all made as tall as the
    /// tallest one's content, as the website's grid row does.
    /// </summary>
    public abstract partial class ProfileStatsCard : CompositeDrawable
    {
        /// <summary>The website's @border-radius-large.</summary>
        public const float CORNER_RADIUS = 10;

        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        /// <summary>How tall the card's content wants it.</summary>
        public abstract float ContentHeight { get; }

        protected ProfileStatsCard()
        {
            RelativeSizeAxes = Axes.X;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            User.BindValueChanged(u => UpdateDisplay(u.NewValue), true);
        }

        /// <summary>Shows the given player's (or nothing, for none).</summary>
        protected abstract void UpdateDisplay(UserProfileData? data);
    }

    /// <summary>
    /// Reprise: a card in the website's three parts (profile-detail-stats-card): the top (the title and the values, darkening a little
    /// towards its bottom), a 60px middle strip a shade darker, and the bottom (at least 80px, darker still, for a chart) filling the rest.
    /// </summary>
    public abstract partial class SectionedProfileCard : ProfileStatsCard
    {
        private const float padding = 10;
        private const float middle_height = 60;
        private const float bottom_min_height = 80;

        private readonly IconUsage icon;
        private readonly LocalisableString title;

        private Container top = null!;
        private Container middle = null!;
        private Container bottom = null!;

        /// <summary>The top's values (under the title).</summary>
        protected FillFlowContainer TopContent { get; private set; } = null!;

        /// <summary>The middle strip's content.</summary>
        protected Container MiddleContent { get; private set; } = null!;

        /// <summary>The bottom's content (a chart is inset 15 from the right, as the website's).</summary>
        protected Container BottomContent { get; private set; } = null!;

        /// <summary>Over the top right corner (the ranked card's tier badge).</summary>
        protected Container TopRightContent { get; private set; } = null!;

        public override float ContentHeight => top.DrawHeight + middle_height + bottom_min_height;

        protected SectionedProfileCard(IconUsage icon, LocalisableString title)
        {
            this.icon = icon;
            this.title = title;

            Masking = true;
            CornerRadius = CORNER_RADIUS;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background3,
                },
                top = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        // linear-gradient(to bottom, transparent 70%, rgba(0, 0, 0, 0.1))
                        new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.Both,
                            Height = 0.3f,
                            Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0), Color4.Black.Opacity(0.1f)),
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(10),
                            Padding = new MarginPadding(padding),
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(5),
                                    Children = new Drawable[]
                                    {
                                        new SpriteIcon
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Size = new Vector2(18),
                                            Icon = icon,
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = title,
                                            Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                                        },
                                    }
                                },
                                TopContent = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(5),
                                },
                            }
                        },
                        TopRightContent = new Container
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            AutoSizeAxes = Axes.Both,
                            Margin = new MarginPadding(padding),
                        },
                    }
                },
                middle = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = middle_height,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.2f),
                        },
                        MiddleContent = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(padding),
                        },
                    }
                },
                bottom = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Children = new Drawable[]
                    {
                        // rgba(0, 0, 0, 0.2) under linear-gradient(rgba(0, 0, 0, 0.2), rgba(0, 0, 0, 0.4))
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.36f), Color4.Black.Opacity(0.52f)),
                        },
                        BottomContent = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Vertical = padding },
                        },
                    }
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            middle.Y = top.DrawHeight;
            bottom.Y = top.DrawHeight + middle_height;
            bottom.Height = Math.Max(bottom_min_height, DrawHeight - bottom.Y);
        }

        /// <summary>The website's text in the middle of an empty chart ("No recent plays", or a bold "-").</summary>
        protected static OsuSpriteText EmptyChartText(LocalisableString text, bool bold = false) => new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = text,
            Font = OsuFont.Default.With(size: 12, weight: bold ? FontWeight.Bold : FontWeight.Regular),
        };
    }

    /// <summary>
    /// Reprise: the website's value-display: a label over a light value. <see cref="CardValueSize.Rank"/> is its "rank" (30),
    /// <see cref="CardValueSize.RankSmall"/> "rank rank-small" (20), <see cref="CardValueSize.Plain"/> "plain" (16).
    /// </summary>
    public partial class CardValueDisplay : CompositeDrawable
    {
        private readonly OsuSpriteText label;

        public ProfileValueDisplay.ContentText Value { get; }

        public LocalisableString Label
        {
            set => label.Text = value;
        }

        public CardValueDisplay(CardValueSize size)
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    label = new OsuSpriteText { Font = OsuFont.Default.With(size: 12) },
                    Value = new ProfileValueDisplay.ContentText
                    {
                        Font = OsuFont.Default.With(size: size switch
                        {
                            CardValueSize.Rank => 30,
                            CardValueSize.RankSmall => 20,
                            _ => 16,
                        }, weight: FontWeight.Light),
                        // margin-top: -0.1em
                        Margin = new MarginPadding { Top = size == CardValueSize.Rank ? -3 : 0 },
                    },
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            label.Colour = colourProvider.Content1;
            Value.Colour = colourProvider.Content2;
        }
    }

    public enum CardValueSize
    {
        Rank,
        RankSmall,
        Plain,
    }
}
