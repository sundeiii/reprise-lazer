// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning
{
    /// <summary>
    /// osu!stable's pause and fail screens: the screen darkened, the skin's "pause-overlay" (or, failed, "fail-background" filling the
    /// height), and its "pause-continue", "pause-retry" and "pause-back" buttons down the middle (growing under the mouse, with the
    /// skin's hover and click sounds).
    /// </summary>
    /// <remarks>
    /// Stable's positions, in its 1024x768 layout: the buttons 160 above the middle, 16 and 192 below it.
    /// </remarks>
    public partial class LegacyGameplayMenu : CompositeDrawable
    {
        private readonly bool failed;
        private readonly Action? resume;
        private readonly Action? retry;
        private readonly Action? quit;

        private Box darkening = null!;
        private Drawable? background;
        private Container buttons = null!;

        public LegacyGameplayMenu(bool failed, Action? resume, Action? retry, Action? quit)
        {
            this.failed = failed;
            this.resume = resume;
            this.retry = retry;
            this.quit = quit;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var layout = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(1024, 768),
                Strategy = DrawSizePreservationStrategy.Minimum,
            };

            InternalChildren = new Drawable[]
            {
                darkening = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0,
                },
                layout,
            };

            var texture = failed ? skin.GetTexture(@"fail-background") : skin.GetTexture(@"pause-overlay");

            if (texture != null)
            {
                background = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = texture,
                    Alpha = 0,
                };

                // Failed: stable's fills the height.
                if (failed)
                    background.Scale = new Vector2(768f / texture.DisplayHeight);

                layout.Add(background);
            }

            layout.Add(buttons = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = 0,
            });

            if (!failed && resume != null)
                buttons.Add(new Button(skin, "continue", "Continue", new Color4(70, 180, 60, 255), resume) { Y = -100 * 1.6f });
            if (retry != null)
                buttons.Add(new Button(skin, "retry", "Retry", new Color4(230, 160, 20, 255), retry) { Y = 10 * 1.6f });
            if (quit != null)
                buttons.Add(new Button(skin, "back", "Back to Menu", new Color4(200, 40, 50, 255), quit) { Y = 120 * 1.6f });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Stable's: the darkening to 80%, the rest in over 300ms (failed, the background over 600).
            darkening.FadeTo(0.8f, 300);
            background?.FadeIn(failed ? 600 : 300);
            buttons.FadeIn(300);
        }

        private partial class Button : ClickableContainer
        {
            private readonly Drawable content;
            private readonly SkinnableSound hoverSound;
            private readonly SkinnableSound clickSound;

            public Button(ISkinSource skin, string name, string text, Color4 colour, Action action)
            {
                Action = action;
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                AutoSizeAxes = Axes.Both;

                Texture? texture = skin.GetTexture($"pause-{name}");

                content = texture != null
                    ? new Sprite { Texture = texture }
                    // Lazer's default skin has none of these: a button like stable's default one.
                    : new Container
                    {
                        Size = new Vector2(400, 80),
                        Masking = true,
                        CornerRadius = 16,
                        BorderThickness = 3,
                        BorderColour = Color4.White.Opacity(0.8f),
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Framework.Graphics.Colour.ColourInfo.GradientVertical(colour.Lighten(0.3f), colour.Darken(0.3f)),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = text,
                                Font = OsuFont.GetFont(size: 40, weight: FontWeight.Bold),
                            },
                        }
                    };

                content.Anchor = Anchor.Centre;
                content.Origin = Anchor.Centre;

                Children = new Drawable[]
                {
                    content,
                    hoverSound = new SkinnableSound(new SampleInfo($"pause-{name}-hover")),
                    clickSound = new SkinnableSound(new SampleInfo($"pause-{name}-click")),
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSound.Play();
                content.ScaleTo(1.1f, 300, Easing.Out);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                content.ScaleTo(1, 300, Easing.Out);
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
