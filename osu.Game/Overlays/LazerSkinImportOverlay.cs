// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Overlays.LazerSkinImport;
using osu.Game.Screens;
using osu.Game.Screens.Menu;

namespace osu.Game.Overlays
{
    /// <summary>
    /// Reprise: skins from osu!'s own lazer on this computer, picked from a list, in the first-run setup's style (one step).
    /// </summary>
    [Cached]
    public partial class LazerSkinImportOverlay : WizardOverlay
    {
        [Resolved]
        private IPerformFromScreenRunner performer { get; set; } = null!;

        public LazerSkinImportOverlay()
            : base(OverlayColourScheme.Purple)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddStep<ScreenImportLazerSkins>();

            Header.Title = "import skins";
            Header.Description = "bring your skins over from osu!lazer";
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

        // Its button imports the ticked skins (in the background) as it closes.
        protected override void ShowNextStep()
        {
            (CurrentScreen as ScreenImportLazerSkins)?.ImportSelected();
            base.ShowNextStep();
        }

        protected override void PopIn()
        {
            base.PopIn();

            // Closed without finishing and opened again: osu!lazer's skins (and ours) may have changed meanwhile.
            (CurrentScreen as ScreenImportLazerSkins)?.Refresh();
        }
    }
}
