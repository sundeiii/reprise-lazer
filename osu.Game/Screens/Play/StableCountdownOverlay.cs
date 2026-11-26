// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// osu!stable's countdown before the first object ("ready", 3, 2, 1, "go", with the skin's readys, count3s, count2s, count1s and gos),
    /// on maps that have one, when there's room for it, as stable's Player.InitializeCountdown times and draws it.
    /// </summary>
    public partial class StableCountdownOverlay : CompositeDrawable
    {
        /// <summary>
        /// When "go" comes and how long a count is, as stable works them out; null when stable shows no countdown for the map.
        /// </summary>
        public static (double Go, double Beat)? Timing(IBeatmap beatmap, CountdownType countdown, int countdownOffset, int rulesetId)
        {
            // Stable has none in taiko, and none for a map without one or without objects.
            if (countdown == CountdownType.None || rulesetId == 1 || beatmap.HitObjects.Count == 0)
                return null;

            double firstCircle = beatmap.HitObjects.First().StartTime;

            var timing = beatmap.ControlPointInfo.TimingPointAt(firstCircle);
            double offset = timing.Time;
            double beatLengthOriginal = beatLengthAt(beatmap, firstCircle);
            if (beatLengthOriginal <= 0) beatLengthOriginal = beatLengthAt(beatmap, 0);
            if (beatLengthOriginal <= 0) return null;

            double goTime = offset - 5;
            double beatLength = beatLengthOriginal;

            // Stable's: too fast, twice as long.
            if (beatLength <= 333)
                beatLength *= 2;

            if (countdown == CountdownType.DoubleSpeed)
                beatLength /= 2;
            else if (countdown == CountdownType.HalfSpeed)
                beatLength *= 2;

            firstCircle -= beatLength * countdownOffset;

            // Back until a good place, then forward a beat (half a beat off the strong beats) at a time to the last before the first object.
            if (goTime >= firstCircle)
            {
                while (goTime > firstCircle - beatLength)
                    goTime -= beatLength;
            }

            int divisor = 1;

            while (goTime < firstCircle - beatLength / divisor)
            {
                goTime += beatLength;
                double beat = (float)(goTime - offset) / beatLengthOriginal % 4;
                divisor = beat > 1.5 && beat < 3.5 ? 2 : 1;
            }

            goTime -= beatLength;

            // Only with room for its four counts after the start.
            if (goTime - 4 * beatLength <= 0)
                return null;

            // Stable works in whole milliseconds here.
            return ((int)goTime, (int)beatLength);
        }

        // Stable's BeatLengthAt: the timing point's beat, times an inherited point's multiplier (1 / slider velocity) after it.
        private static double beatLengthAt(IBeatmap beatmap, double time)
        {
            var timing = beatmap.ControlPointInfo.TimingPointAt(time);
            double beatLength = timing.BeatLength;

            if (beatmap.ControlPointInfo is LegacyControlPointInfo legacy)
            {
                var difficulty = legacy.DifficultyPointAt(time);
                if (difficulty.Time >= timing.Time && difficulty.SliderVelocity > 0 && difficulty.SliderVelocity != 1)
                    beatLength *= Math.Clamp(1 / difficulty.SliderVelocity, 0.1, 10);
            }

            return beatLength;
        }

        private readonly double go;
        private readonly int beat;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private ISamplePlaybackDisabler? samplePlaybackDisabler { get; set; }

        private Sprite ready = null!, count3 = null!, count2 = null!, count1 = null!, goSprite = null!;

        private (double Time, string Name)[] sounds = Array.Empty<(double, string)>();
        private int nextSound;

        public StableCountdownOverlay(double go, double beat)
        {
            this.go = go;
            this.beat = (int)beat;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Stable's 1024x768 layout, centred (its widescreen-aware layer below the objects).
            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(1024, 768),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Children = new Drawable[]
                {
                    ready = createSprite(),
                    count3 = createSprite(),
                    count2 = createSprite(),
                    count1 = createSprite(),
                    goSprite = createSprite(),
                },
            };
        }

        private static Sprite createSprite() => new KeptSprite
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Alpha = 0,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            setUp();
        }

        private void onSkinChanged() => Schedule(setUp);

        private Texture? texture(string name) => skin.GetTexture(name) ?? skinManager.DefaultClassicSkin.GetTexture(name);

        private ISample? sample(string name) => skin.GetSample(new SampleInfo(name)) ?? skinManager.DefaultClassicSkin.GetSample(new SampleInfo(name));

        private static void setTexture(Sprite sprite, Texture? texture)
        {
            sprite.Texture = texture;
            sprite.Size = texture == null ? Vector2.Zero : new Vector2(texture.DisplayWidth, texture.DisplayHeight);
        }

        private void setUp()
        {
            foreach (var sprite in new[] { ready, count3, count2, count1, goSprite })
            {
                sprite.ClearTransforms();
                sprite.Alpha = 0;
                sprite.Scale = Vector2.One;
                sprite.Rotation = 0;
            }

            setTexture(ready, texture(@"ready"));
            setTexture(count3, texture(@"count3"));
            setTexture(count2, texture(@"count2"));
            setTexture(count1, texture(@"count1"));
            setTexture(goSprite, texture(@"go"));

            // Stable's new layout: skins of version 2 and up, and the default.
            decimal version = skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value ?? 2;
            bool newLayout = version > 1 || skin.GetTexture(@"count1") == null;

            int g = (int)go;
            int b = beat;

            // Stable's old layout puts 3 and 2 either side of the middle, a sixth and a bit of 1's width out (its 640x480, so 1.6 times here).
            if (!newLayout)
            {
                float side = count1.Size.X / 6.8f * 1.6f;
                count3.Origin = Anchor.CentreRight;
                count3.X = -side;
                count2.Origin = Anchor.CentreLeft;
                count2.X = side;
            }
            else
            {
                count3.Origin = count2.Origin = Anchor.Centre;
                count3.X = count2.X = 0;
            }

            // "ready": in over a beat from 6 before "go" (from 3 times as big and turned, with the old layout), out over a beat from 4 before.
            using (ready.BeginAbsoluteSequence(g - 6 * b))
            {
                ready.FadeTo(0).FadeIn(b);

                if (!newLayout)
                    ready.ScaleTo(3).ScaleTo(1, b).RotateTo(-0.4f * 180 / MathF.PI).RotateTo(0, b);
            }

            using (ready.BeginAbsoluteSequence(g - 4 * b))
                ready.FadeOut(b).ScaleTo(newLayout ? 1.2f : 1.8f, b);

            countIn(count3, g, b, 3, newLayout);
            countIn(count2, g, b, 2, newLayout);
            countIn(count1, g, b, 1, newLayout);

            // "go": in by "go" (growing from 0.2 and turning with the old layout, shrinking from 1.4 with the new), out from 0.3 of a beat after.
            if (newLayout)
            {
                using (goSprite.BeginAbsoluteSequence(g - (int)(0.6 * b)))
                    goSprite.ScaleTo(1.4f).ScaleTo(1, (int)(0.6 * b) + (int)(0.2 * b));

                using (goSprite.BeginAbsoluteSequence(g - (int)(0.2 * b)))
                    goSprite.FadeTo(0).FadeIn((int)(0.2 * b));
            }
            else
            {
                using (goSprite.BeginAbsoluteSequence(g - (int)(0.6 * b)))
                {
                    goSprite.FadeTo(0).FadeIn((int)(0.6 * b));
                    goSprite.RotateTo(-4 * 180 / MathF.PI).RotateTo(0, (int)(0.6 * b) + (int)(0.2 * b), Easing.Out);
                    goSprite.ScaleTo(0.2f).ScaleTo(1, (int)(0.6 * b) + (int)(0.2 * b), Easing.Out);
                }
            }

            using (goSprite.BeginAbsoluteSequence(g + (int)(0.3 * b)))
                goSprite.FadeOut(b - (int)(0.3 * b));

            // Stable's sounds: readys, then count3s, count2s, count1s and gos on the counts (or "count" four times for a skin without them).
            bool separate = sample(@"count3s") != null;

            sounds = new[]
            {
                ((double)(int)(g - 5.9 * b), @"readys"),
                (g - 3 * b, separate ? @"count3s" : @"count"),
                (g - 2 * b, separate ? @"count2s" : @"count"),
                (g - 1 * b, separate ? @"count1s" : @"count"),
                (g, separate ? @"gos" : @"count"),
            };

            nextSound = sounds.Count(s => s.Time < Time.Current);
        }

        // Stable's: each count in over 0.2 of a beat up to its beat (shrinking from 1.4 with the new layout); out over the next 0.2 with the new
        // layout, all three together around "go" with the old.
        private static void countIn(Sprite sprite, int g, int b, int n, bool newLayout)
        {
            int inStart = g - (int)((n + 0.2) * b);
            int inEnd = g - n * b;

            using (sprite.BeginAbsoluteSequence(inStart))
            {
                sprite.FadeTo(0).FadeIn(inEnd - inStart);

                if (newLayout)
                    sprite.ScaleTo(1.4f).ScaleTo(1, inEnd - inStart);
            }

            int outStart = newLayout ? g - (int)((n - 1 + 0.2) * b) : g - (int)(0.2 * b);
            int outEnd = newLayout ? g - (int)((n - 1) * b) : g + (int)(0.2 * b);

            using (sprite.BeginAbsoluteSequence(outStart))
                sprite.FadeOut(outEnd - outStart);
        }

        /// <summary>How many of its sounds have played (for tests).</summary>
        public int SoundsPlayed { get; private set; }

        protected override void Update()
        {
            base.Update();

            double time = Time.Current;

            // Back before a sound (a replay seeked back): it plays again when reached.
            while (nextSound > 0 && time < sounds[nextSound - 1].Time)
                nextSound--;

            bool held = samplePlaybackDisabler?.SamplePlaybackDisabled.Value == true;

            while (nextSound < sounds.Length && time >= sounds[nextSound].Time)
            {
                // Not while paused or catching up; not one seeked far past (a skip).
                if (held && time - sounds[nextSound].Time < 200)
                    break;

                if (time - sounds[nextSound].Time < 200)
                {
                    sample(sounds[nextSound].Name)?.Play();
                    SoundsPlayed++;
                }

                nextSound++;
            }
        }

        // Its transforms kept once done, so seeking back (a replay) shows them again.
        private partial class KeptSprite : Sprite
        {
            public override bool RemoveCompletedTransforms => false;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }
    }
}
