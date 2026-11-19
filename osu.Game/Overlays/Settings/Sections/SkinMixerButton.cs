// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Overlays.SkinMixer;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>
    /// Reprise: the skin mixer (<see cref="SkinMixerOverlay"/>).
    /// </summary>
    public partial class SkinMixerButton : SettingsButtonV2
    {
        [Resolved]
        private SkinMixerOverlay? overlay { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Text = "Skin mixer...";
            Action = () => overlay?.Show();
        }
    }
}
