// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Replays;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    /// <summary>
    /// Reprise: Relax tapping like a player (<see cref="RelaxTappingProfile"/>): their alternation decides the keys, every note is still
    /// hit, and with their timing copied the hits land where they would.
    /// </summary>
    public partial class TestSceneOsuModRelaxTapping : TestSceneOsuModRelax
    {
        private const int notes = 12;
        private const double spacing = 100;

        private static RelaxTappingProfile profile(double alternate, double offset = 0) => new RelaxTappingProfile
        {
            Name = "test",
            FirstKeyLeft = 1,
            Buckets = new[]
            {
                new RelaxTappingProfile.Bucket
                {
                    Alternate = alternate,
                    Hold = Enumerable.Repeat(40.0, 11).ToArray(),
                    Offset = Enumerable.Repeat(offset, 11).ToArray(),
                },
            },
        };

        // A stream of notes 100ms apart in one place, the cursor resting on them.
        private static Beatmap stream() => new Beatmap
        {
            // Not stacked: the cursor stays on every note.
            StackLeniency = 0,
            Difficulty = { OverallDifficulty = 5 },
            HitObjects = Enumerable.Range(0, notes).Select(i => (HitObject)new HitCircle
            {
                StartTime = 1000 + i * spacing,
                Position = new Vector2(256, 192),
                HitWindows = new OsuHitWindows(),
            }).ToList(),
        };

        private static List<ReplayFrame> cursorOnNotes() => new List<ReplayFrame>
        {
            new OsuReplayFrame(0, new Vector2(256, 192)),
            new OsuReplayFrame(1000 + notes * spacing + 500, new Vector2(256, 192)),
        };

        private List<OsuAction> pressesInOrder()
        {
            var presses = new List<OsuAction>();
            var before = new HashSet<OsuAction>();

            foreach (var frame in Player.Score.Replay.Frames.OfType<OsuReplayFrame>())
            {
                presses.AddRange(frame.Actions.Where(a => !before.Contains(a)));
                before = frame.Actions.ToHashSet();
            }

            return presses;
        }

        private static OsuModRelax relax(RelaxTappingProfile tapping, bool copyTiming)
        {
            OsuModRelax.TestProfile = tapping;
            OsuModRelax.TestCopyTiming = copyTiming;
            return new OsuModRelax();
        }

        [TearDownSteps]
        public void ClearTestProfile() => AddStep("clear the test profile", () =>
        {
            OsuModRelax.TestProfile = null;
            OsuModRelax.TestCopyTiming = null;
        });

        [Test]
        public void TestAlternating() => CreateModTest(new ModTestData
        {
            Mod = relax(profile(alternate: 1), copyTiming: false),
            Autoplay = false,
            CreateBeatmap = stream,
            ReplayFrames = cursorOnNotes(),
            PassCondition = () => Player.ScoreProcessor.Combo.Value == notes
                                  && pressesInOrder().Count == notes
                                  && pressesInOrder().Zip(pressesInOrder().Skip(1)).All(p => p.First != p.Second),
        });

        [Test]
        public void TestSingleTapping() => CreateModTest(new ModTestData
        {
            Mod = relax(profile(alternate: 0), copyTiming: false),
            Autoplay = false,
            CreateBeatmap = stream,
            ReplayFrames = cursorOnNotes(),
            PassCondition = () => Player.ScoreProcessor.Combo.Value == notes
                                  && pressesInOrder().Count == notes
                                  && pressesInOrder().Distinct().Count() == 1,
        });

        [Test]
        public void TestCopiedTiming() => CreateModTest(new ModTestData
        {
            Mod = relax(profile(alternate: 1, offset: 20), copyTiming: true),
            Autoplay = false,
            CreateBeatmap = stream,
            ReplayFrames = cursorOnNotes(),
            PassCondition = () => Player.ScoreProcessor.Combo.Value == notes
                                  && Player.ScoreProcessor.HitEvents.All(e => e.TimeOffset >= 19 && e.TimeOffset <= 40),
        });
    }
}
