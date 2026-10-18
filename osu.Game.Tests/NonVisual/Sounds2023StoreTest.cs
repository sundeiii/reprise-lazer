// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Audio;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>The 2023 sound effects (<see cref="Sounds2023Store"/>): today's names answered with 2023's files, only when picked.</summary>
    [TestFixture]
    public class Sounds2023StoreTest
    {
        [TearDown]
        public void TearDown() => new Sounds2023Store(false).Dispose();

        [Test]
        public void TestSameNameAndRenamedSounds()
        {
            using var store = new Sounds2023Store(true);

            // Same name then and now.
            Assert.That(store.Get(@"Samples/UI/overlay-pop-in.wav"), Is.Not.Null);
            // Any extension the game tries.
            Assert.That(store.Get(@"Samples/UI/overlay-pop-in.mp3"), Is.Not.Null);
            // Today's name for 2023's notification sound: the same file.
            Assert.That(store.Get(@"Samples/UI/notification-default.wav"), Is.EqualTo(store.Get(@"Samples/UI/notification-pop-in.wav")));
            Assert.That(store.Get(@"Samples/Menu/back-to-top.wav"), Is.Not.Null);
            // The main menu's 2023 clicks.
            Assert.That(store.Get(@"Samples/Menu/button-solo-select.wav"), Is.Not.Null);
            Assert.That(Sounds2023Store.Enabled, Is.True);
        }

        [Test]
        public void TestLeavesTheRestToTheGame()
        {
            using var store = new Sounds2023Store(true);

            // A sound 2023 had the same as now (and one it didn't have): the game's own.
            Assert.That(store.Get(@"Samples/UI/default-hover.wav"), Is.Null);
            Assert.That(store.Get(@"Samples/UI/tabselect-select.wav"), Is.Null);
            // Not a sample at all.
            Assert.That(store.Get(@"Textures/Menu/logo.png"), Is.Null);
        }

        [Test]
        public void TestOffAnswersNothing()
        {
            using var store = new Sounds2023Store(false);

            Assert.That(store.Get(@"Samples/UI/overlay-pop-in.wav"), Is.Null);
            Assert.That(store.Get(@"Samples/UI/notification-default.wav"), Is.Null);
            Assert.That(Sounds2023Store.Enabled, Is.False);
        }
    }
}
