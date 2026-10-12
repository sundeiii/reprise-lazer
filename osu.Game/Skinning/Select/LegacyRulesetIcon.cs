// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets;

namespace osu.Game.Skinning.Select
{
    /// <summary>
    /// The current mode's "mode-*-small" on the mode button, drawn (like stable's) above all the footer's buttons and additively:
    /// skins (XooMoon) draw art around it on black, which then doesn't show, and the other buttons' images don't cover it.
    /// </summary>
    public partial class LegacyRulesetIcon : CompositeDrawable
    {
        private Sprite icon = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = icon = new Sprite
            {
                Origin = Anchor.Centre,
                Blending = BlendingParameters.Additive,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(r =>
            {
                icon.Texture = skin.GetTexture($@"mode-{r.NewValue.ShortName}-small");
                // Stable grows it in each time.
                icon.ScaleTo(0).ScaleTo(1, 1000, Easing.OutElastic);
            }, true);
        }
    }
}
