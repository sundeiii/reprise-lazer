// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Screens;
using osu.Game.Screens.Menu;

namespace osu.Game.Overlays.LazerImport
{
    /// <summary>
    /// Reprise: things from osu!'s own lazer on this computer, picked from a list (a <see cref="LazerImportScreen"/>, its one step), in the
    /// first-run setup's style. Its button imports what's ticked as it closes. Each kind (skins, beatmaps, collections) has its own.
    /// </summary>
    public abstract partial class LazerImportOverlay : WizardOverlay
    {
        [Resolved]
        private IPerformFromScreenRunner performer { get; set; } = null!;

        protected LazerImportOverlay()
            : base(OverlayColourScheme.Purple)
        {
        }

        public override void Show()
        {
            // As the first-run setup: over the main menu.
            performer.PerformFromScreen(screen =>
            {
                if (screen is MainMenu menu)
                    menu.ReturnToOsuLogo();

                base.Show();
            }, new[] { typeof(MainMenu) });
        }

        // Its button imports what's ticked (in the background) as it closes.
        protected override void ShowNextStep()
        {
            (CurrentScreen as LazerImportScreen)?.ImportSelected();
            base.ShowNextStep();
        }

        protected override void PopIn()
        {
            base.PopIn();

            // Closed without finishing and opened again: osu!lazer's (and ours) may have changed meanwhile.
            (CurrentScreen as LazerImportScreen)?.Refresh();
        }
    }
}
