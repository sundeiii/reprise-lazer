// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The client moved from Rinari (rinarii.de, then rinarii.xyz) to reprise.pw, and a saved config keeps whatever URL it was last given.
    /// </summary>
    [TestFixture]
    public class CustomApiUrlMigrationTest
    {
        private static string loadCustomApiUrl(string? savedValue)
        {
            using var storage = new TemporaryNativeStorage("custom-api-url-migration-test");

            if (savedValue != null)
            {
                using var stream = storage.GetStream("game.ini", FileAccess.Write, FileMode.Create);
                using var writer = new StreamWriter(stream);
                writer.WriteLine($"CustomApiUrl = {savedValue}");
            }

            using var config = new OsuConfigManager(storage);
            return config.Get<string>(OsuSetting.CustomApiUrl);
        }

        [Test]
        public void TestFreshConfigDefaultsToReprise()
        {
            Assert.That(loadCustomApiUrl(null), Is.EqualTo("osu.reprise.pw"));
        }

        [TestCase("lazer-api.rinarii.de", "osu.reprise.pw")]
        [TestCase("lazer-api.rinarii.xyz", "osu.reprise.pw")]
        [TestCase("https://lazer-api.rinarii.de", "https://osu.reprise.pw")]
        [TestCase("https://lazer-api.rinarii.xyz/", "https://osu.reprise.pw/")]
        [TestCase("Lazer-API.Rinarii.DE", "osu.reprise.pw")]
        [TestCase("rinarii.de", "osu.reprise.pw")]
        [TestCase("rinarii.xyz", "osu.reprise.pw")]
        public void TestSavedOldDomainIsMovedToReprise(string saved, string expected)
        {
            Assert.That(loadCustomApiUrl(saved), Is.EqualTo(expected));
        }

        [TestCase("osu.reprise.pw")]
        [TestCase("https://osu.reprise.pw")]
        [TestCase("my-own-server.example.com")]
        [TestCase("http://localhost:8080")]
        public void TestOtherUrlsAreLeftAlone(string saved)
        {
            Assert.That(loadCustomApiUrl(saved), Is.EqualTo(saved));
        }

        [TestCase("notrinarii.de")]
        [TestCase("rinarii.debug.example.com")]
        [TestCase("rinarii.xyz.example.com")]
        public void TestOnlyTheActualDomainIsRewritten(string saved)
        {
            // similar looking hosts which are not the old domain must not be touched.
            Assert.That(loadCustomApiUrl(saved), Is.EqualTo(saved));
        }
    }
}
