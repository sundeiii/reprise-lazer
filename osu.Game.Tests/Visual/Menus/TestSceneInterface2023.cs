// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Overlays.Toolbar;
using osu.Game.Screens.Menu;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>The 2023 interface (<see cref="Interface2023"/>): the main menu in 2023's icons, the toolbar's hexagon icons.</summary>
    public partial class TestSceneInterface2023 : OsuTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name once the screen is shown.</summary>
        public static Action<string>? Screenshot;

        [Cached(typeof(INotificationOverlay))]
        private readonly NotificationOverlay notifications = new NotificationOverlay();

        private static readonly string[] hexacons = { "home", "news", "devtools", "wiki", "rankings", "beatmap", "messaging", "social", "music", "settings", "notification" };

        [TearDownSteps]
        public void TearDownSteps() => AddStep("2023 interface off", () => Interface2023.Apply(false));

        [Test]
        public void TestShown()
        {
            ButtonSystem buttons = null!;

            AddStep("2023 interface on", () => Interface2023.Apply(true));
            AddStep("show", () =>
            {
                OsuLogo logo;
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(40, 40, 50, 255) },
                    new Toolbar { State = { Value = Visibility.Visible } },
                    // The toolbar's 2023 icons (the overlays they belong to aren't here, so on their own).
                    new FillFlowContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 60,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(12),
                        ChildrenEnumerable = hexacons.Select(h => new Interface2023.Hexacon(h) { Size = new Vector2(36) }),
                    },
                    buttons = new ButtonSystem(),
                    logo = new OsuLogo { RelativePositionAxes = Axes.Both, Position = new Vector2(0.5f) },
                };
                buttons.SetOsuLogo(logo);
            });
            AddStep("top level", () => buttons.State = ButtonSystemState.TopLevel);
            AddUntilStep("hexacons loaded", () => this.ChildrenOfType<Interface2023.Hexacon>().All(h => h.Texture != null));
            AddUntilStep("menu icons in 2023's font", () => buttons.ChildrenOfType<SpriteIcon>().Any(i => i.Icon.Family == Interface2023.FONT));
            AddWaitStep("let it appear", 15);
            AddStep("screenshot", () => Screenshot?.Invoke("interface-2023"));
            AddWaitStep("saved", 5);
        }
    }
}
