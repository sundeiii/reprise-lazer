// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class OsuSettingsSubsection : RulesetSettingsSubsection
    {
        public OsuSettingsSubsection(Ruleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager gameConfig)
        {
            var config = (OsuRulesetConfigManager)Config;

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = RulesetSettingsStrings.SnakingInSliders,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.SnakingInSliders)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = RulesetSettingsStrings.SnakingOutSliders,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.SnakingOutSliders)
                })
                {
                    ApplyClassicDefault = c => ((IHasCurrentValue<bool>)c).Current.Value = false,
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = RulesetSettingsStrings.HitAnimations,
                    HintText = RulesetSettingsStrings.HitAnimationsOsuTooltip,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.HitAnimations)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = RulesetSettingsStrings.CursorTrail,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.ShowCursorTrail)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = RulesetSettingsStrings.CursorRipples,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.ShowCursorRipples)
                }),
                new SettingsItemV2(new FormEnumDropdown<PlayfieldBorderStyle>
                {
                    Caption = RulesetSettingsStrings.PlayfieldBorderStyle,
                    Current = config.GetBindable<PlayfieldBorderStyle>(OsuRulesetSetting.PlayfieldBorderStyle),
                }),
                // Reprise: Relax tapping like a top player (measured from their replays).
                new SettingsItemV2(new RelaxTappingDropdown
                {
                    Caption = "Relax tapping",
                    HintText = "With Relax, the keys are pressed the way this player taps: alternating or single-tapping as they do at each speed, "
                               + "holding as long as they do. Shown on the key overlay and in replays; Relax still hits every note.",
                    Current = gameConfig.GetBindable<string>(OsuSetting.RelaxTapping),
                })
                {
                    Keywords = new[] { "relax", "tapping", "keys", "alternate", "replay" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Relax: copy their timing too",
                    HintText = "Relax hits each note as early or late as that player would, so accuracy and unstable rate come out like theirs.",
                    Current = gameConfig.GetBindable<bool>(OsuSetting.RelaxCopyTiming),
                })
                {
                    Keywords = new[] { "relax", "timing", "unstable rate", "accuracy" },
                },
            };
        }
    }

    /// <summary>Reprise: "Perfect" (Relax's own tapping) and the players there are tapping profiles of.</summary>
    public partial class RelaxTappingDropdown : FormDropdown<string>
    {
        public RelaxTappingDropdown()
        {
            Items = new[] { string.Empty }.Concat(RelaxTappingProfile.Available).ToArray();
        }

        protected override LocalisableString GenerateItemText(string item) => string.IsNullOrEmpty(item) ? "Perfect (Relax's own)" : item;
    }
}
