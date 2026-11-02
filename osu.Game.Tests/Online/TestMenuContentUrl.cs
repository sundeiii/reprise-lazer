// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online.API.Requests;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class TestMenuContentUrl
    {
        [TestCase("https://osu.reprise.pw", "https://assets.reprise.pw/menu-content.json")]
        [TestCase("https://osu.ppy.sh", "https://assets.ppy.sh/menu-content.json")]
        [TestCase("http://osu.localtest.me", "http://assets.localtest.me/menu-content.json")]
        [TestCase("https://dev.ppy.sh", "https://assets.ppy.sh/menu-content.json")]
        [TestCase("http://localhost", "https://assets.ppy.sh/menu-content.json")]
        [TestCase(null, "https://assets.ppy.sh/menu-content.json")]
        public void TestUrlFor(string? websiteUrl, string expected) => Assert.That(GetMenuContentRequest.UrlFor(websiteUrl), Is.EqualTo(expected));
    }
}
