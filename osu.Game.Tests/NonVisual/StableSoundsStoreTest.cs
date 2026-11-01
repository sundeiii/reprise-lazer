// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Audio;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>osu!stable's typing, chat and multiplayer sounds (<see cref="StableSoundsStore"/>): the game's names answered with stable's files, only when picked.</summary>
    [TestFixture]
    public class StableSoundsStoreTest
    {
        [TearDown]
        public void TearDown() => new StableSoundsStore(false).Dispose();

        [Test]
        public void TestStableSounds()
        {
            using var store = new StableSoundsStore(true);

            for (int i = 1; i <= 4; i++)
                Assert.That(store.Get($@"Samples/Keyboard/key-press-{i}.wav"), Is.Not.Null);

            // Any extension the game tries.
            Assert.That(store.Get(@"Samples/Keyboard/key-delete.mp3"), Is.Not.Null);
            Assert.That(store.Get(@"Samples/Keyboard/key-movement.wav"), Is.Not.Null);
            Assert.That(store.Get(@"Samples/Keyboard/key-confirm.wav"), Is.Not.Null);
            Assert.That(store.Get(@"Samples/Keyboard/key-caps.wav"), Is.Not.Null);
            Assert.That(store.Get(@"Samples/UI/notification-mention.wav"), Is.Not.Null);
            Assert.That(store.Get(@"Samples/Multiplayer/player-joined.wav"), Is.Not.Null);
            Assert.That(store.Get(@"Samples/Multiplayer/player-left.wav"), Is.Not.Null);
            Assert.That(StableSoundsStore.Enabled, Is.True);
        }

        [Test]
        public void TestLeavesTheRestToTheGame()
        {
            using var store = new StableSoundsStore(true);

            // Stable has no sound for these.
            Assert.That(store.Get(@"Samples/Keyboard/key-invalid.wav"), Is.Null);
            Assert.That(store.Get(@"Samples/Multiplayer/player-kicked.wav"), Is.Null);
            Assert.That(store.Get(@"Samples/UI/notification-default.wav"), Is.Null);
            // Not a sample at all.
            Assert.That(store.Get(@"Textures/Menu/logo.png"), Is.Null);
        }

        [Test]
        public void TestStableNamesForSkins()
        {
            using var store = new StableSoundsStore(true);

            // The names a stable skin has its own of.
            Assert.That(StableSoundsStore.StableName(@"Keyboard/key-press-3"), Is.EqualTo(@"key-press-3"));
            Assert.That(StableSoundsStore.StableName(@"UI/notification-mention"), Is.EqualTo(@"match-start"));
            Assert.That(StableSoundsStore.StableName(@"Multiplayer/player-joined"), Is.EqualTo(@"match-join"));
            Assert.That(StableSoundsStore.StableName(@"Keyboard/key-invalid"), Is.Null);
        }

        [Test]
        public void TestOffAnswersNothing()
        {
            using var store = new StableSoundsStore(false);

            Assert.That(store.Get(@"Samples/Keyboard/key-press-1.wav"), Is.Null);
            Assert.That(store.Get(@"Samples/Multiplayer/player-joined.wav"), Is.Null);
            Assert.That(StableSoundsStore.Enabled, Is.False);
        }
    }
}
