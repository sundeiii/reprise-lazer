// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osuTK;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Ranking.Statistics.Session;

namespace osu.Game.Tests.Visual.Ranking
{
    public partial class TestSceneSessionStatsDisplay : OsuManualInputManagerTestScene
    {
        [SetUp]
        public void SetUp() => Schedule(() => Clear());

        [Test]
        public void TestEmptySession()
        {
            AddStep("show display", () => showDisplay(createStore(pastSessions: 0, currentPlays: 0)));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("no charts for empty session", () => !this.ChildrenOfType<SessionTrendChart>().Any());
        }

        [Test]
        public void TestSinglePlay()
        {
            AddStep("show display", () => showDisplay(createStore(pastSessions: 0, currentPlays: 1)));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("both trend charts present", () => this.ChildrenOfType<SessionTrendChart>().Count() == 2);

            // the results screen already shows the play's own timing distribution, so the compact view leaves the combined one out.
            AddAssert("no histogram in compact view", () => !this.ChildrenOfType<HitErrorHistogramChart>().Any());
        }

        [Test]
        public void TestCompactViewIsShort()
        {
            AddStep("show display", () => showDisplay(createStore(pastSessions: 0, currentPlays: 6)));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddUntilStep("charts laid out", () => this.ChildrenOfType<SessionTrendChart>().All(c => c.ChildrenOfType<ChartHoverColumn>().All(column => column.DrawWidth > 0)));

            AddStep("log height", () => osu.Framework.Logging.Logger.Log($"[HEIGHT] compact display is {this.ChildrenOfType<SessionStatsDisplay>().Single().DrawHeight}px tall"));

            // the results screen has to fit this next to all its other statistics without scrolling, so it must stay one short row.
            AddAssert("compact view is one short row", () => this.ChildrenOfType<SessionStatsDisplay>().Single().DrawHeight < 100);
        }

        [Test]
        public void TestBestPlayIsStarredOnEachChart()
        {
            AddStep("show display", () => showDisplay(createStore(pastSessions: 0, currentPlays: 6)));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);

            AddAssert("each chart has exactly one star", () => this.ChildrenOfType<SessionTrendChart>().All(chart => chart.ChildrenOfType<SpriteIcon>().Count(i => i.Icon.Equals(FontAwesome.Solid.Star)) == 1));
        }

