// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Skinning.Mixer;

namespace osu.Game.Tests.Skins
{
    /// <summary>Reprise: the skin mixer: a base skin with parts from others, the game's default, or blank.</summary>
    [TestFixture]
    public class SkinMixerTest
    {
        private static SkinMixerSource skin(string name, params (string path, string content)[] files) =>
            new SkinMixerSource(name, files.Select(f => new KeyValuePair<string, Func<byte[]>>(f.path, () => Encoding.UTF8.GetBytes(f.content))));

        private static string text(Dictionary<string, byte[]> files, string path) => Encoding.UTF8.GetString(files[path]);

        private static T part<T>(SkinMixerOption[] parts, Func<T, bool> which) where T : SkinMixerOption =>
            parts.SelectMany(p => p.Leaves).OfType<T>().First(which);

        private static SkinMixerGroup group(SkinMixerOption[] parts, string name)
        {
            foreach (var g in flatten(parts).OfType<SkinMixerGroup>())
            {
                if (g.Name == name)
                    return g;
            }

            throw new InvalidOperationException(name);

            static IEnumerable<SkinMixerOption> flatten(IEnumerable<SkinMixerOption> options) =>
                options.SelectMany(o => new[] { o }.Concat(o is SkinMixerGroup g ? flatten(g.Children) : Enumerable.Empty<SkinMixerOption>()));
        }

        private readonly SkinMixerSource baseSkin = skin("base",
            ("skin.ini", "[General]\r\nName: base\r\nAuthor: someone\r\nVersion: 2.7\r\n\r\n[Colours]\r\nCombo1: 255,0,0\r\nCombo2: 0,255,0\r\n"),
            ("hitcircle.png", "base circle"),
            ("hitcircle@2x.png", "base circle 2x"),
            ("hitcircleoverlay.png", "base overlay"),
            ("hit300.png", "base 300"),
            ("hit300-0.png", "base 300 frame"),
            ("cursor.png", "base cursor"),
            ("hud.json", "{}"));

        private readonly SkinMixerSource other = skin("other",
            ("skin.ini", "[General]\r\nName: other\r\n[Colours]\r\nCombo1: 0,0,255\r\n[Fonts]\r\nScorePrefix: fonts/score\r\n[Mania]\r\nKeys: 4\r\nColumnWidth: 40\r\n"),
            ("hitcircle.png", "other circle"),
            ("cursor.png", "other cursor"),
            ("cursor@2x.png", "other cursor 2x"),
            ("fonts/score-0.png", "other score 0"),
            ("fonts/score-1.png", "other score 1"));

        [Test]
        public void TestNothingChosenKeepsTheBase()
        {
            var mixed = SkinMixing.Mix(baseSkin, SkinMixerParts.Create(), "mixed");

            Assert.That(mixed.Keys.Where(k => k != "skin.ini"), Is.EquivalentTo(baseSkin.Files.Keys.Where(k => k != "skin.ini")));
            Assert.That(text(mixed, "skin.ini"), Does.Contain("Name: mixed").And.Contain("Author: someone").And.Contain("Combo2: 0,255,0"));
            Assert.That(text(mixed, "hud.json"), Is.EqualTo("{}"), "the layout comes along");
        }

        [Test]
        public void TestPartFromAnotherSkinReplacesTheBases()
        {
            var parts = SkinMixerParts.Create();
            part<SkinMixerFileOption>(parts, o => o.FileName == "hitcircle").Choice = SkinMixerChoice.From(other);

            var mixed = SkinMixing.Mix(baseSkin, parts, "mixed");

            Assert.That(text(mixed, "hitcircle.png"), Is.EqualTo("other circle"));
            Assert.That(mixed.ContainsKey("hitcircle@2x.png"), Is.False, "the base's @2x of it went too (other has none)");
            Assert.That(text(mixed, "hitcircleoverlay.png"), Is.EqualTo("base overlay"), "a different part stays");
        }

        [Test]
        public void TestGameDefaultRemovesIt()
        {
            var parts = SkinMixerParts.Create();
            part<SkinMixerFileOption>(parts, o => o.FileName == "cursor").Choice = SkinMixerChoice.DEFAULT;

            Assert.That(SkinMixing.Mix(baseSkin, parts, "mixed").ContainsKey("cursor.png"), Is.False);
        }

