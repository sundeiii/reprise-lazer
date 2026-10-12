// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.General;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.CursorRotation,
                    Current = config.GetBindable<bool>(OsuSetting.CursorRotation)
                })
                {
                    Keywords = [@"spin"],
                },
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = UserInterfaceStrings.MenuCursorSize,
                    Current = config.GetBindable<float>(OsuSetting.MenuCursorSize),
                    KeyboardStep = 0.01f,
                    LabelFormat = v => $"{v:0.##}x"
                }),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = UserInterfaceStrings.Parallax,
                    Current = config.GetBindable<float>(OsuSetting.MenuParallaxScale),
                    DisplayAsPercentage = true,
                    LabelFormat = v => v == 0 ? CommonStrings.Disabled : FormSliderBar<float>.DefaultLabelFormat(v, true),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = UserInterfaceStrings.HoldToConfirmActivationTime,
                    Current = config.GetBindable<double>(OsuSetting.UIHoldActivationDelay),
                    KeyboardStep = 50,
                    LabelFormat = v => $"{v:N0} ms",
                })
                {
                    Keywords = [@"delay"],
                    ApplyClassicDefault = c => ((IHasCurrentValue<double>)c).Current.Value = 0,
                },
                new UIThemeDropdownAndRestart(),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Custom UI hue",
                    HintText = "Apply a custom colour hue to interface panels.",
                    Current = config.GetBindable<bool>(OsuSetting.CustomUIHueEnabled),  })
                {
                    Keywords = new[] { "colour", "color", "theme", "hue", "ui" }
                },
                new SettingsItemV2(new FormHuePicker
                {
                    Caption = "UI hue",
                    Current = config.GetBindable<float>(OsuSetting.CustomUIHue),
                })
                {
                    Keywords = new[] { "colour", "color", "theme", "hue", "ui" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Separate accent hue",
                    HintText = "Use a separate accent colour for highlights.",
                    Current = config.GetBindable<bool>(OsuSetting.CustomUIAccentEnabled),
                })
                {
                    Keywords = new[] { "accent", "colour", "color", "theme", "hue" }
                },
                new SettingsItemV2(new FormHuePicker
                {
                    Caption = "Accent hue",
                    Current = config.GetBindable<float>(OsuSetting.CustomUIAccentHue),
                })
                {
                    Keywords = new[] { "accent", "colour", "color", "theme", "hue" }
                },
                new SettingsItemV2(new FormEnumDropdown<ResultScreenStyle>
                {
                    Caption = "Results screen style",
                    HintText = "Choose which results screen layout to use.",
                    Current = config.GetBindable<ResultScreenStyle>(OsuSetting.ResultScreenStyle),
                })
                {
                    Keywords = new[] { "results", "ranking", "score", "clean", "stable" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Auto-hide toolbar",
                    HintText = "Hide the top toolbar until the cursor is near the top of the screen.",
                    Current = config.GetBindable<bool>(OsuSetting.ToriiAutoHideToolbar),
                })
                {
                    Keywords = new[] { "toolbar", "auto hide", "autohide", "top bar" }
                },
            };
        }
    }
}
