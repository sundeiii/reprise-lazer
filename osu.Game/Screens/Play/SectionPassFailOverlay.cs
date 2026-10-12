// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Audio;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// osu!stable's "section pass" / "section fail" in breaks (behind <see cref="OsuSetting.SectionPassFail"/>): the skin's
    /// section-pass or section-fail blinking in the middle of the screen with its sectionpass or sectionfail sound in breaks (stable's
    /// only in those longer than 2.88 seconds), instead of the game's break display; a pass with at least half health when the break starts. Timings from the 2015 client. Skins without
    /// them (the game's own) show the classic skin's.
    /// </summary>
    public partial class SectionPassFailOverlay : CompositeDrawable
    {
        // Stable's: at a break's middle or this long before its end, whichever is earlier (stable only showed it in breaks longer than
        // this; shorter ones show it at their start).
        private const double time_before_end = 2880;

        // Stable's sounds start this long after the time above (its images blink from then).
        private const double pass_delay = 20;
        private const double fail_delay = 130;

        private readonly IReadOnlyList<BreakPeriod> breaks;
        private readonly HealthProcessor healthProcessor;

        private Sprite pass = null!;
        private Sprite fail = null!;
        private ISample? passSample;
        private ISample? failSample;

        private Bindable<bool> enabled = null!;
        private Bindable<float> uiScale = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private BreakPeriod? currentBreak;
        private double? soundTime;
        private bool passed;

        /// <summary>
        /// The game's own break display (countdown, progress, arrows): hidden while this is on, as stable has none.
        /// </summary>
        public Drawable? ReplacedBreakDisplay { get; init; }

        /// <summary>
        /// The play's grade, shown next to the skin's song progress pie in breaks as stable does.
        /// </summary>
        public IBindable<ScoreRank>? Rank { get; init; }

        /// <summary>
        /// The HUD's song progress pie (the classic skin's), which the grade goes beside; none, none shown.
        /// </summary>
        public Func<Drawable?>? FindProgressPie { get; init; }

        // Stable's grade beside its pie: 28 to its side (its 640x480, so 1.6 times in the 1024x768 layout).
        private static readonly Vector2 grade_offset = new Vector2(-28 * 1.6f, 0);

        // Stable shows the grade in breaks long enough for it to fade in and out.
        private const double min_grade_break = 2000;

        private Container layout = null!;
        private Sprite grade = null!;
        private Sprite gradeGlow = null!;

        public SectionPassFailOverlay(IReadOnlyList<BreakPeriod> breaks, HealthProcessor healthProcessor)
        {
            this.breaks = breaks;
            this.healthProcessor = healthProcessor;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            enabled = config.GetBindable<bool>(OsuSetting.SectionPassFail);
            uiScale = config.GetBindable<float>(OsuSetting.UIScale);

            // Stable's 1024x768 layout, the images at their sizes in it.
            InternalChild = layout = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(1024, 768),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Children = new Drawable[]
                {
                    pass = new Sprite { Anchor = Anchor.Centre, Origin = Anchor.Centre, Alpha = 0 },
                    fail = new Sprite { Anchor = Anchor.Centre, Origin = Anchor.Centre, Alpha = 0 },
                    gradeGlow = new Sprite { Origin = Anchor.Centre, Alpha = 0, Blending = BlendingParameters.Additive },
                    grade = new Sprite { Origin = Anchor.Centre, Alpha = 0 },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            loadSkin();

            enabled.BindValueChanged(e =>
            {
                if (ReplacedBreakDisplay != null)
                    ReplacedBreakDisplay.Alpha = e.NewValue ? 0 : 1;
            }, true);
        }

        private void onSkinChanged() => Schedule(loadSkin);

        // The skin's, else (the game's own skins have none) the classic skin's, as stable's default.
        private void loadSkin()
        {
            var classic = skinManager.DefaultClassicSkin;

            pass.Texture = skin.GetTexture(@"section-pass") ?? classic.GetTexture(@"section-pass");
            fail.Texture = skin.GetTexture(@"section-fail") ?? classic.GetTexture(@"section-fail");
            passSample = skin.GetSample(new SampleInfo(@"sectionpass")) ?? classic.GetSample(new SampleInfo(@"sectionpass"));
            failSample = skin.GetSample(new SampleInfo(@"sectionfail")) ?? classic.GetSample(new SampleInfo(@"sectionfail"));
        }

        protected override void Update()
        {
            base.Update();

            // With the HUD, in stable's 16:9 area.
            StableHudArea.Apply(this, true, uiScale.Value);

            double time = Time.Current;
            var breakNow = breaks.FirstOrDefault(b => b.HasEffect && time >= b.StartTime && time < b.EndTime);

            if (breakNow != currentBreak)
            {
                currentBreak = breakNow;

                if (breakNow != null && enabled.Value)
                {
                    show(breakNow);
                    showGrade(breakNow);
                }
                else
                    hide();
            }

            if (soundTime is double at && time >= at)
            {
                // Not when seeking far past it (a replay).
                if (time - at < 200)
                    (passed ? passSample : failSample)?.Play();

                soundTime = null;
            }

            // Beside the pie wherever the HUD has it.
            if (grade.Alpha > 0 || gradeGlow.Alpha > 0)
            {
                if (FindProgressPie?.Invoke() is Drawable pie && pie.IsPresent)
                    grade.Position = gradeGlow.Position = layout.ToLocalSpace(pie.ScreenSpaceDrawQuad.Centre) + grade_offset;
                else
                    grade.Alpha = gradeGlow.Alpha = 0;
            }
        }

        private void showGrade(BreakPeriod breakPeriod)
        {
            if (Rank == null || Rank.Value == ScoreRank.F || breakPeriod.Duration < min_grade_break)
                return;

            var texture = skin.GetTexture($@"ranking-{Rank.Value}-small") ?? skinManager.DefaultClassicSkin.GetTexture($@"ranking-{Rank.Value}-small");

            if (texture == null)
                return;

            grade.Texture = gradeGlow.Texture = texture;

            // Stable's: in 0.3 seconds from 0.4 after the break starts, out by 0.7 before it ends; a copy growing and fading as it comes.
            using (grade.BeginAbsoluteSequence(breakPeriod.StartTime))
                grade.ScaleTo(0.8f).Delay(400).FadeIn(300).Then().Delay(breakPeriod.Duration - 1700).FadeOut(300);

            using (gradeGlow.BeginAbsoluteSequence(breakPeriod.StartTime))
            {
                gradeGlow.ScaleTo(0.8f).Delay(400).FadeIn()
                         .ScaleTo(1.3f, 1200).FadeOut(1200);
            }
        }

        private void show(BreakPeriod breakPeriod)
        {
            hide();

            // As stable, decided when the break starts.
            passed = healthProcessor.Health.Value >= 0.5;

            double at = breakPeriod.Duration / 2 > time_before_end
                ? breakPeriod.StartTime + breakPeriod.Duration / 2
                : Math.Max(breakPeriod.StartTime, breakPeriod.EndTime - time_before_end);

            if (passed)
            {
                soundTime = at + pass_delay;

                using (pass.BeginAbsoluteSequence(at))
                {
                    pass.Delay(20).FadeIn()
                        .Delay(80).FadeOut()
                        .Delay(60).FadeIn()
                        .Delay(70).FadeOut()
                        .Delay(50).FadeIn()
                        .Delay(1000).FadeOut(200);
                }
            }
            else
            {
                soundTime = at + fail_delay;

                using (fail.BeginAbsoluteSequence(at))
                {
                    fail.Delay(130).FadeIn()
                        .Delay(100).FadeOut()
                        .Delay(50).FadeIn()
                        .Delay(1000).FadeOut(200);
                }
            }
        }

        private void hide()
        {
            soundTime = null;

            pass.ClearTransforms();
            pass.Alpha = 0;
            fail.ClearTransforms();
            fail.Alpha = 0;
            grade.ClearTransforms();
            grade.Alpha = 0;
            gradeGlow.ClearTransforms();
            gradeGlow.Alpha = 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }
    }
}
