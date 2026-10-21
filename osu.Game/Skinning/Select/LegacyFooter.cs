// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Framework.Utils;
using osu.Game.Graphics.Containers;
using osu.Game.Screens.Menu;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Select
{
    public partial class LegacyFooter : CompositeDrawable
    {
        private Container components = null!;
        private LogoTrackingContainer logoTrackingContainer = null!;
        private IDisposable? logoTracking;

        private const float buttons_pos_4_3 = 120 * 1.6f;
        private const float buttons_pos_16_9 = 140 * 1.6f;

        // What the back and footer buttons do (nothing when unset).
        public Action? BackAction { get; init; }
        public Action? ModeAction { get; init; }
        public Action? ModsAction { get; init; }
        public Action? RandomAction { get; init; }
        public Action? OptionsAction { get; init; }

        /// <summary>
        /// Placed (unseen) where the options button is, so a popover from it (beatmap options) opens there.
        /// </summary>
        public Drawable? OptionsPopoverTarget { get; init; }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, OsuConfigManager config, TextureStore textures)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            const float mods_button_off = 57.6f * 1.6f;
            const float random_button_off = mods_button_off + 48 * 1.6f;
            const float options_button_off = random_button_off + 48 * 1.6f;
            const float user_pos_off = options_button_off + 48 * 2 * 1.6f;

            InternalChildren = new Drawable[]
            {
                createBottom(skin, config.Is2015(skin)
                    ? textures.Get(@"Stable/songselect-bottom@2x")
                    : null),
                new ClickableContainer
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                    Action = BackAction,
                    Child = new LegacyBackButton
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                    },
                },
                components = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                    X = buttons_pos_16_9,
                    Children = new Drawable[]
                    {
                        new LegacyFooterUser
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            X = user_pos_off + 3 * 1.6f,
                            Y = 2 * 1.6f,
                        },
                        new Container
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new LegacyRulesetFooterButton { Action = ModeAction },
                                new LegacyFooterButton("mods") { X = mods_button_off, Action = ModsAction },
                                new LegacyFooterButton("random") { X = random_button_off, Action = RandomAction },
                                new LegacyFooterButton("options") { X = options_button_off, Action = OptionsAction },
                                (OptionsPopoverTarget ?? Empty()).With(t =>
                                {
                                    t.Anchor = Anchor.BottomLeft;
                                    t.Origin = Anchor.BottomLeft;
                                    t.X = options_button_off;
                                    t.Alpha = 0;
                                    t.AlwaysPresent = true;
                                    // A point, so it takes no clicks from the button; the popover still opens from here.
                                    t.Scale = new Vector2(0.001f);
                                }),
                            }
                        },
                        // Above all the buttons, like stable's (centred on the mode button, 56 up).
                        new LegacyRulesetIcon
                        {
                            BypassAutoSizeAxes = Axes.Both,
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            X = 57.6f / 2 * 1.6f,
                            Y = -35 * 1.6f,
                        },
                    }
                },
                (logoTrackingContainer = new LogoTrackingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                }).WithChild(logoTrackingContainer.LogoFacade.With(f =>
                {
                    f.Anchor = Anchor.BottomRight;
                    f.Origin = Anchor.Centre;
                    // todo: lazer's positioning of the logo differs from stable, but for aesthetic purposes it's better to use lazer's.
                    // having the logo shift position when switching between a lazer and a legacy skin would look awkward.
                    // for reference, stable's positioning of the logo is close to Vector2(-70, -50).
                    f.Position = new Vector2(-76, -36);
                })),
            };
        }

        /// <summary>
        /// The skin's "songselect-bottom", or (lazer's default legacy skin has none) stable's default look:
        /// a dark bar with a thin coloured line along its top.
        /// </summary>
        /// <summary>The name of stable 2015's own bottom (a skin's can look the same).</summary>
        public const string STABLE_2015_BOTTOM = @"Stable 2015 bottom";

        private static Drawable createBottom(ISkinSource skin, Texture? stableBottom)
        {
            // Stable 2015's is always its own (101 high, stretched across); skins' came later.
            if (stableBottom != null)
            {
                return new Sprite
                {
                    Name = STABLE_2015_BOTTOM,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Texture = stableBottom,
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = 101,
                };
            }

            var texture = skin.GetTexture(@"songselect-bottom");

            if (texture != null)
            {
                // At the bottom, like stable's (the footer is taller than the bar: the player card reaches above it).
                return new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Texture = texture,
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                };
            }

            return new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 88,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 3,
                        Colour = ColourInfo.GradientHorizontal(new Color4(120, 60, 255, 255), new Color4(60, 160, 255, 255)),
                    },
                }
            };
        }

        protected override void Update()
        {
            base.Update();

            bool isWidescreen = Precision.DefinitelyBigger(DrawWidth, 1024);
            components.X = isWidescreen ? buttons_pos_16_9 : buttons_pos_4_3;
        }

        public void StartTrackingLogo(OsuLogo logo, float duration = 0, Easing easing = Easing.None)
        {
            logoTracking = logoTrackingContainer.StartTracking(logo, duration, easing);
        }

        public void StopTrackingLogo()
        {
            logoTracking?.Dispose();
            logoTracking = null;
        }
    }
}
