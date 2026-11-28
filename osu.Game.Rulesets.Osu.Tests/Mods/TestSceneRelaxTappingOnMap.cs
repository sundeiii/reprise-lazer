// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Storyboards;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    /// <summary>
    /// Reprise: Relax tapping like a player on a real map, to see how it plays: the cursor follows the notes as autoplay's does (no
    /// clicks), Relax clicks with the profile, and the results are written out (judgements, accuracy, UR, how often keys alternate).
    /// Run by hand: RELAX_MAP (an .osu file), RELAX_PROFILE (a player, or empty for Relax's own), RELAX_COPY (1 for their timing),
    /// RELAX_DT (1 for DT), RELAX_SECONDS (how much of the map, from its first note; default 40), RELAX_UPDATE_MS (how far apart the
    /// game updates; default 1), RELAX_DETAIL (1 for each decision around the first misses), RELAX_AUDIO (the set's folder, for its song). OSU_TESTS_NO_TIMEOUT=1 for long ones.
    /// </summary>
    [Explicit("Plays a map from a file, by hand.")]
    public partial class TestSceneRelaxTappingOnMap : OsuModTestScene
    {
        private static string env(string name, string fallback = "") => Environment.GetEnvironmentVariable(name) ?? fallback;

        private double end;

        // The timing the profile drew for each note (real ms), before the cursor had a say.
        private readonly List<double> planned = new List<double>();

        protected override TestPlayer CreateModPlayer(Ruleset ruleset) => new CursorOnlyPlayer(CurrentTestData, AllowFail);

        // With RELAX_AUDIO (the map's unpacked set folder): its song plays along, to watch it as in the game.
        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null) =>
            string.IsNullOrEmpty(env("RELAX_AUDIO")) ? base.CreateWorkingBeatmap(beatmap, storyboard) : new SongWorkingBeatmap(beatmap, Audio, env("RELAX_AUDIO"));

        private class SongWorkingBeatmap : TestWorkingBeatmap
        {
            private readonly ITrackStore tracks;

            public SongWorkingBeatmap(IBeatmap beatmap, AudioManager audio, string folder)
                : base(beatmap, null, audio)
            {
                tracks = audio.GetTrackStore(new StorageBackedResourceStore(new NativeStorage(folder)));
            }

            protected override Track GetBeatmapTrack() => tracks.Get(Beatmap.Metadata.AudioFile);
        }

        [Test]
        public void TestPlay()
        {
            string profileName = env("RELAX_PROFILE");
            bool copy = env("RELAX_COPY") == "1";
            bool dt = env("RELAX_DT") == "1";
            double seconds = double.Parse(env("RELAX_SECONDS", "40"), System.Globalization.CultureInfo.InvariantCulture);

            AddStep("profile", () =>
            {
                OsuModRelax.TestProfile = string.IsNullOrEmpty(profileName) ? null : RelaxTappingProfile.Load(profileName) ?? throw new ArgumentException($"no profile {profileName}");
                OsuModRelax.TestCopyTiming = copy;
                planned.Clear();
                OsuModRelax.TestLog = line =>
                {
                    if (line.StartsWith("PLAN ", StringComparison.Ordinal))
                        planned.Add(double.Parse(line[5..], System.Globalization.CultureInfo.InvariantCulture));
                    else if (env("RELAX_DETAIL") == "1")
                        Console.WriteLine("DECIDE " + line);
                };
            });

            CreateModTest(new ModTestData
            {
                Mods = dt ? new Mod[] { new OsuModRelax(), new OsuModDoubleTime() } : new Mod[] { new OsuModRelax() },
                Autoplay = false,
                CreateBeatmap = () =>
                {
                    using var stream = File.OpenRead(env("RELAX_MAP"));
                    using var reader = new LineBufferedReader(stream);
                    var beatmap = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                    double first = beatmap.HitObjects.First().StartTime;
                    beatmap.HitObjects = beatmap.HitObjects.Where(h => h.StartTime <= first + seconds * 1000 * (dt ? 1.5 : 1)).ToList();
                    end = beatmap.HitObjects.Max(h => h.GetEndTime());
                    return beatmap;
                },
                ReplayFrames = new List<ReplayFrame>(),
                PassCondition = () => true,
            });

            AddUntilStep("played", () => Player.DrawableRuleset.FrameStableClock.CurrentTime > end + 500);
            AddStep("report", () => report(profileName, copy, dt));
            AddStep("clear the test profile", () =>
            {
                OsuModRelax.TestProfile = null;
                OsuModRelax.TestCopyTiming = null;
                OsuModRelax.TestLog = null;
            });
        }

        private void report(string profileName, bool copy, bool dt)
        {
            var processor = Player.ScoreProcessor;
            var basic = processor.HitEvents.Where(e => e.HitObject is Objects.HitCircle && e.Result.IsBasic()).ToList();
            var hits = basic.Where(e => e.Result.IsHit()).ToList();
            double rate = dt ? 1.5 : 1;
            double mean = hits.Average(e => e.TimeOffset);
            double ur = 10 * Math.Sqrt(hits.Average(e => (e.TimeOffset - mean) * (e.TimeOffset - mean))) / rate;

            int count(HitResult r) => basic.Count(e => e.Result == r);

            // Key presses in order, from the recorded frames: how often a press is the other key than the one before.
            var presses = new List<OsuAction>();
            var before = new HashSet<OsuAction>();

            foreach (var frame in Player.Score.Replay.Frames.OfType<OsuReplayFrame>())
            {
                presses.AddRange(frame.Actions.Where(a => !before.Contains(a)));
                before = frame.Actions.ToHashSet();
            }

            double alternate = presses.Count > 1 ? presses.Zip(presses.Skip(1)).Count(p => p.First != p.Second) / (double)(presses.Count - 1) : 0;

            // With RELAX_DETAIL=1: every circle's judgement around the first misses (when, how far off, the gap before it).
            if (env("RELAX_DETAIL") == "1")
            {
                var circles = basic.OrderBy(e => e.HitObject.StartTime).ToList();
                int firstMiss = circles.FindIndex(e => e.Result == HitResult.Miss);

                for (int i = Math.Max(0, firstMiss - 3); i < Math.Min(circles.Count, firstMiss + 12); i++)
                {
                    var e = circles[i];
                    double gap = i > 0 ? e.HitObject.StartTime - circles[i - 1].HitObject.StartTime : 0;
                    Console.WriteLine($"DETAIL {e.HitObject.StartTime,8:0} gap {gap,5:0} {e.Result,-6} offset {e.TimeOffset,7:0.0} pos {((Objects.OsuHitObject)e.HitObject).StackedPosition}");
                }
            }

            Console.WriteLine($"RESULT {Path.GetFileName(env("RELAX_MAP"))} {(dt ? "DT" : "NM")} {(string.IsNullOrEmpty(profileName) ? "Relax's own" : profileName)}{(copy ? " +timing" : "")}: "
                              + $"acc {processor.Accuracy.Value:P2}, 300 {count(HitResult.Great)} / 100 {count(HitResult.Ok)} / 50 {count(HitResult.Meh)} / miss {count(HitResult.Miss)}, "
                              + $"UR {ur:0} (mean {mean / rate:+0.0;-0.0} ms){drawn()}, presses {presses.Count}, alternate {alternate:P0}, combo {processor.HighestCombo.Value}");
        }

        private string drawn()
        {
            if (planned.Count == 0 || planned.All(p => p == 0))
                return string.Empty;

            double mean = planned.Average();
            return $", drawn UR {10 * Math.Sqrt(planned.Average(p => (p - mean) * (p - mean))):0} (mean {mean:+0.0;-0.0} ms)";
        }

        /// <summary>The cursor as autoplay moves it, without its clicks: Relax clicks.</summary>
        private partial class CursorOnlyPlayer : ModTestPlayer
        {
            public CursorOnlyPlayer(ModTestData data, bool allowFail)
                : base(data, allowFail)
            {
            }

            protected override void PrepareReplay()
            {
                var auto = new OsuAutoGenerator(GameplayState.Beatmap, DrawableRuleset.Mods).Generate().Frames.Cast<OsuReplayFrame>().ToList();

                // The game updates about every millisecond (a replay's frames are each played): the cursor's frames are filled in
                // that finely (moving straight between autoplay's), or RELAX_UPDATE_MS apart (e.g. 16.7 for a 60Hz worst case).
                double step = double.Parse(env("RELAX_UPDATE_MS", "1"), System.Globalization.CultureInfo.InvariantCulture);
                var frames = new List<ReplayFrame>();

                for (int i = 0; i < auto.Count; i++)
                {
                    frames.Add(new OsuReplayFrame(auto[i].Time, auto[i].Position));

                    if (i + 1 < auto.Count && step < 16)
                    {
                        for (double t = auto[i].Time + step; t < auto[i + 1].Time; t += step)
                        {
                            float amount = (float)((t - auto[i].Time) / (auto[i + 1].Time - auto[i].Time));
                            frames.Add(new OsuReplayFrame(t, Vector2.Lerp(auto[i].Position, auto[i + 1].Position, amount)));
                        }
                    }
                }

                // As an old replay: Relax presses the keys itself.
                DrawableRuleset.SetReplayScore(new Score
                {
                    Replay = new Replay { Frames = frames },
                    ScoreInfo = new ScoreInfo { User = new APIUser { Username = @"cursor" }, IsLegacyScore = true, Mods = DrawableRuleset.Mods.ToArray() },
                });

                DrawableRuleset.SetRecordTarget(Score);
            }
        }
    }
}
