// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Screens.RankingV2.Legacy;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>Reprise: the stable results screen's judgement images, picked as stable picks them.</summary>
    [TestFixture]
    public class LegacyRankingElementTextureTest
    {
        private static readonly DummyRenderer renderer = new DummyRenderer();

        [Test]
        public void TestPlainImageOverFirstFrame()
        {
            // A skin hiding 300s in gameplay with a blank hit300-0, keeping hit300 for the results.
            var skin = new FakeSkin("hit300", "hit300-0");
            Assert.That(get(skin), Is.SameAs(skin.Textures["hit300"]));
        }

        [Test]
        public void TestFirstFrameWhenThatsAllThereIs()
        {
            var skin = new FakeSkin("hit300-0");
            Assert.That(get(skin), Is.SameAs(skin.Textures["hit300-0"]));
        }

        [Test]
        public void TestPlayersSkinOverDefault()
        {
            // The player's skin's frame, not the default skin's plain image underneath.
            var players = new FakeSkin("hit300-0");
            var fallback = new FakeSkin("hit300");
            Assert.That(get(players, fallback), Is.SameAs(players.Textures["hit300-0"]));
        }

        private static Texture? get(params ISkin[] skins) => LegacyRankingPanel.LegacyRankingElement.GetElementTexture(new FakeSource(skins), "hit300");

        private class FakeSkin : ISkin
        {
            public readonly Dictionary<string, Texture> Textures;

            public FakeSkin(params string[] names)
            {
                Textures = names.ToDictionary(n => n, _ => renderer.CreateTexture(1, 1));
            }

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;
            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => Textures.GetValueOrDefault(componentName);
            public ISample? GetSample(ISampleInfo sampleInfo) => null;
            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) where TLookup : notnull where TValue : notnull => null;
        }

        private class FakeSource : ISkinSource
        {
            private readonly ISkin[] skins;

            public FakeSource(ISkin[] skins)
            {
                this.skins = skins;
            }

            public event Action? SourceChanged
            {
                add { }
                remove { }
            }

            public ISkin? FindProvider(Func<ISkin, bool> lookupFunction) => skins.FirstOrDefault(lookupFunction);
            public IEnumerable<ISkin> AllSources => skins;

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) =>
                skins.Select(s => s.GetTexture(componentName, wrapModeS, wrapModeT)).FirstOrDefault(t => t != null);

            public ISample? GetSample(ISampleInfo sampleInfo) => null;
            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) where TLookup : notnull where TValue : notnull => null;
        }
    }
}
