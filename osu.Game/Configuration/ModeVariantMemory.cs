// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Reprise: remembers whether Relax or Autopilot was on (<see cref="OsuSetting.ModeVariant"/>), and turns it back on when the game starts,
    /// in the mode the game starts in (it remembers that itself), and in a mode that has it after one that doesn't (mania). Changes while a
    /// screen holds the mods (a replay, multiplayer) aren't remembered.
    /// </summary>
    public partial class ModeVariantMemory : Component
    {
        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        private Bindable<string> variant = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            variant = config.GetBindable<string>(OsuSetting.ModeVariant);

            apply();

            // Back on in a mode that has it, after one without it (mania) took it off.
            ruleset.BindValueChanged(_ => apply());
            selectedMods.BindValueChanged(_ => remember());
            selectedMods.BindDisabledChanged(_ => remember());
        }

        private void apply()
        {
            if (string.IsNullOrEmpty(variant.Value) || selectedMods.Disabled || selectedMods.Value.Any(m => m.Acronym == variant.Value))
                return;

            if (ruleset.Value.CreateInstance().CreateAllMods().FirstOrDefault(m => m.Acronym == variant.Value) is not Mod mod)
                return;

            var mods = selectedMods.Value.Where(m => m.Acronym != @"RX" && m.Acronym != @"AP").ToList();

            // What can't be played with it goes (as the toolbar's switch does).
            mods.RemoveAll(m => !ModUtils.CheckCompatibleSet(new[] { m, mod }));
            mods.Add(mod);
            selectedMods.Value = mods;
        }

        private void remember()
        {
            if (selectedMods.Disabled)
                return;

            var acronyms = selectedMods.Value.Select(m => m.Acronym).ToArray();
            string current = acronyms.Contains(@"RX") ? @"RX" : acronyms.Contains(@"AP") ? @"AP" : string.Empty;

            // A mode without it (mania; Autopilot outside osu!) doesn't forget it.
            if (current.Length == 0 && variant.Value.Length > 0
                && !ruleset.Value.CreateInstance().CreateAllMods().Any(m => m.Acronym == variant.Value))
                return;

            variant.Value = current;
        }
    }
}