        [Test]
        public void TestBlankHidesItWithItsFrames()
        {
            var parts = SkinMixerParts.Create();
            part<SkinMixerFileOption>(parts, o => o.FileName == "hit300").Choice = SkinMixerChoice.BLANK;

            var mixed = SkinMixing.Mix(baseSkin, parts, "mixed");

            Assert.That(mixed["hit300.png"], Is.EqualTo(SkinMixing.TRANSPARENT_PNG));
            Assert.That(mixed["hit300@2x.png"], Is.EqualTo(SkinMixing.TRANSPARENT_PNG));
            Assert.That(mixed.ContainsKey("hit300-0.png"), Is.False, "its animation frames go");
        }

        [Test]
        public void TestGroupChoiceGoesToEachPart()
        {
            var parts = SkinMixerParts.Create();
            var cursor = group(parts, "Cursor");

            foreach (var leaf in cursor.Leaves)
                leaf.Choice = SkinMixerChoice.From(other);

            var mixed = SkinMixing.Mix(baseSkin, parts, "mixed");

            Assert.That(text(mixed, "cursor.png"), Is.EqualTo("other cursor"));
            Assert.That(text(mixed, "cursor@2x.png"), Is.EqualTo("other cursor 2x"));
        }

        [Test]
        public void TestIniSettings()
        {
            var parts = SkinMixerParts.Create();
            part<SkinMixerIniProperty>(parts, o => o.Property == "Combo1").Choice = SkinMixerChoice.From(other);
            part<SkinMixerIniProperty>(parts, o => o.Property == "Combo2").Choice = SkinMixerChoice.DEFAULT;

            string ini = text(SkinMixing.Mix(baseSkin, parts, "mixed"), "skin.ini");

            Assert.That(ini, Does.Contain("Combo1: 0,0,255"));
            Assert.That(ini, Does.Not.Contain("Combo2"));
        }

        [Test]
        public void TestFontPrefixBringsItsFiles()
        {
            var parts = SkinMixerParts.Create();
            part<SkinMixerIniProperty>(parts, o => o.Property == "ScorePrefix").Choice = SkinMixerChoice.From(other);

            var mixed = SkinMixing.Mix(baseSkin, parts, "mixed");

            Assert.That(text(mixed, "skin.ini"), Does.Contain("ScorePrefix: fonts/score"));
            Assert.That(text(mixed, "fonts/score-1.png"), Is.EqualTo("other score 1"));
        }

        [Test]
        public void TestManiaSection()
        {
            var parts = SkinMixerParts.Create();
            part<SkinMixerIniSection>(parts, o => o.Value == "4").Choice = SkinMixerChoice.From(other);

            string ini = text(SkinMixing.Mix(baseSkin, parts, "mixed"), "skin.ini");

            Assert.That(ini, Does.Contain("[Mania]").And.Contain("Keys: 4").And.Contain("ColumnWidth: 40"));
        }

        [Test]
        public void TestEmptyBaseIsTheLatestVersion()
        {
            // The game's classic skin as the base: no files, no skin.ini.
            var parts = SkinMixerParts.Create();
            part<SkinMixerFileOption>(parts, o => o.FileName == "cursor").Choice = SkinMixerChoice.From(other);

            var mixed = SkinMixing.Mix(skin("osu! (classic)"), parts, "mine");

            Assert.That(text(mixed, "skin.ini"), Does.Contain("Name: mine").And.Contain("Version: latest"));
            Assert.That(text(mixed, "cursor.png"), Is.EqualTo("other cursor"));
            Assert.That(mixed.Count, Is.EqualTo(3));
        }

        [Test]
        public void TestMatching()
        {
            var back = new SkinMixerFileOption("menu-back", false, isAnimatable: true);
            Assert.That(back.Matches("menu-back-3@2x.png"), Is.True);
            Assert.That(back.Matches("menu-back.png"), Is.True);
            Assert.That(back.Matches("menu-background.png"), Is.False);
            Assert.That(back.Matches("menu-back.wav"), Is.False, "not a sound");

            var numbers = new SkinMixerFileOption("default-*", false, allowedSuffixes: new[] { "0", "1" });
            Assert.That(numbers.Matches("default-1@2x.png"), Is.True);
            Assert.That(numbers.Matches("default-7.png"), Is.False);

            Assert.That(new SkinMixerFileOption("cursor", false).Matches("fonts/cursor.png"), Is.False, "only the skin's top level");
        }
    }
}
