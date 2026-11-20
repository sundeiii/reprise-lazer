// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Overlays.SongSwap;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class SongSwapCandidateTest : RealmTest
    {
        [Test]
        public void TestCandidatesCoverMapAfterStretching()
        {
            RunTestWithRealm((realm, _) =>
            {
                var ruleset = new RulesetInfo { ShortName = "osu" };
                BeatmapInfo difficulty(double length, double bpm = 180) => new BeatmapInfo(ruleset)
                {
                    Length = length,
                    BPM = bpm,
                    Metadata = new BeatmapMetadata { AudioFile = "audio.mp3" },
                };

                var target = difficulty(60000);
                var ownSet = new BeatmapSetInfo(new[] { target });
                target.BeatmapSet = ownSet;

                var shortSong = difficulty(59000);
                var longSong = difficulty(70000);
                longSong.Metadata.AudioFile = "AUDIO.MP3";
                var alternateSong = difficulty(65000);
                alternateSong.Metadata.AudioFile = "alternate.mp3";
                var quarterSong = difficulty(65000, 45);
                var quadrupleSong = difficulty(65000, 720);
                var spedUpSong = difficulty(70000, 144); // At 1.25x this only lasts 56 seconds.
                var slowedSong = difficulty(50000, 225); // At 0.8x this lasts 62.5 seconds.
                var hiddenSong = difficulty(120000);
                hiddenSong.Hidden = true;

                realm.Write(r =>
                {
                    r.Add(ownSet);
                    r.Add(new BeatmapSetInfo(new[] { shortSong, longSong, alternateSong }));
                    r.Add(new BeatmapSetInfo(new[] { quarterSong }));
                    r.Add(new BeatmapSetInfo(new[] { quadrupleSong }));
                    r.Add(new BeatmapSetInfo(new[] { spedUpSong }));
                    r.Add(new BeatmapSetInfo(new[] { slowedSong }));
                    r.Add(new BeatmapSetInfo(new[] { difficulty(59000), hiddenSong }));
                    r.Add(new BeatmapSetInfo(new[] { difficulty(120000) }) { Protected = true });
                    r.Add(new BeatmapSetInfo(new[] { difficulty(120000) }) { DeletePending = true });
                });

                var candidates = ScreenSongSwap.ReadCandidates(realm, target);

                Assert.That(candidates.Select(c => c.Song.ID), Is.EqualTo(new[] { longSong.ID, alternateSong.ID, quarterSong.ID, quadrupleSong.ID, slowedSong.ID }));
                Assert.That(candidates[^1].Stretch, Is.EqualTo(0.8));
                Assert.That(candidates.Any(c => c.Loops), Is.False);

                // With looping on, the short ones are offered too (marked as looping); the hidden, protected and deleted ones still aren't.
                var all = ScreenSongSwap.ReadCandidates(realm, target, includeShort: true);
                Assert.That(all.Count, Is.EqualTo(candidates.Count + 2));
                Assert.That(all.Single(c => c.Song.ID == spedUpSong.ID).Loops, Is.True);
                Assert.That(all.Where(c => !c.Loops).Select(c => c.Song.ID), Is.EquivalentTo(candidates.Select(c => c.Song.ID)));
            });
        }
    }
}
