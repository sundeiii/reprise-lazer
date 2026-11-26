// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Skinning.Mixer;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Animations timed by the skin's AnimationFramerate, mixed in from a skin timed otherwise, play as they did there.
    /// </summary>
    public class SkinMixerRetimingTest
    {
        // Rafis' follow point: 10 frames, no AnimationFramerate (so a second over them, 100ms each), the arrow on frames 3 to 5.
        private static SkinMixerSource rafis() => source("Rafis", "[General]\nVersion: 2.2\n",
            Enumerable.Range(0, 10).Select(i => ($"followpoint-{i}.png", i is >= 3 and <= 5 ? "arrow" : i == 0 ? "dot" : "blank")));

        private static SkinMixerSource source(string name, string ini, IEnumerable<(string path, string content)> files) =>
            new SkinMixerSource(name, files.Append((path: "skin.ini", content: ini))
                                           .Select(f => new KeyValuePair<string, Func<byte[]>>(f.path, () => Encoding.UTF8.GetBytes(f.content))));

        private static Dictionary<string, byte[]> mix(SkinMixerSource baseSkin, SkinMixerSource from)
        {
            var followPoints = new SkinMixerFileOption("followpoint", false, "Follow points", true) { Choice = SkinMixerChoice.From(from) };
            return SkinMixing.Mix(baseSkin, new SkinMixerOption[] { followPoints }, "mixed");
        }

        private static string[] frames(Dictionary<string, byte[]> skin) =>
            Enumerable.Range(0, 1000).TakeWhile(i => skin.ContainsKey($"followpoint-{i}.png"))
                      .Select(i => Encoding.UTF8.GetString(skin[$"followpoint-{i}.png"])).ToArray();

        [Test]
        public void TestIntoSixtyFrames()
        {
            var baseSkin = source("Base", "[General]\nVersion: 2.7\nAnimationFramerate: 60\n",
                Enumerable.Range(0, 60).Select(i => ($"Followpoint-{i}.png", "old")));

            var result = frames(mix(baseSkin, rafis()));

            // A second at 60 frames a second: each of Rafis' frames 6 times.
            Assert.That(result, Has.Length.EqualTo(60));
            Assert.That(result.Take(6), Is.All.EqualTo("dot"));
            Assert.That(result.Skip(18).Take(18), Is.All.EqualTo("arrow"));
            Assert.That(result.Skip(36), Is.All.EqualTo("blank"));
            Assert.That(result, Has.None.EqualTo("old"), "the base's own frames gone");
        }

        [Test]
        public void TestSameTimingUntouched()
        {
            var baseSkin = source("Base", "[General]\nVersion: 2.4\n", new[] { ("followpoint.png", "old") });

            var result = frames(mix(baseSkin, rafis()));

            Assert.That(result, Has.Length.EqualTo(10));
            Assert.That(result[3], Is.EqualTo("arrow"));
        }

        [Test]
        public void TestFromAFramerateIntoAnother()
        {
            // 4 frames at 10 a second (400ms) into a skin at 20: 8 frames, each twice.
            var from = source("From", "[General]\nAnimationFramerate: 10\n", Enumerable.Range(0, 4).Select(i => ($"followpoint-{i}.png", $"f{i}")));
            var baseSkin = source("Base", "[General]\nAnimationFramerate: 20\n", Array.Empty<(string, string)>());

            Assert.That(frames(mix(baseSkin, from)), Is.EqualTo(new[] { "f0", "f0", "f1", "f1", "f2", "f2", "f3", "f3" }));
        }
    }
}
