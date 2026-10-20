// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.Menu;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>Lazer's 2023 startup screen (behind the "2023 startup screen" setting): shows, for a supporter and not.</summary>
    public partial class TestSceneDisclaimer : ScreenTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name once the screen is shown.</summary>
        public static Action<string>? Screenshot;

        [Test]
        public void TestShown()
        {
            Disclaimer disclaimer = null!;

            AddStep("load disclaimer", () => LoadScreen(disclaimer = new Disclaimer()));
            AddUntilStep("shown", () => disclaimer.IsCurrentScreen());
            AddWaitStep("let it appear", 20);
            AddStep("screenshot", () => Screenshot?.Invoke("disclaimer-2023"));
            AddWaitStep("saved", 5);

            AddStep("toggle support", () =>
            {
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser
                {
                    Username = API.LocalUser.Value.Username,
                    Id = API.LocalUser.Value.Id + 1,
                    IsSupporter = !API.LocalUser.Value.IsSupporter,
                };
            });
        }
    }
}