        [Test]
        public void TestNewPersonalBestIsPointedOut()
        {
            AddStep("show display after a play which beat an earlier one", () =>
            {
                var store = new SessionStatsStore(LocalStorage.GetStorageForDirectory(Guid.NewGuid().ToString("N")));
                var random = new Random(1);

                store.Record(SessionStatsTestHelper.CreateScore(random, 60, "Map A"), DateTimeOffset.Now.AddMinutes(-5));
                store.Record(SessionStatsTestHelper.CreateScore(random, 2, "Map A"), DateTimeOffset.Now);

                showDisplay(store);
            });

            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("unstable rate badge shown", () => this.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "New best unstable rate on this map"));
        }

        [Test]
        public void TestNoBadgeForAFirstPlayOrAWorsePlay()
        {
            AddStep("show display after a first play", () =>
            {
                var store = new SessionStatsStore(LocalStorage.GetStorageForDirectory(Guid.NewGuid().ToString("N")));
                store.Record(SessionStatsTestHelper.CreateScore(new Random(1), 2, "Map A"), DateTimeOffset.Now);

                showDisplay(store);
            });

            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("no badge", () => !this.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().StartsWith("New best", StringComparison.Ordinal)));
        }

        [Test]
        public void TestClickingAPlayReportsIt()
        {
            SessionStatsStore store = null!;
            SessionPlayRecord? clicked = null;

            AddStep("show detailed display", () => showDisplay(store = createStore(pastSessions: 0, currentPlays: 4), detailed: true, playClicked: r => clicked = r));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);

            AddAssert("a row per play can be clicked", () => playRows().Count == 4);

            // rows are listed newest first.
            AddStep("click the newest play", () => playRows().First().TriggerClick());
            AddAssert("that play was reported", () => clicked?.ScoreID == store.CurrentSessionPlays.Last().ScoreID);

            AddStep("click the oldest play", () => playRows().Last().TriggerClick());
            AddAssert("that play was reported", () => clicked?.ScoreID == store.CurrentSessionPlays.First().ScoreID);
        }

        [Test]
        public void TestPlaysCannotBeClickedWithoutAHandler()
        {
            AddStep("show detailed display", () => showDisplay(createStore(pastSessions: 0, currentPlays: 3), detailed: true));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("no clickable rows", () => playRows().Count == 0);
        }

        // rows of the play list which do something when clicked.
        private List<OsuClickableContainer> playRows() => this.ChildrenOfType<OsuClickableContainer>().Where(c => c.TooltipText.ToString() == "Open this play").ToList();

        [Test]
        public void TestDetailedView()
        {
            AddStep("show detailed display", () => showDisplay(createStore(pastSessions: 0, currentPlays: 8), detailed: true));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("both trend charts present", () => this.ChildrenOfType<SessionTrendChart>().Count() == 2);
            AddAssert("histogram present", () => this.ChildrenOfType<HitErrorHistogramChart>().Count() == 1);
            AddStep("log height", () => osu.Framework.Logging.Logger.Log($"[HEIGHT] detailed display is {this.ChildrenOfType<SessionStatsDisplay>().Single().DrawHeight}px tall"));
            AddAssert("detailed view is much taller than the compact one", () => this.ChildrenOfType<SessionStatsDisplay>().Single().DrawHeight > 300);
        }

        [Test]
        public void TestHoveringAPointDoesNotThrowAndRecovers()
        {
            AddStep("show display", () => showDisplay(createStore(pastSessions: 0, currentPlays: 5)));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);

            AddUntilStep("charts laid out", () => this.ChildrenOfType<SessionTrendChart>().All(c => c.ChildrenOfType<ChartHoverColumn>().All(column => column.DrawWidth > 0)));

            AddStep("hover a point", () => InputManager.MoveMouseTo(this.ChildrenOfType<SessionTrendChart>().First().ChildrenOfType<ChartHoverColumn>().ElementAt(2)));
            AddUntilStep("column is hovered", () => this.ChildrenOfType<SessionTrendChart>().First().ChildrenOfType<ChartHoverColumn>().ElementAt(2).IsHovered);
            AddStep("move away", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddUntilStep("column no longer hovered", () => !this.ChildrenOfType<SessionTrendChart>().First().ChildrenOfType<ChartHoverColumn>().ElementAt(2).IsHovered);
        }

        [Test]
        public void TestColumnsStayInsideTheirChart()
        {
            AddStep("show display", () => showDisplay(createStore(pastSessions: 0, currentPlays: 2)));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);

            AddUntilStep("charts laid out", () => this.ChildrenOfType<SessionTrendChart>().All(c => c.ChildrenOfType<ChartHoverColumn>().All(column => column.DrawWidth > 0)));

            // with only two points, the columns used to be as wide as the whole chart and reached into the neighbouring one.
            AddAssert("no column extends beyond its chart", () => this.ChildrenOfType<SessionTrendChart>().All(chart =>
                chart.ChildrenOfType<ChartHoverColumn>().All(column =>
                    column.ScreenSpaceDrawQuad.TopLeft.X >= chart.ScreenSpaceDrawQuad.TopLeft.X - 1
                    && column.ScreenSpaceDrawQuad.TopRight.X <= chart.ScreenSpaceDrawQuad.TopRight.X + 1)));
        }

        [Test]
        public void TestPopulatedSession()
        {
            AddStep("show display", () => showDisplay(createStore(pastSessions: 0, currentPlays: 12)));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("both trend charts present", () => this.ChildrenOfType<SessionTrendChart>().Count() == 2);
            AddAssert("every play is hoverable on both charts", () => trendColumnCount() == 2 * 12);
        }

        [Test]
        public void TestOnlySessionPlaysAreShown()
        {
            SessionStatsStore store = null!;

            AddStep("show display with history", () => showDisplay(store = createStore(pastSessions: 2, currentPlays: 4)));
            AddUntilStep("display loaded", () => this.ChildrenOfType<SessionStatsDisplay>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("history exists", () => store.AllPlays.Count > store.CurrentSessionPlays.Count);
            AddAssert("only current session charted", () => trendColumnCount() == 2 * 4);
        }

        // the histogram has hover columns of its own, so only count the ones belonging to the trend charts.
        private int trendColumnCount() => this.ChildrenOfType<SessionTrendChart>().Sum(c => c.ChildrenOfType<ChartHoverColumn>().Count());

        private SessionStatsStore createStore(int pastSessions, int currentPlays)
            => SessionStatsTestHelper.CreateStore(LocalStorage.GetStorageForDirectory(Guid.NewGuid().ToString("N")), pastSessions, currentPlays);

        private void showDisplay(SessionStatsStore store, bool detailed = false, Action<SessionPlayRecord>? playClicked = null) => Child = new DependencyProvidingContainer
        {
            RelativeSizeAxes = Axes.Both,
            CachedDependencies = new (Type, object)[] { (typeof(SessionStatsStore), store) },
            Child = new OsuTooltipContainer(null)
            {
                RelativeSizeAxes = Axes.Both,
                // the display fills the width it is given, so give it a fixed one here (roughly the results screen statistics area).
                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 1000,
                    AutoSizeAxes = Axes.Y,
                    Child = new SessionStatsDisplay(detailed: detailed, playClicked: playClicked),
                }
            }
        };
    }
}
