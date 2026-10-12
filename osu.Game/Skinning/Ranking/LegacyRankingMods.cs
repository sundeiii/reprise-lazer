// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using Vector2 = osuTK.Vector2;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The score's mods on a legacy ranking screen's first page, as stable's: the skin's "selection-mod-*" icons, the first centred on
    /// (600, 260) of stable's 640x480 field, which stays centred on wide screens (280 right of the middle), each next one 20 to the left
    /// (1.6 times all in the 1024x768 layout), each growing in half a second after the one before. Skins' mods boxes are there.
    /// </summary>
    public partial class LegacyRankingMods : CompositeDrawable
    {
        private static readonly Vector2 first_position = new Vector2(600 - 320, 260) * 1.6f;
        private const float spacing = 20 * 1.6f;

        private readonly ScoreInfo score;

        public LegacyRankingMods(ScoreInfo score)
        {
            this.score = score;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            int index = 0;

            foreach (var icon in CreateIcons(score.Ruleset, score.Mods, skin))
            {
                icon.Anchor = Anchor.TopCentre;
                icon.Origin = Anchor.Centre;
                icon.Position = first_position - new Vector2(spacing * index, 0);
                icon.Alpha = 0;

                AddInternal(icon);

                icon.Delay(500 * index)
                    .ScaleTo(2).ScaleTo(1, 400, Easing.Out)
                    .FadeIn(400, Easing.Out);

                index++;
            }
        }

        /// <summary>
        /// Stable's icons for <paramref name="mods"/>, in its order (its mods' values): the skin's "selection-mod-*", else the game's
        /// icon; Nightcore and Perfect without the mods they include. None for a ruleset that can't say its legacy mods.
        /// </summary>
        public static IEnumerable<Drawable> CreateIcons(IRulesetInfo rulesetInfo, IReadOnlyList<Mod> scoreMods, ISkinSource skin)
        {
            LegacyMods mods;
            Ruleset ruleset;

            try
            {
                ruleset = rulesetInfo.CreateInstance();
                mods = ruleset.ConvertToLegacyMods(scoreMods.ToArray());
            }
            catch (Exception)
            {
                yield break;
            }

            foreach (var mod in Enum.GetValues<LegacyMods>())
            {
                if (BitOperations.PopCount((uint)mod) != 1 || !mods.HasFlag(mod))
                    continue;

                if ((mod == LegacyMods.DoubleTime && mods.HasFlag(LegacyMods.Nightcore)) || (mod == LegacyMods.SuddenDeath && mods.HasFlag(LegacyMods.Perfect)))
                    continue;

                // Stable's name for Autopilot is "Relax2".
                string name = mod == LegacyMods.Autopilot ? "relax2" : mod.ToString().ToLowerInvariant();

                if (skin.GetTexture($"selection-mod-{name}") is { } texture)
                {
                    yield return new Sprite { Texture = texture };
                    continue;
                }

                // Lazer's default skin has none of these: lazer's icon for the mod.
                var lazerMod = scoreMods.FirstOrDefault(m => ruleset.ConvertToLegacyMods(new[] { m }).HasFlag(mod));

                if (lazerMod != null)
                    yield return new ModIcon(lazerMod, showTooltip: false) { Scale = new Vector2(0.6f) };
            }
        }
    }
}
