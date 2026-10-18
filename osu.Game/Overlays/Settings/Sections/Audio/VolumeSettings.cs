// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public partial class VolumeSettings : SettingsSubsection
    {
        protected override LocalisableString Header => AudioSettingsStrings.VolumeHeader;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                // Reprise: the menu and interface sounds as lazer had them in 2023.
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "2023 sound effects",
                    HintText = "The menu and interface sounds as lazer had them in 2023 (the main menu's buttons, notifications, overlays, the music player). Applies after restarting the game.",
                    Current = config.GetBindable<bool>(OsuSetting.Sounds2023),
                })
                {
                    Keywords = new[] { "old", "classic", "2023", "sfx", "samples" }
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AudioSettingsStrings.MasterVolume,
                    Current = audio.Volume,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    PlaySamplesOnAdjust = false,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AudioSettingsStrings.MasterVolumeInactive,
                    Current = config.GetBindable<double>(OsuSetting.VolumeInactive),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AudioSettingsStrings.EffectVolume,
                    Current = audio.VolumeSample,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    PlaySamplesOnAdjust = false,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AudioSettingsStrings.MusicVolume,
                    Current = audio.VolumeTrack,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    PlaySamplesOnAdjust = false,
                }),
            };
        }
    }
}
