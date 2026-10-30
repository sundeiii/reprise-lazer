// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Skinning.Ranking;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// osu!stable's mod icons in gameplay (with a legacy skin and <see cref="OsuSetting.StableLayouts"/>, instead of the game's): the
    /// skin's "selection-mod-*", the first centred on (610, 94) of stable's 640x480 field: 30 in from the right edge, under the score,
    /// as the client keeps it on wide screens (its coordinates there are scaled by height/480, not centred). Each next one 10 to the
    /// left (1.6 times all in the 1024x768 layout), each popping in half a second after the one before; fading out over 5 seconds,
    /// except in replays. From the 2015 client (Class63).
    /// </summary>
    public partial class LegacyGameplayMods : CompositeDrawable
    {
        private const float spacing = 10 * 1.6f;

        /// <summary>The first icon's centre, from the top right corner of the 1024x768 layout.</summary>
        private static readonly Vector2 first_position = new Vector2(-(640 - 610) * 1.6f, 94 * 1.6f);

        private readonly IRulesetInfo ruleset;
        private readonly IReadOnlyList<Mod> mods;
        private readonly Func<bool> isReplay;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        private Bindable<bool> stableLayouts = null!;
        private Bindable<float> uiScale = null!;

        private Container icons = null!;

        /// <summary>
        /// The icons shown.
        /// </summary>
        public IEnumerable<Drawable> Icons => icons.Children.OfType<Container>().Select(holder => holder.Child);

        private bool shown;

        public LegacyGameplayMods(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, Func<bool> isReplay)
        {
            this.ruleset = ruleset;
            this.mods = mods;
            this.isReplay = isReplay;
        }

        /// <summary>
        /// Whether these replace the game's mod display.
        /// </summary>
        public static bool IsActive(OsuConfigManager config, SkinManager skinManager) =>
            config.Get<bool>(OsuSetting.StableLayouts) && skinManager.CurrentSkin.Value is LegacySkin;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            stableLayouts = config.GetBindable<bool>(OsuSetting.StableLayouts);
            uiScale = config.GetBindable<float>(OsuSetting.UIScale);

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(1024, 768),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Child = icons = new Container { RelativeSizeAxes = Axes.Both },
            };
        }

        protected override void Update()
        {
            base.Update();

            // With the HUD, in stable's 16:9 area.
            StableHudArea.Apply(this, true, uiScale.Value);

            bool active = stableLayouts.Value && skinManager.CurrentSkin.Value is LegacySkin;
            Alpha = active ? 1 : 0;

            if (active && !shown)
            {
                shown = true;
                show();
            }
        }

        private void show()
        {
            int index = 0;
            bool replay = isReplay();

            foreach (var icon in LegacyRankingMods.CreateIcons(ruleset, mods, skin))
            {
                icon.Anchor = Anchor.Centre;
                icon.Origin = Anchor.Centre;
                icon.Alpha = 0;

                // Each in a holder centred where stable centres it, the icon centred in it so it pops in from its middle as in stable.
                icons.Add(new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Size = icon.Size * icon.Scale,
                    Position = first_position - new Vector2(spacing * index, 0),
                    Child = icon,
                });

                var appear = icon.Delay(500 * index).ScaleTo(2).ScaleTo(1, 400, Easing.Out).FadeIn(400, Easing.Out);

                // Stable's fade out over 5 seconds from when each appears, except when watching.
                if (!replay)
                    appear.Then().FadeOut(5000 - 400);

                index++;
            }
        }
    }
}
