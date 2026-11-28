// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
using osu.Game.Rulesets.Scoring;
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

        // A frame every millisecond, as the game updates about that often (each of a replay's frames is played).
        private static List<ReplayFrame> cursorOnNotes() => Enumerable.Range(0, (int)(1000 + notes * spacing + 500))
                                                                      .Select(t => (ReplayFrame)new OsuReplayFrame(t, new Vector2(256, 192)))
                                                                      .ToList();

        // Jumps: notes 100ms apart, 300px to and fro, the cursor on each only at its time (moving straight on, as autoplay's does).
        private static Beatmap jumps() => new Beatmap
        {
            StackLeniency = 0,
            Difficulty = { OverallDifficulty = 5 },
            HitObjects = Enumerable.Range(0, notes).Select(i => (HitObject)new HitCircle
            {
                StartTime = 1000 + i * spacing,
                Position = jumpPosition(i),
                HitWindows = new OsuHitWindows(),
            }).ToList(),
        };

        private static Vector2 jumpPosition(int i) => new Vector2(i % 2 == 0 ? 106 : 406, 192);

        // A frame every millisecond, as the game updates about that often (each of a replay's frames is played); or as far apart as
        // a slow computer's updates.
        private static List<ReplayFrame> cursorThroughJumps(double step = 1)
        {
            var frames = new List<ReplayFrame> { new OsuReplayFrame(0, jumpPosition(0)) };

            for (double t = 900; t <= 1000 + notes * spacing; t += step)
            {
                int i = Math.Clamp((int)((t - 1000) / spacing), 0, notes - 1);
                frames.Add(new OsuReplayFrame(t, i + 1 < notes ? Vector2.Lerp(jumpPosition(i), jumpPosition(i + 1), (float)Math.Max(0, (t - 1000 - i * spacing) / spacing)) : jumpPosition(i)));
            }

            return frames;
        }

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

        // Their late hits wait only while the cursor is surely still on the note: on jumps it's gone straight after, so they come sooner
        // (never a miss), but on the last note, where it stays, as late as theirs. Hits early are as theirs.
        [Test]
        public void TestCopiedLateTimingOnJumps() => CreateModTest(new ModTestData
        {
            Mod = relax(profile(alternate: 1, offset: 20), copyTiming: true),
            Autoplay = false,
            CreateBeatmap = jumps,
            ReplayFrames = cursorThroughJumps(),
            PassCondition = () => Player.ScoreProcessor.Combo.Value == notes
                                  && Player.ScoreProcessor.HitEvents.All(e => e.Result == HitResult.Great)
                                  && Player.ScoreProcessor.HitEvents.All(e => e.HitObject.StartTime == 1000 + (notes - 1) * spacing ? e.TimeOffset >= 19 : e.TimeOffset < 10),
        });

        // A slow computer (60 updates a second): a hit a little late, the cursor gone by the update after, is made in the one before.
        [TestCase(3)]
        [TestCase(20)]
        public void TestCopiedLateTimingOnJumpsWithSlowUpdates(double offset) => CreateModTest(new ModTestData
        {
            Mod = relax(profile(alternate: 1, offset: offset), copyTiming: true),
            Autoplay = false,
            CreateBeatmap = jumps,
            ReplayFrames = cursorThroughJumps(1000 / 60.0),
            PassCondition = () => Player.ScoreProcessor.Combo.Value == notes && Player.ScoreProcessor.HitEvents.All(e => e.Result == HitResult.Great),
        });

        [Test]
        public void TestCopiedEarlyTimingOnJumps() => CreateModTest(new ModTestData
        {
            Mod = relax(profile(alternate: 1, offset: -8), copyTiming: true),
            Autoplay = false,
            CreateBeatmap = jumps,
            ReplayFrames = cursorThroughJumps(),
            PassCondition = () => Player.ScoreProcessor.Combo.Value == notes
                                  && Player.ScoreProcessor.HitEvents.All(e => e.Result == HitResult.Great && e.TimeOffset <= -8 && e.TimeOffset > -20),
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
