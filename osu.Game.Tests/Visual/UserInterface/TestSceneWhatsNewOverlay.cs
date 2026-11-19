// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Updater;

namespace osu.Game.Tests.Visual.UserInterface
{
    /// <summary>Reprise: what's new after an update (<see cref="WhatsNewOverlay"/>), from a release's notes.</summary>
    public partial class TestSceneWhatsNewOverlay : OsuTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name once the screen is shown.</summary>
        public static Action<string>? Screenshot;

        private WhatsNewOverlay overlay = null!;

        [SetUp]
        public void SetUp() => Schedule(() => Child = overlay = new WhatsNewOverlay());

        [Test]
        public void TestRunsScheduledWorkWhileHidden()
        {
            TestWhatsNewOverlay hidden = null!;
            bool ran = false;

            // What ShowFor does once the notes come in (show them, or restart without them) is scheduled while the overlay is hidden.
            AddStep("hidden overlay", () => Child = hidden = new TestWhatsNewOverlay());
            AddAssert("hidden", () => hidden.State.Value == Visibility.Hidden);
            AddStep("schedule work", () => hidden.Later(() => ran = true));
            AddUntilStep("it runs", () => ran);
        }

        private partial class TestWhatsNewOverlay : WhatsNewOverlay
        {
            public void Later(Action action) => Schedule(action);
        }

        [Test]
        public void TestShown()
        {
            AddStep("show notes", () => overlay.ShowNotes("v2026.1021.2-lazer", "## What's new\n\n"
                                                                              + "- Relax/Autopilot remembered (back on when the game starts, and after mania)\n"
                                                                              + "- \"Use these mods\" on Song Select V1's leaderboard scores\n"
                                                                              + "- 2023 interface: the signed-in card gets a line with the rank in the mode played\n\n"
                                                                              + "**Full Changelog**: https://github.com/sundeiii/reprise-lazer/compare/a...b"));
            AddUntilStep("shown", () => overlay.State.Value == Visibility.Visible);
            AddWaitStep("let it appear", 10);
            AddStep("screenshot", () => Screenshot?.Invoke("whats-new"));
        }

        [Test]
        public void TestNotShownWithoutNotes()
        {
            AddStep("only the changelog link", () => overlay.ShowNotes("v2026.1021.2-lazer", "**Full Changelog**: https://github.com/sundeiii/reprise-lazer/compare/a...b"));
            AddAssert("not shown", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestRestartButtonForAReadyUpdate()
        {
            bool restarted = false;

            AddStep("show notes of a ready update", () => overlay.ShowNotes("v2026.1119.0-lazer", "- skins from osu!lazer", () => restarted = true));
            AddUntilStep("shown", () => overlay.State.Value == Visibility.Visible);

            AddStep("press restart", () => overlay.ChildrenOfType<RoundedButton>().Single(b => b.Text.ToString() == "Restart to update").TriggerClick());
            AddAssert("restarted", () => restarted);
            AddUntilStep("closed", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestNoRestartButtonAfterAnUpdate()
        {
            AddStep("show notes", () => overlay.ShowNotes("v2026.1119.0-lazer", "- skins from osu!lazer"));
            AddUntilStep("shown", () => overlay.State.Value == Visibility.Visible);
            AddAssert("no restart button", () => overlay.ChildrenOfType<RoundedButton>().Single(b => b.Text.ToString() == "Restart to update").Parent!.Alpha == 0);
        }

        [Test]
        public void TestCleanDropsChangelogLink()
        {
            AddAssert("link gone", () => WhatsNewOverlay.Clean("- one\r\n\r\n**Full Changelog**: https://x"), () => Is.EqualTo("- one"));
        }
    }
}
