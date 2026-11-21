// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// Measures the hit error produced by the override Relax (not <see cref="Mods.OsuModRelax"/>),
    /// with the cursor parked on a stack of circles so every hit is purely down to Relax timing.
    /// </summary>
    public partial class TestSceneRelaxHitTiming : PlayerTestScene
    {
        private const int circle_count = 20;
        private const double first_circle_time = 1500;
        private const double circle_spacing = 500;

        private static readonly Vector2 circle_position = new Vector2(256, 192);

        protected override bool HasCustomSteps => true;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var hitObjects = new List<HitObject>();

            for (int i = 0; i < circle_count; i++)
            {
                double startTime = first_circle_time + i * circle_spacing;

                // alternate circles and (short) sliders, as slider heads go through a different Relax path than plain circles.
                if (i % 2 == 0)
                {
                    hitObjects.Add(new HitCircle
                    {
                        StartTime = startTime,
                        Position = circle_position,
                        HitWindows = new OsuHitWindows(),
                    });
                }
                else
                {
                    hitObjects.Add(new Slider
                    {
                        StartTime = startTime,
                        Position = circle_position,
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(30, 0) }),
                    });
                }
            }

            return new Beatmap
            {
                Difficulty = { OverallDifficulty = 9 },
                HitObjects = hitObjects,
            };
        }

        [Test]
        public void TestHitErrorDistribution()
        {
            CreateTest(() => AddStep("enable override relax", () => enableRelax()));

            AddStep("park cursor on circles", () => InputManager.MoveMouseTo(Player.DrawableRuleset.Playfield.ToScreenSpace(circle_position)));
            AddUntilStep("wait for completion", () => Player.ScoreProcessor.HasCompleted.Value);

            // The headless test host runs a faster-than-realtime clock, so frames can skip over hit windows and not every circle is guaranteed to be hit.
            AddAssert("some circles hit", () => getOffsets().Count, () => Is.GreaterThan(0));

            AddStep("log hit errors", () =>
            {
                var offsets = getOffsets();

                Logger.Log($"[RelaxHitTiming] hit {offsets.Count}/{circle_count}, min {offsets.Min():N2}ms, max {offsets.Max():N2}ms, mean {offsets.Average():N2}ms");
                Logger.Log($"[RelaxHitTiming] circle offsets: {string.Join(", ", getOffsets(e => e.HitObject is not SliderHeadCircle).Select(o => o.ToString("N1")))}");
                Logger.Log($"[RelaxHitTiming] slider head offsets: {string.Join(", ", getOffsets(e => e.HitObject is SliderHeadCircle).Select(o => o.ToString("N1")))}");
            });

            // Absolute lateness can't be asserted here (coarse test clock), but Relax must never click before the hit time. Anything early means an unintended source of input (e.g. the Relax mod's leniency).
            AddAssert("no early hits", () => getOffsets().Min(), () => Is.GreaterThanOrEqualTo(-0.5));
        }

        [Test]
        public void TestConfiguredOffsetDelaysClicks()
        {
            CreateTest(() => AddStep("enable override relax with a late offset", () => enableRelax(offset: 12)));

            AddStep("park cursor on circles", () => InputManager.MoveMouseTo(Player.DrawableRuleset.Playfield.ToScreenSpace(circle_position)));
            AddUntilStep("wait for completion", () => Player.ScoreProcessor.HasCompleted.Value);

            AddAssert("some circles hit", () => getOffsets().Count, () => Is.GreaterThan(0));

            // with the offset left at zero clicks land right at the hit time, so nothing being this late means the setting was used.
            AddAssert("no click before the configured offset", () => getOffsets().Min(), () => Is.GreaterThanOrEqualTo(11.5));
        }

        /// <summary>
        /// Turns Relax on with the given timing.
        /// </summary>
        /// <remarks>
        /// This sets the runtime state which gameplay reads directly, not the config it is normally derived from.
        /// A config value only propagates when it changes, so one left over from an earlier test would stop it from being noticed.
        /// </remarks>
        private static void enableRelax(double offset = 0, double jitter = 0)
        {
            GameplayOverrides.Relax = true;
            GameplayOverrides.RelaxOffset = offset;
            GameplayOverrides.RelaxJitter = jitter;
        }

        [TearDown]
        public void TearDown()
        {
            GameplayOverrides.Relax = false;
            GameplayOverrides.RelaxOffset = 0;
            GameplayOverrides.RelaxJitter = 0;
        }

        private List<double> getOffsets(System.Func<HitEvent, bool> filter = null) => Player.ScoreProcessor.HitEvents
                                                   .Where(e => e.HitObject is HitCircle and not SliderEndCircle && e.Result.IsHit() && (filter?.Invoke(e) ?? true))
                                                   .Select(e => e.TimeOffset)
                                                   .ToList();
    }
}
