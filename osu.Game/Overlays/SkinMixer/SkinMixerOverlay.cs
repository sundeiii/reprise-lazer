// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Game.Database;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;

namespace osu.Game.Overlays.SkinMixer
{
    /// <summary>Reprise: a step that makes a skin (the mixer's, the modifier's).</summary>
    public interface ISkinMakingScreen
    {
        /// <summary>Makes it (in the background) and imports it; <paramref name="onCreated"/> gets it (null if it failed).</summary>
        void Create(Action<Live<SkinInfo>?> onCreated);
    }

    /// <summary>
    /// Reprise: making a skin in the game, in the first-run setup's style, over the main menu; its button makes the skin and puts it on.
    /// </summary>
    public abstract partial class SkinMakingOverlay : WizardOverlay
    {
        [Resolved]
        private IPerformFromScreenRunner performer { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected SkinMakingOverlay()
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

        // Its button makes the skin (in the background) as it closes.
        protected override void ShowNextStep()
        {
            if (CurrentScreen is ISkinMakingScreen screen)
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

    /// <summary>
    /// Reprise: a skin mixer in the game (as OsuSkinMixer is outside it): a new skin from a base skin with parts of others, the game's
    /// default or blank in places.
    /// </summary>
    [Cached]
    public partial class SkinMixerOverlay : SkinMakingOverlay
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            AddStep<ScreenSkinMixer>();

            Header.Title = "skin mixer";
            Header.Description = "make a skin from the parts of others";
        }
    }

    /// <summary>
    /// Reprise: a skin modifier in the game (OsuSkinMixer's): a skin's modified copy (instafade circles, a smooth trail, no 300s...).
    /// </summary>
    [Cached]
    public partial class SkinModifierOverlay : SkinMakingOverlay
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            AddStep<ScreenSkinModifier>();

            Header.Title = "skin modifier";
            Header.Description = "change things about a skin";
        }
    }
}
