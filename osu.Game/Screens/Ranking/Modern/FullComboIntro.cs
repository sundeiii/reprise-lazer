// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Modern
{
    /// <summary>
    /// Reprise: the redesign's full combo moment ("Animation Storyboard", revision 10.6), before the results after a full combo: over the
    /// beatmap darkened, "FULL COMBO!" sweeps in (each letter drawn down from the top, a coloured edge leading) and slams into place, then
    /// a star flashes in either side; it then gives way to the results. Space (or a click) skips it.
    /// </summary>
    public partial class FullComboIntro : CompositeDrawable, IKeyBindingHandler<GlobalAction>
    {
        /// <summary>The design's colours for the strokes' leading edges, letter by letter.</summary>
        private static readonly Color4[] stroke_colours =
        {
            Color4Extensions.FromHex("eb4791"), // pink
            Color4Extensions.FromHex("7047eb"), // purple
            Color4Extensions.FromHex("47b4eb"), // blue
            Color4Extensions.FromHex("47eb55"), // green
            Color4Extensions.FromHex("ebc247"), // orange
            Color4Extensions.FromHex("eb4747"), // red
        };

        private const string text = "FULL COMBO!";

        // When each part happens (ms from the start); all of it within about a second, as the storyboard asks.
        private const double letter_stagger = 25;
        private const double letter_sweep = 180;
        private const double white_lag = 45;
        private const double slam_at = 300;
        private const double slam_duration = 120;
        private const double stars_at = slam_at + slam_duration;
        private const double stars_duration = 220;
        private const double hold = 260;
        private const double fade_out = 200;

        // The design's centre line for the text and stars: 33 below the middle of its 1080.
        private const float centre_line = 33;

        /// <summary>How long it runs, start to end.</summary>
        public const double DURATION = stars_at + stars_duration + hold + fade_out;

        /// <summary>Whether a play's a full combo (passed, its combo the most the beatmap gives).</summary>
        public static bool IsFullCombo(ScoreInfo score)
        {
            int max = score.GetMaximumAchievableCombo();
            return score.Passed && score.Rank != ScoreRank.F && max > 0 && score.MaxCombo >= max && !(score.Statistics.TryGetValue(HitResult.Miss, out int misses) && misses > 0);
        }

        /// <summary>Called once, when it's over (or skipped): the results take over then.</summary>
        public Action? Finished;

        private bool finished;

        // The game's own sounds: a wind-up as it sweeps in (not the results' score swoosh), the section pass as it slams down, the badge's dink as the stars flash.
        private Sample? sweepSample;
        private Sample? slamSample;
        private Sample? starsSample;

        private Container textContainer = null!;
        private Sprite[] stars = null!;
        private LetterSweep[] letters = null!;

        public FullComboIntro()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures, ISampleStore samples)
        {
            sweepSample = samples.Get(@"DailyChallenge/beatmap-windup");
            slamSample = samples.Get(@"Gameplay/sectionpass");
            starsSample = samples.Get(@"Results/badge-dink-max");

            var starTexture = textures.Get(@"Results/full-combo-star");

            letters = text.Select((c, i) => new LetterSweep(c, stroke_colours[i % stroke_colours.Length])).ToArray();

            // Either side, 160 across, as the design has them (10.7% in from each edge).
            stars = new[]
            {
                new Sprite { Texture = starTexture, Anchor = Anchor.CentreLeft, X = 205 + 80 },
                new Sprite { Texture = starTexture, Anchor = Anchor.CentreRight, X = -(205 + 80) },
            };

            InternalChildren = new Drawable[]
            {
                // The design's 75% black over the beatmap's background (with the results' own 25% under it).
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 2 / 3f,
                },
                new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(1920, 1080),
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Children = new Drawable[]
                    {
                        textContainer = new Container
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            // On the stars' centre line, as in the design (its text sits 33 below the middle, level with them).
                            Y = centre_line,
                            Child = new FillFlowContainer<LetterSweep>
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                // The design's tracking (60.6 at 75.8).
                                Spacing = new Vector2(60.6f, 0),
                                Children = letters,
                            },
                        },
                        stars[0],
                        stars[1],
                    }
                },
            };

            foreach (var star in stars)
            {
                star.Origin = Anchor.Centre;
                star.Size = new Vector2(169);
                star.Y = centre_line;
                star.Alpha = 0;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            this.FadeInFromZero(80);

            sweepSample?.Play();
            Scheduler.AddDelayed(() =>
            {
                if (!finished)
                    slamSample?.Play();
            }, slam_at);
            Scheduler.AddDelayed(() =>
            {
                if (!finished)
                    starsSample?.Play();
            }, stars_at);

            // The sweep: each letter drawn down from the top, a little after the one before it.
            for (int i = 0; i < letters.Length; i++)
                letters[i].Sweep(i * letter_stagger);

            // ...while the whole of it comes down onto the screen, slamming into its size.
            textContainer.ScaleTo(1.35f).ScaleTo(1, slam_at, Easing.InQuad)
                         .Then().ScaleTo(1.04f, slam_duration / 2, Easing.OutQuad)
                         .Then().ScaleTo(1, slam_duration / 2, Easing.InQuad);

            // The stars flash in once the text's landed, slamming into their size.
            foreach (var star in stars)
            {
                star.Delay(stars_at).FadeIn(stars_duration / 3).ScaleTo(1.8f).ScaleTo(1, stars_duration, Easing.OutQuint);
                star.Delay(stars_at).FlashColour(Color4.White, stars_duration);
            }

            this.Delay(DURATION - fade_out).FadeOut(fade_out, Easing.OutQuad).OnComplete(_ => finish());
        }

        /// <summary>Ends it now (the results take over).</summary>
        public void Skip()
        {
            if (finished)
                return;

            ClearTransforms();
            this.FadeOut(120, Easing.OutQuad);
            finish();
        }

        private void finish()
        {
            if (finished)
                return;

            finished = true;
            Finished?.Invoke();
        }

        // Space (the game's "select"), as the storyboard says, before the results underneath take it.
        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (finished || e.Repeat || e.Action != GlobalAction.Select)
                return false;

            Skip();
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (finished)
                return false;

            Skip();
            return true;
        }

        // Blocks the results underneath while it's on.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => !finished;

        /// <summary>
        /// One letter, drawn down from the top as the design's strokes do: its coloured edge first, the white following just behind.
        /// </summary>
        private partial class LetterSweep : CompositeDrawable
        {
            private readonly char character;
            private readonly Color4 colour;

            private Container colourReveal = null!;
            private Container whiteReveal = null!;

            public LetterSweep(char character, Color4 colour)
            {
                this.character = character;
                this.colour = colour;

                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                OsuSpriteText letter(Color4 c) => new OsuSpriteText
                {
                    Text = character.ToString(),
                    Font = OsuFont.Torus.With(size: 75.8f, weight: FontWeight.Bold),
                    Colour = c,
                };

                var sizing = letter(Color4.Transparent);

                InternalChildren = new Drawable[]
                {
                    // Gives it its size (the letter's own); the reveals below are as big.
                    sizing,
                    colourReveal = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Height = 0,
                        Masking = true,
                        Child = letter(colour),
                    },
                    whiteReveal = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Height = 0,
                        Masking = true,
                        Child = letter(Color4.White),
                    },
                };
            }

            public void Sweep(double delay)
            {
                colourReveal.Delay(delay).ResizeHeightTo(1, letter_sweep, Easing.OutQuad)
                            .Then().FadeOut(white_lag * 2);
                whiteReveal.Delay(delay + white_lag).ResizeHeightTo(1, letter_sweep, Easing.OutQuad);
            }
        }
    }
}
