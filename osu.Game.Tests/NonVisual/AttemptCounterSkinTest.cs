// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The attempt counter is meant to be part of any skin, so it has to be something the skin editor can list, place and save.
    /// </summary>
    [TestFixture]
    public class AttemptCounterSkinTest
    {
        [Test]
        public void TestListedAmongTheComponentsASkinCanUse()
        {
            Assert.That(SerialisedDrawableInfo.GetAllAvailableDrawables(), Does.Contain(typeof(AttemptCounter)));
        }

        [Test]
        public void TestCanBeCreatedWithoutArgumentsAndEdited()
        {
            // the skin editor and saved skins create components with nothing but their type.
            var instance = (ISerialisableDrawable)Activator.CreateInstance(typeof(AttemptCounter))!;

            Assert.That(instance.IsEditable, Is.True);
        }

        [Test]
        public void TestSettingsSurviveBeingSavedInASkin()
        {
            var original = new AttemptCounter
            {
                Position = new osuTK.Vector2(30, 40),
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                UsesFixedAnchor = true,
            };

            original.ShowBestAccuracy.Value = false;

            var info = new SerialisedDrawableInfo(original);
            var restored = (AttemptCounter)info.CreateInstance();

            Assert.That(info.Type, Is.EqualTo(typeof(AttemptCounter)));
            Assert.That(info.Settings.Keys, Does.Contain("show_best_accuracy"));

            Assert.That(restored.ShowBestAccuracy.Value, Is.False);
            Assert.That(restored.Position, Is.EqualTo(original.Position));
            Assert.That(restored.Anchor, Is.EqualTo(Anchor.TopRight));
            Assert.That(restored.UsesFixedAnchor, Is.True);
        }

        [Test]
        public void TestDefaultsToShowingBestAccuracy()
        {
            Assert.That(new AttemptCounter().ShowBestAccuracy.Value, Is.True);
            Assert.That(new SerialisedDrawableInfo(new AttemptCounter()).Settings.Keys.Contains("show_best_accuracy"));
        }
    }
}
