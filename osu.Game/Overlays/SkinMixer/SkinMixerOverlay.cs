// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;

namespace osu.Game.Overlays.SkinMixer
{
    /// <summary>
    /// Reprise: a skin mixer in the game (as OsuSkinMixer is outside it): a new skin from a base skin with parts of others, the game's
    /// default or blank in places. In the first-run setup's style; its button makes the skin and puts it on.
    /// </summary>
    [Cached]
    public partial class SkinMixerOverlay : WizardOverlay
    {
        [Resolved]
        private IPerformFromScreenRunner performer { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        public SkinMixerOverlay()
            : base(OverlayColourScheme.Purple)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddStep<ScreenSkinMixer>();

            Header.Title = "skin mixer";
            Header.Description = "make a skin from the parts of others";
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

        // Its button makes the skin (in the background) as it closes.
        protected override void ShowNextStep()
        {
            if (CurrentScreen is ScreenSkinMixer screen)
            {
                // Put on once made, from here (the screen's gone by then). A hidden overlay doesn't run what it schedules, so it keeps
                // running until then.
                AlwaysPresent = true;

                screen.Create(skin => Schedule(() =>
                {
                    if (skin != null)
                        skins.CurrentSkinInfo.Value = skin;

                    AlwaysPresent = false;
                }));
            }

            base.ShowNextStep();
        }
    }
}
