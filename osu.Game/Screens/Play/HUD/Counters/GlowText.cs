// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>
    /// Reprise: text with a glow of its colour around it, as a CSS text-shadow (0 0 10px) draws it. Unlike <see cref="GlowingSpriteText"/>,
    /// its letters keep their own widths (that one sets every glyph in a digit-wide cell, for scores).
    /// </summary>
    public partial class GlowText : GlowingDrawable
    {
        private OsuSpriteText text = null!;

        public GlowText()
        {
            BlurSigma = new Vector2(4);
            EffectBlending = BlendingParameters.Additive;
        }

        public LocalisableString Text
        {
            get => text.Text;
            set
            {
                text.Text = value;
                // The glow is drawn once into a kept image: drawn again for the new text (a grade going from SS to S kept showing SS).
                ForceRedraw();
            }
        }

        public FontUsage Font
        {
            get => text.Font;
            set
            {
                text.Font = value;
                ForceRedraw();
            }
        }

        public ColourInfo TextColour
        {
            get => text.Colour;
            set
            {
                text.Colour = value;
                ForceRedraw();
            }
        }

        public Vector2 Spacing
        {
            get => text.Spacing;
            set => text.Spacing = value;
        }

        protected override Drawable CreateDrawable() => text = new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Shadow = false,
        };
    }
}
