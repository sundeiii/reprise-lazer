// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Screens.Play.HUD.Counters;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>Reprise: the tosu pp counters in a real (autoplayed) play: their progress lines follow the play.</summary>
    public partial class TestSceneTosuStyleCountersInPlay : OsuPlayerTestScene
    {
        public static Action<string>? Screenshot;

        protected override bool Autoplay => true;

        protected override bool HasCustomSteps => true;

        [Test]
        public void TestProgressFollowsPlay()
        {
            CreateTest();

            AddStep("add the counters", () => Player.HUDOverlay.Add(new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new osuTK.Vector2(0, 60),
                Children = new Drawable[]
                {
                    new SimplisticPerformancePointsCounter { Scale = osuTK.Vector2.One },
                    new DarkAndWhitePerformancePointsCounter { Scale = osuTK.Vector2.One },
                },
            }));

            AddUntilStep("into the map", () => Player.GameplayClockContainer.CurrentTime > Beatmap.Value.Beatmap.HitObjects[40].StartTime);
            AddStep("log", () => log());
            AddUntilStep("map progress moving", () => stats().All(s => s.MapProgress.Value > 0.05));
            AddUntilStep("song progress moving", () => stats().All(s => s.SongProgress.Value > 0.05));
            AddUntilStep("simplistic line keeps up with the song", () => Math.Abs(fillWidth<SimplisticPerformancePointsCounter>() - stats()[0].SongProgress.Value) < 0.01);
            AddUntilStep("dark and white line keeps up with the map", () => Math.Abs(fillWidth<DarkAndWhitePerformancePointsCounter>() - stats()[1].MapProgress.Value) < 0.01);
            AddStep("log", () => log());
            AddStep("screenshot", () => Screenshot?.Invoke("tosu-in-play"));
        }

        private void log()
        {
            foreach (var s in stats())
                Console.WriteLine($"time {Player.GameplayClockContainer.CurrentTime:0} song {s.SongProgress.Value:0.000} map {s.MapProgress.Value:0.000} track {Beatmap.Value.Track.Length:0}");
            Console.WriteLine($"fills {fillWidth<SimplisticPerformancePointsCounter>():0.000} {fillWidth<DarkAndWhitePerformancePointsCounter>():0.000}");
        }

        // The Simplistic counter's, then the DarkAndWhite one's.
        private LivePlayStats[] stats() => new[]
        {
            Player.ChildrenOfType<SimplisticPerformancePointsCounter>().Single().ChildrenOfType<LivePlayStats>().Single(),
            Player.ChildrenOfType<DarkAndWhitePerformancePointsCounter>().Single().ChildrenOfType<LivePlayStats>().Single(),
        };

        private float fillWidth<T>() where T : Drawable
            => Player.ChildrenOfType<T>().Single().ChildrenOfType<Drawable>().Where(d => d.RelativeSizeAxes == Axes.Both && d.Width < 1 && d.Width >= 0 && d.Height == 1)
                     .Select(d => d.Width).DefaultIfEmpty(-1).Max();
    }
}
