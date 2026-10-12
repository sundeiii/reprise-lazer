// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>
    /// The main menu with "Use osu!stable layouts" and a legacy skin: osu!stable's 2015 menu.
    /// </summary>
    public partial class TestSceneStableMainMenu : OsuGameTestScene
    {
        private ButtonSystem buttons => Game.ChildrenOfType<ButtonSystem>().Single();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("don't fetch online content", () => Game.ChildrenOfType<OnlineMenuBanner>().Single().FetchOnlineContent = false);
            AddStep("disable return to top on idle", () => buttons.ReturnToTopOnIdle = false);
            AddStep("set legacy skin", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value = Game.Dependencies.Get<SkinManager>().DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Game.LocalConfig.SetValue(OsuSetting.StableLayouts, true));
        }

        [Test]
        public void TestStableMenu()
        {
            AddUntilStep("stable menu", () => Game.ChildrenOfType<LegacyMainMenu>().Any(m => m.IsLoaded));
            AddStep("open the menu", () => buttons.State = ButtonSystemState.TopLevel);

            // Stable's button images: Play, Edit, Options, Exit, and no toolbar.
            AddUntilStep("stable's buttons", () => shownButtons().SequenceEqual(new[] { "play", "edit", "options", "exit" }));
            AddAssert("game's buttons hidden", () => buttons.StableLayout);
            AddUntilStep("toolbar hidden", () => Game.Toolbar.State.Value == Visibility.Hidden);

            // The game's logo underneath, unseen, where stable's is: left of the middle.
            AddUntilStep("logo where stable's is", () =>
            {
                var logo = Game.ChildrenOfType<OsuLogo>().Single();
                var expected = LegacyMainMenu.LogoPosition(logo.Parent!.DrawSize, false);
                return (logo.Position - expected).Length < 0.001f && expected.X < 0.5f;
            });
            AddAssert("game's logo unseen", () => Game.ChildrenOfType<OsuLogo>().Single().Colour == Color4.Transparent);
            AddAssert("stable's logo drawn", () => Game.ChildrenOfType<LegacyMainMenu>().Single().ChildrenOfType<Sprite>()
                                                       .Any(s => s.Texture != null && s.IsPresent && s.DrawWidth > 300));

            AddStep("click Play", () => button("play").TriggerClick());
            AddUntilStep("Freeplay, Multiplayer and Back", () => shownButtons().SequenceEqual(new[] { "freeplay", "multiplayer", "back" }));
            // The game's online modes under Multiplayer, as more of stable's buttons.
            AddStep("click Multiplayer", () => button("multiplayer").TriggerClick());
            AddUntilStep("online modes", () => shownButtons().SequenceEqual(new[] { "lobby", "rankedplay", "playlists", "dailychallenge", "back" }));
            AddStep("click Back", () => button("back", ButtonSystemState.Multi).TriggerClick());
            AddUntilStep("back to Play", () => buttons.State == ButtonSystemState.Play);

            AddStep("click Back", () => button("back").TriggerClick());
            AddUntilStep("back to the top", () => buttons.State == ButtonSystemState.TopLevel && shownButtons().Contains("play"));

            // Left alone: the logo to the middle; and back.
            AddStep("idle", () => buttons.StableIdle = true);
            AddUntilStep("logo in the middle", () => (Game.ChildrenOfType<OsuLogo>().Single().Position - new osuTK.Vector2(0.5f)).Length < 0.001f);
            AddStep("not idle", () => buttons.StableIdle = false);

            // Stable's music bar: the song shown.
            AddUntilStep("song shown", () => Game.ChildrenOfType<LegacyMainMenu>().Single().ChildrenOfType<osu.Game.Graphics.Sprites.TruncatingSpriteText>()
                                                 .Any(t => t.Alpha > 0 && t.Text.ToString().Length > 0));

            // Stable's font, from the game's own resources.
            AddAssert("stable's font", () => new[] { "Aller-Light", "Aller-Regular", "Aller-Bold" }.All(f => Game.Dependencies.Get<FontStore>().Get(f, 'A') != null));

            // Closing: everything round the logo goes (and stays gone).
            AddStep("close the menu", () => buttons.State = ButtonSystemState.Exit);
            AddUntilStep("corners gone", () => corners().Alpha == 0);
            AddWaitStep("a while", 5);
            AddAssert("still gone", () => corners().Alpha == 0);
            AddStep("open the menu again", () => buttons.State = ButtonSystemState.TopLevel);
            AddUntilStep("corners back", () => corners().Alpha == 1);

            AddStep("stop using stable layouts", () => Game.LocalConfig.SetValue(OsuSetting.StableLayouts, false));
            AddUntilStep("game's menu", () => !Game.ChildrenOfType<LegacyMainMenu>().Any() && !buttons.StableLayout);
            AddUntilStep("game's logo seen again", () => Game.ChildrenOfType<OsuLogo>().Single().Colour == Color4.White);
        }

        // The player card, info, music bar... round the logo.
        private osu.Framework.Graphics.Drawable corners() => Game.ChildrenOfType<LegacyMainMenu>().Single().ChildrenOfType<osu.Game.Skinning.Select.LegacyFooterUser>().Single().Parent!;

        // The buttons shown (named by their image), top to bottom.
        private string[] shownButtons() => Game.ChildrenOfType<LegacyMainMenu>().SingleOrDefault()?.ChildrenOfType<ClickableContainer>()
                                               .Where(c => c.Alpha > 0.5f && isMenuButton(c))
                                               .OrderBy(c => c.Y)
                                               .Select(name)
                                               .ToArray() ?? System.Array.Empty<string>();

        private static readonly string[] button_names =
            { "play", "edit", "options", "exit", "freeplay", "multiplayer", "back", "lobby", "rankedplay", "playlists", "dailychallenge" };

        private static bool isMenuButton(ClickableContainer c) => button_names.Contains(c.Name) && c.ChildrenOfType<Sprite>().All(s => s.Texture != null);

        private static string name(ClickableContainer c) => c.Name;

        // The shown one of that name (both Play's and Multiplayer's have a Back).
        private ClickableContainer button(string buttonName, ButtonSystemState? _ = null) => Game.ChildrenOfType<LegacyMainMenu>().Single().ChildrenOfType<ClickableContainer>()
                                                                                              .Where(isMenuButton)
                                                                                              .First(c => name(c) == buttonName && c.Alpha > 0.5f);
    }
}
