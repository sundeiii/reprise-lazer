// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Ranking.Statistics.Session;

namespace osu.Game.Tests.Visual.Ranking
{
    public partial class TestSceneSessionBrowser : ScreenTestScene
    {
        private SessionStatsStore store = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            // three finished sessions, plus the current one.
            Dependencies.Cache(store = SessionStatsTestHelper.CreateStore(LocalStorage, pastSessions: 3, currentPlays: 6));
        }

        [Test]
        public void TestBrowseSessions()
        {
            AddStep("open browser", () => LoadScreen(new SessionBrowserScreen()));

            // "all time" plus one entry per session.
            AddUntilStep("screen is titled", () => this.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "Session stats"));
            AddUntilStep("all sessions listed", () => this.ChildrenOfType<SessionBrowserScreen.SessionListItem>().Count() == store.GetSessions().Count + 1);
            AddUntilStep("current session shown by default", () => this.ChildrenOfType<SessionStatsDisplay>().Any(d => d.IsLoaded));

            AddStep("select all time", () => this.ChildrenOfType<SessionBrowserScreen.SessionListItem>().First().TriggerClick());
            AddUntilStep("all time details shown", () => this.ChildrenOfType<SessionStatsDisplay>().Any(d => d.IsLoaded));

            AddStep("select oldest session", () => this.ChildrenOfType<SessionBrowserScreen.SessionListItem>().Last().TriggerClick());
            AddUntilStep("session details shown", () => this.ChildrenOfType<SessionStatsDisplay>().Any(d => d.IsLoaded));
            AddAssert("trend charts shown", () => this.ChildrenOfType<SessionTrendChart>().Any());
            AddAssert("combined histogram shown", () => this.ChildrenOfType<HitErrorHistogramChart>().Any());
        }

        [Test]
        public void TestBrowseMaps()
        {
            AddStep("open browser", () => LoadScreen(new SessionBrowserScreen()));
            AddUntilStep("sessions listed", () => this.ChildrenOfType<SessionBrowserScreen.SessionListItem>().Count() == store.GetSessions().Count + 1);

            AddStep("switch to maps", () => this.ChildrenOfType<OsuTabControl<SessionBrowserScreen.ListMode>>().Single().Current.Value = SessionBrowserScreen.ListMode.Maps);

            // the seeded plays alternate between two maps.
            AddUntilStep("a map per entry", () => this.ChildrenOfType<SessionBrowserScreen.SessionListItem>().Count() == 2);
            AddAssert("entries are maps", () => this.ChildrenOfType<SessionBrowserScreen.SessionListItem>().All(i => i.Entry.Title.Contains("Map ", StringComparison.Ordinal)));
            AddAssert("entries count attempts", () => this.ChildrenOfType<SessionBrowserScreen.SessionListItem>().All(i => i.Entry.Subtitle.Contains("attempt", StringComparison.Ordinal)));
            AddUntilStep("map details shown", () => this.ChildrenOfType<SessionStatsDisplay>().Any(d => d.IsLoaded));

            AddStep("switch back to sessions", () => this.ChildrenOfType<OsuTabControl<SessionBrowserScreen.ListMode>>().Single().Current.Value = SessionBrowserScreen.ListMode.Sessions);
            AddUntilStep("sessions listed again", () => this.ChildrenOfType<SessionBrowserScreen.SessionListItem>().Count() == store.GetSessions().Count + 1);
        }

        [Test]
        public void TestOpeningAPlayWhichNoLongerExistsDoesNotBreakTheBrowser()
        {
            AddStep("open browser", () => LoadScreen(new SessionBrowserScreen()));
            AddUntilStep("details shown", () => this.ChildrenOfType<SessionStatsDisplay>().Any(d => d.IsLoaded));
            AddUntilStep("plays listed", () => this.ChildrenOfType<OsuClickableContainer>().Any(c => c.TooltipText.ToString() == "Open this play"));

            // the seeded scores were never really saved, so there is nothing to open.
            AddStep("click a play", () => this.ChildrenOfType<OsuClickableContainer>().First(c => c.TooltipText.ToString() == "Open this play").TriggerClick());
            AddAssert("browser still shown", () => this.ChildrenOfType<SessionBrowserScreen>().Single().IsCurrentScreen());
        }
    }

    public partial class TestSceneSessionBrowserEmpty : ScreenTestScene
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(new SessionStatsStore(LocalStorage));
        }

        [Test]
        public void TestNoSessionsYet()
        {
            AddStep("open browser", () => LoadScreen(new SessionBrowserScreen()));
            AddUntilStep("screen loaded", () => this.ChildrenOfType<SessionBrowserScreen>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("nothing listed", () => !this.ChildrenOfType<SessionBrowserScreen.SessionListItem>().Any());
        }
    }
}
