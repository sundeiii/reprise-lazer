// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Skinning
{
    /// <summary>
    /// osu!stable's button dialog (its beatmap options, for one): the screen darkened, a title at the top left, and numbered buttons
    /// down the middle made of the skin's "button-left", "button-middle" and "button-right" in each button's colour, sliding in from
    /// alternate sides. A button (or its number key) does its action and closes; clicking elsewhere or back closes.
    /// </summary>
    /// <remarks>
    /// Stable's sizes (its 640x480, x1.6 here): buttons 460 by 40, 50 apart from 50 under the title.
    /// </remarks>
    public partial class LegacyButtonDialog : CompositeDrawable, IKeyBindingHandler<GlobalAction>
    {
        /// <param name="Closes">Whether pressing it closes the dialog (stable's mod selection's reset doesn't).</param>
        public record DialogButton(string Text, Color4 Colour, Action? Action, bool Closes = true);

        private const float button_width = 460 * 1.6f;
        private const float button_height = 40 * 1.6f;
        private const float button_spacing = 50 * 1.6f;

        private readonly string title;
        private readonly IReadOnlyList<DialogButton> buttons;
        private readonly List<Button> buttonDrawables = new List<Button>();

        /// <summary>
        /// Called once it's closed (and gone).
        /// </summary>
        public Action? Closed { get; init; }

        /// <summary>
        /// Something between the title and the buttons (stable's mod selection's mods), in the 1024x768 layout.
        /// </summary>
        public Drawable? Content { get; init; }

        /// <summary>
        /// Where the first button is (in the 1024x768 layout), when there's <see cref="Content"/> above them.
        /// </summary>
        public float? ButtonsTop { get; init; }

        public LegacyButtonDialog(string title, IReadOnlyList<DialogButton> buttons)
        {
            this.title = title;
            this.buttons = buttons;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // Stable's: at the top left, wrapping across (its mod selection's is a sentence).
            var titleText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 33))
            {
                Position = new Vector2(2 * 1.6f),
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Right = 8 },
                Text = title,
            };

            var flow = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = ButtonsTop ?? 2 * 1.6f + 38 + 50 * 1.6f,
                Width = button_width,
                AutoSizeAxes = Axes.Y,
            };

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0, 0, 0, 200),
                },
                new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(1024, 768),
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Children = new[] { titleText, Content ?? Empty(), flow },
                },
            };

            var left = skin.GetTexture(@"button-left");
            var middle = skin.GetTexture(@"button-middle");
            var right = skin.GetTexture(@"button-right");

            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                int index = i;

                var button = new Button($"{i + 1}. {b.Text}", b.Colour, left, middle, right)
                {
                    Y = button_spacing * i,
                    Action = () => press(index),
                };

                // Stable's: one after another, from alternate sides.
                button.FadeOut().MoveToX(i % 2 == 0 ? 40 * 1.6f : -40 * 1.6f)
                      .Delay(60 * i)
                      .FadeIn(800, Easing.Out)
                      .MoveToX(0, 800, Easing.OutQuint);

                buttonDrawables.Add(button);
                flow.Add(button);
            }
        }

        // The keyboard is its own while open (the buttons' numbers, not typed into song select's search).
        public override bool AcceptsFocus => true;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(200);
            GetContainingFocusManager()?.ChangeFocus(this);
        }

        private bool closing;

        private void press(int index)
        {
            if (closing)
                return;

            if (buttons[index].Closes)
                close();

            buttons[index].Action?.Invoke();
        }

        private void close()
        {
            if (closing)
                return;

            closing = true;
            this.FadeOut(200).Expire();

            if (HasFocus)
                GetContainingFocusManager()?.ChangeFocus(null);

            Closed?.Invoke();
        }

        /// <summary>
        /// Closes it (as clicking outside the buttons does).
        /// </summary>
        public void Close() => close();

        protected override bool OnClick(ClickEvent e)
        {
            close();
            return true;
        }

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnScroll(ScrollEvent e) => true;

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // The buttons' numbers.
            int number = e.Key switch
            {
                >= Key.Number1 and <= Key.Number9 => e.Key - Key.Number1,
                >= Key.Keypad1 and <= Key.Keypad9 => e.Key - Key.Keypad1,
                _ => -1,
            };

            if (number >= 0 && number < buttons.Count)
            {
                buttonDrawables[number].TriggerClick();
                return true;
            }

            return base.OnKeyDown(e);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action != GlobalAction.Back)
                return false;

            close();
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        private partial class Button : ClickableContainer
        {
            private readonly Drawable background;
            private readonly Color4 colour;
            private readonly SkinnableSound hoverSound;
            private readonly SkinnableSound clickSound;

            public Button(string text, Color4 colour, Texture? left, Texture? middle, Texture? right)
            {
                // Stable's: the colour a little darker, lighter under the mouse.
                this.colour = colour.Darken(0.1f);

                Size = new Vector2(button_width, button_height);

                background = left != null && middle != null && right != null
                    ? new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Sprite { Texture = left, Height = button_height, Width = left.DisplayWidth * button_height / left.DisplayHeight },
                            new Sprite
                            {
                                Texture = middle,
                                RelativeSizeAxes = Axes.X,
                                Height = button_height,
                            }.With(s =>
                            {
                                float sideWidth = left.DisplayWidth * button_height / left.DisplayHeight;
                                s.X = sideWidth;
                                s.Width = (button_width - sideWidth * 2) / button_width;
                            }),
                            new Sprite
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Texture = right,
                                Height = button_height,
                                Width = right.DisplayWidth * button_height / right.DisplayHeight,
                            },
                        }
                    }
                    // Lazer's default skin has none: a bar like stable's default one.
                    : new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = button_height / 2,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Framework.Graphics.Colour.ColourInfo.GradientVertical(Color4.White, Color4.White.Opacity(0.75f)),
                        },
                    };

                background.Colour = this.colour;

                Children = new Drawable[]
                {
                    background,
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = text,
                        Font = OsuFont.GetFont(size: 30, weight: FontWeight.Bold),
                        Shadow = true,
                    },
                    hoverSound = new SkinnableSound(new SampleInfo("click-short")),
                    clickSound = new SkinnableSound(new SampleInfo("click-short-confirm")),
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSound.Play();
                background.FadeColour(colour.Lighten(0.3f), 100);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(colour, 400);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                clickSound.Play();
                return base.OnClick(e);
            }
        }
    }
}
