// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Framework.Input.Events;
using osu.Game.Audio;

namespace osu.Game.Skinning.Select
{
    public partial class LegacyFooterButton : ClickableContainer
    {
        private readonly string kind;

        private Sprite hoverSprite = null!;
        private SkinnableSound hoverSound = null!;
        private SkinnableSound clickSound = null!;

        public LegacyFooterButton(string kind)
        {
            this.kind = kind;

            // On the bottom, like stable's: a button is as tall as its hover image, and skins that draw their whole layout with
            // "selection-mode" give the mode button a 1x1 one (at the top of the row, it lifted their layout off the bottom).
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            Enabled.Value = true;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            AutoSizeAxes = Axes.Both;

            var texture = skin.GetTexture($"selection-{kind}");

            Children = new Drawable[]
            {
                // Under it, the skin's normal-resolution image, where its "@2x" one is see-through and the normal one isn't (BTMC's
                // selection-mode @2x lacks the top of its layout, which stable at 768 high shows from the normal one).
                new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Texture = normalResolution(skin, $"selection-{kind}", texture),
                    BypassAutoSizeAxes = Axes.Both,
                },
                new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Texture = texture,
                    // to match stable, the button input area should not be taken from this sprite. it should be taken from the hover sprite below.
                    // see: https://github.com/peppy/osu-stable-reference/blob/c34a74fb61c17c5667486a12548485d1f03baa2e/osu!/GameModes/Select/SongSelection.cs#L340-L349
                    BypassAutoSizeAxes = Axes.Both,
                },
                hoverSprite = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Texture = skin.GetTexture($"selection-{kind}-over"),
                    Alpha = 0f,
                    AlwaysPresent = true,
                },
                hoverSound = new SkinnableSound(new SampleInfo("click-short")),
                clickSound = new SkinnableSound(new SampleInfo("click-short-confirm")),
            };
        }

        // The skin's normal-resolution image when it's as big as the one shown (else none: the same image twice).
        private static Texture? normalResolution(ISkinSource skin, string name, Texture? shown)
        {
            if (shown == null || shown.ScaleAdjust == 1 || skin.FindProvider(s => s.GetTexture(name) != null) is not LegacySkin legacySkin)
                return null;

            var normal = legacySkin.GetNormalResolutionTexture(name);
            return normal != null && Precision.AlmostEquals(normal.DisplayWidth, shown.DisplayWidth, 1) ? normal : null;
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverSprite.FadeIn(100);
            hoverSound.Play();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverSprite.FadeOut(100);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            clickSound.Play();
            return base.OnClick(e);
        }
    }
}
