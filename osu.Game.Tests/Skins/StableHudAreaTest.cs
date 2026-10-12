// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class StableHudAreaTest
    {
        [Test]
        public void TestWideWindowHasMarginsAtTheSides() => Assert.That(StableHudArea.Fit(new Vector2(1920, 1009)), Is.EqualTo(new Vector2(1009 * 16f / 9, 1009)));

        [Test]
        public void TestFourByThreeIsWhole() => Assert.That(StableHudArea.Fit(new Vector2(1280, 960)), Is.EqualTo(new Vector2(1280, 960)));

        [Test]
        public void TestSixteenByNineFills() => Assert.That(StableHudArea.Fit(new Vector2(1920, 1080)), Is.EqualTo(new Vector2(1920, 1080)));
    }
}
