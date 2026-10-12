// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The retry and watch replay buttons of a legacy ranking screen, on the right edge of the screen.
    /// </summary>
    /// <remarks>
    /// In the legacy 1024x768 layout, retry is at 576px height and watch replay at 672px, or at 576px when there's no retry.
    /// "ranking-retry" and "ranking-replay" fall back to "pause-retry" and "pause-replay".
    /// A button only shows when it has an action. Place this in a container spanning the screen.
    /// </remarks>
    public partial class LegacyRankingButtons : CompositeDrawable
    {
        private const float upper_y = 576;
        private const float lower_y = 672;

        private readonly Action? retry;
        private readonly Action? watchReplay;

        public LegacyRankingButtons(Action? retry, Action? watchReplay)
        {
            this.retry = retry;
            this.watchReplay = watchReplay;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // Stable's: newer skins' pause screen buttons, old skins' "ranking-*" (each the other when the skin hasn't one).
            bool old = LegacyRankingScreen.UsesOldLayout(skin);

            Texture? button(string name) => old
                ? skin.GetTexture($"ranking-{name}") ?? skin.GetTexture($"pause-{name}")
                : skin.GetTexture($"pause-{name}") ?? skin.GetTexture($"ranking-{name}");

            if (retry != null)
                AddInternal(new LegacyRankingButton(button("retry"), retry) { Y = upper_y });

            if (watchReplay != null)
                AddInternal(new LegacyRankingButton(button("replay"), watchReplay) { Y = retry != null ? lower_y : upper_y });
        }

        private partial class LegacyRankingButton : ClickableContainer
        {
            public LegacyRankingButton(Texture? texture, Action action)
            {
                Anchor = Anchor.TopRight;
                Origin = Anchor.CentreRight;
                AutoSizeAxes = Axes.Both;
                Action = action;
                Child = new Sprite { Texture = texture };
            }
        }
    }
}
