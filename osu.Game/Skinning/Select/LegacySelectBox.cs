// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Select
{
    /// <summary>
    /// Stable's choice box: the current choice in a white-bordered black box; clicking lists the choices below it.
    /// </summary>
    public partial class LegacySelectBox<T> : CompositeDrawable
        where T : struct
    {
        private readonly Bindable<T> current;
        private readonly (string Text, T Value)[] choices;

        private OsuSpriteText currentText = null!;
        private FillFlowContainer list = null!;

        /// <summary>
        /// The colour of the current choice's text.
        /// </summary>
        public Color4 TextColour { get; init; } = Color4.White;

        /// <summary>
        /// Stable 2015's look: rounded, a thin light border, Aller, a ⌄ at the right; its choices with a › each.
        /// </summary>
        public bool Stable2015 { get; init; }

        /// <summary>
        /// Stable 2015's border colour (its ranking choice's is blue).
        /// </summary>
        public Color4 BorderColour2015 { get; init; } = new Color4(255, 255, 255, 140);

        /// <summary>
        /// Stable 2015's text size (13; its ranking choice's 14).
        /// </summary>
        public float TextSize2015 { get; init; } = 13;

        public LegacySelectBox(Bindable<T> current, (string Text, T Value)[] choices, float width = 192)
        {
            this.current = current;
            this.choices = choices;

            Size = new Vector2(width, 22);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            list = new FillFlowContainer
            {
                Y = 24,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Alpha = 0,
            };

            foreach (var (text, value) in choices)
            {
                list.Add(new ChoiceItem(text, () =>
                {
                    current.Value = value;
                    list.Hide();
                }, Stable2015));
            }

            if (Stable2015)
            {
                // Stable 2015's: 15 high (its field), corners of 5, size 13 with a chevron 8 in from the right.
                Height = 15 * 1.6f;
                list.Y = Height + 1;

                InternalChildren = new Drawable[]
                {
                    new ClickableContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 5 * 1.6f,
                        BorderColour = BorderColour2015,
                        BorderThickness = 1.6f,
                        Action = () => list.FadeTo(list.Alpha > 0 ? 0 : 1, 100),
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0, 0, 0, 128) },
                            currentText = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 5,
                                Font = StableFont.Get(TextSize2015),
                                Colour = TextColour,
                            },
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.Centre,
                                X = -8 * 1.6f,
                                Size = new Vector2(13),
                                Icon = FontAwesome.Solid.ChevronDown,
                            },
                        }
                    },
                    list,
                };

                return;
            }

            InternalChildren = new Drawable[]
            {
                // Stable's: black with a white border, the choice in white.
                new ClickableContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderColour = Color4.White,
                    BorderThickness = 2,
                    Action = () => list.FadeTo(list.Alpha > 0 ? 0 : 1, 100),
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                        currentText = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 3,
                            Font = OsuFont.GetFont(size: 19),
                            Colour = TextColour,
                        },
                    }
                },
                list,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            current.BindValueChanged(c => currentText.Text = choices.FirstOrDefault(x => EqualityComparer<T>.Default.Equals(x.Value, c.NewValue)).Text ?? c.NewValue.ToString()!, true);
        }

        // The open list goes past this box.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => base.ReceivePositionalInputAt(screenSpacePos) || (list.IsPresent && list.ReceivePositionalInputAt(screenSpacePos));

        private partial class ChoiceItem : ClickableContainer
        {
            private readonly Box hover;

            public ChoiceItem(string text, Action action, bool stable2015 = false)
            {
                Action = action;
                RelativeSizeAxes = Axes.X;
                Height = stable2015 ? 17 * 1.6f : 20;

                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0, 0, 0, stable2015 ? 240 : 230) },
                    hover = new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(255, 255, 255, 50), Alpha = 0 },
                    stable2015
                        // Stable 2015's: size 12 from 12 in, a › before it.
                        ? new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 6 * 1.6f - 5,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            Children = new Drawable[]
                            {
                                new SpriteIcon
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(9),
                                    Icon = FontAwesome.Solid.ChevronRight,
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = text,
                                    Font = StableFont.Get(12),
                                },
                            }
                        }
                        : new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 3,
                            Text = text,
                            Font = OsuFont.GetFont(size: 17),
                        },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hover.FadeIn(80);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover.FadeOut(80);
                base.OnHoverLost(e);
            }
        }
    }
}
