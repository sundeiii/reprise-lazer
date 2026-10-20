// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Graphics;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarHomeButton : ToolbarButton
    {
        public ToolbarHomeButton()
        {
            ButtonContent.Width *= 1.4f;
            Hotkey = GlobalAction.Home;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            TooltipMain = ToolbarStrings.HomeHeaderTitle;
            TooltipSub = ToolbarStrings.HomeHeaderDescription;
            // Reprise: the 2023 interface's hexagon icon.
            if (Interface2023.Enabled)
                SetIcon(new Interface2023.Hexacon(@"home"));
            else
                SetIcon(OsuIcon.Home);
        }
    }
}
