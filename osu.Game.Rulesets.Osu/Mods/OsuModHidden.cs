// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModHidden : ModHidden, IHidesApproachCircles, IUpdatableByPlayfield
    {
        [SettingSource(
            "Only fade approach circles",
            "The main object body will not fade when enabled.")]
        public Bindable<bool> OnlyFadeApproachCircles { get; } =
            new BindableBool();

        public override LocalisableString Description =>
            @"Play with no approach circles and fading circles/sliders.";

        public override Type[] IncompatibleMods => new[]
        {
            typeof(IRequiresApproachCircles),
            typeof(OsuModSpinIn),
            typeof(OsuModDepth),
            typeof(OsuModFreezeFrame)
        };

        public const double FADE_IN_DURATION_MULTIPLIER = 0.4;
        public const double FADE_OUT_DURATION_MULTIPLIER = 0.3;

        private bool previousRemoveHidden;
        private bool hasInitialisedRemoveHiddenState;

        protected override bool IsFirstAdjustableObject(HitObject hitObject) =>
            !(hitObject is Spinner || hitObject is SpinnerTick);

        public override void ApplyToBeatmap(IBeatmap beatmap)
        {
            /*
             * Beatmap timing changes happen before drawable objects are created.
             *
             * When Remove Hidden is already enabled before entering gameplay,
             * skip Hidden's timing adjustments completely.
             *
             * When toggled during gameplay, Update() handles existing drawable
             * objects directly.
             */
            if (GameplayOverrides.RemoveHidden)
                return;

            base.ApplyToBeatmap(beatmap);

            foreach (var obj in beatmap.HitObjects.OfType<OsuHitObject>())
                applyFadeInAdjustment(obj);

            static void applyFadeInAdjustment(OsuHitObject osuObject)
            {
                // Sliders retain their default TimeFadeIn to match Stable
                if (osuObject is not Slider)
                    osuObject.TimeFadeIn = osuObject.TimePreempt * FADE_IN_DURATION_MULTIPLIER;

                foreach (var nested in osuObject.NestedHitObjects.OfType<OsuHitObject>())
                    applyFadeInAdjustment(nested);
            }
        }

        protected override void ApplyIncreasedVisibilityState(
            DrawableHitObject hitObject,
            ArmedState state)
        {
            if (GameplayOverrides.RemoveHidden)
                return;

            applyHiddenState(hitObject, true);
        }

        protected override void ApplyNormalVisibilityState(
            DrawableHitObject hitObject,
            ArmedState state)
        {
            if (GameplayOverrides.RemoveHidden)
                return;

            applyHiddenState(hitObject, false);
        }

        public void Update(Playfield playfield)
        {
            bool removeHidden =
                GameplayOverrides.RemoveHidden;

            if (hasInitialisedRemoveHiddenState &&
                previousRemoveHidden == removeHidden)
            {
                return;
            }

            hasInitialisedRemoveHiddenState = true;
            previousRemoveHidden = removeHidden;

            foreach (DrawableOsuHitObject drawable in
                     playfield.HitObjectContainer.AliveObjects
                              .OfType<DrawableOsuHitObject>())
            {
                refreshHiddenState(drawable, removeHidden);
            }
        }

        private void refreshHiddenState(
            DrawableOsuHitObject drawable,
            bool removeHidden)
        {
            resetHiddenVisuals(drawable);

            if (!removeHidden)
                applyHiddenState(drawable, false);
        }

        private static void resetHiddenVisuals(
            DrawableOsuHitObject drawable)
        {
            switch (drawable)
            {
                case DrawableHitCircle circle:
                    resetHitCircle(circle);
                    break;

                case DrawableSlider slider:
                    resetSlider(slider);
                    break;

                case DrawableSliderTick tick:
                    tick.ClearTransforms();
                    tick.Show();
                    break;

                case DrawableSliderRepeat repeat:
                    repeat.ClearTransforms();
                    repeat.CirclePiece.ClearTransforms();

                    repeat.Show();
                    repeat.CirclePiece.Show();
                    break;

                case DrawableSliderTail tail:
                    tail.ClearTransforms();
                    tail.Show();
                    break;

                case DrawableSpinner spinner:
                    spinner.ClearTransforms();
                    spinner.Show();

                    var approachCircle =
                        (spinner.Body.Drawable as IHasApproachCircle)
                        ?.ApproachCircle;

                    if (approachCircle != null)
                    {
                        approachCircle.ClearTransforms();
                        approachCircle.Show();
                    }

                    break;
            }

            static void resetHitCircle(
                DrawableHitCircle circle)
            {
                circle.ClearTransforms();
                circle.CirclePiece.ClearTransforms();
                circle.ApproachCircle.ClearTransforms();

                circle.Show();
                circle.CirclePiece.Show();
                circle.ApproachCircle.Show();
            }

            static void resetSlider(
                DrawableSlider slider)
            {
                slider.ClearTransforms();
                slider.Body.ClearTransforms();

                slider.Show();
                slider.Body.Show();

                resetHitCircle(slider.HeadCircle);

                if (slider.TailCircle != null)
                {
                    slider.TailCircle.ClearTransforms();
                    slider.TailCircle.Show();
                }
            }
        }

        private void applyHiddenState(
            DrawableHitObject drawableObject,
            bool increaseVisibility)
        {
            if (!(drawableObject is DrawableOsuHitObject drawableOsuObject))
                return;

            OsuHitObject hitObject =
                drawableOsuObject.HitObject;

            (double fadeStartTime, double fadeDuration) =
                getFadeOutParameters(drawableOsuObject);

            /*
             * Process approach circle hiding first, because
             * OnlyFadeApproachCircles returns before body fading.
             */
            if (!increaseVisibility)
            {
                if (drawableObject is DrawableHitCircle circle)
                {
                    using (circle.BeginAbsoluteSequence(
                               hitObject.StartTime -
                               hitObject.TimePreempt))
                    {
                        circle.ApproachCircle.Hide();
                    }
                }
                else if (drawableObject is DrawableSpinner spinner)
                {
                    spinner.Body.OnSkinChanged +=
                        () => hideSpinnerApproachCircle(spinner);

                    hideSpinnerApproachCircle(spinner);
                }
            }

            if (OnlyFadeApproachCircles.Value)
                return;

            switch (drawableObject)
            {
                case DrawableSliderTail:
                    using (drawableObject.BeginAbsoluteSequence(
                               fadeStartTime))
                    {
                        drawableObject.FadeOut(fadeDuration);
                    }

                    break;

                case DrawableSliderRepeat sliderRepeat:
                    using (drawableObject.BeginAbsoluteSequence(
                               fadeStartTime))
                    {
                        sliderRepeat.CirclePiece.FadeOut(
                            fadeDuration);
                    }

                    using (drawableObject.BeginAbsoluteSequence(
                               drawableObject.HitStateUpdateTime))
                    {
                        sliderRepeat.FadeOut();
                    }

                    break;

                case DrawableHitCircle circle:
                    Drawable fadeTarget = increaseVisibility
                        ? circle.CirclePiece
                        : circle;

                    using (drawableObject.BeginAbsoluteSequence(
                               fadeStartTime))
                    {
                        fadeTarget.FadeOut(fadeDuration);
                    }

                    break;

                case DrawableSlider slider:
                    using (slider.BeginAbsoluteSequence(
                               fadeStartTime))
                    {
                        slider.Body.FadeOut(
                            fadeDuration,
                            Easing.Out);
                    }

                    break;

                case DrawableSliderTick sliderTick:
                    using (sliderTick.BeginAbsoluteSequence(
                               fadeStartTime))
                    {
                        sliderTick.FadeOut(fadeDuration);
                    }

                    break;

                case DrawableSpinner spinner:
                    using (spinner.BeginAbsoluteSequence(
                               fadeStartTime))
                    {
                        spinner.FadeOut(fadeDuration);
                    }

                    break;
            }
        }

        private (double fadeStartTime, double fadeDuration)
            getFadeOutParameters(
                DrawableOsuHitObject drawableObject)
        {
            switch (drawableObject)
            {
                case DrawableSliderTail tail:
                    Debug.Assert(tail.Slider != null);
                    return getParameters(
                        tail.Slider.HeadCircle);

                case DrawableSliderRepeat repeat:
                    Debug.Assert(repeat.Slider != null);
                    return getParameters(
                        repeat.Slider.HeadCircle);

                default:
                    return getParameters(
                        drawableObject.HitObject);
            }

            static (double fadeStartTime, double fadeDuration)
                getParameters(OsuHitObject hitObject)
            {
                double fadeOutStartTime =
                    hitObject.StartTime -
                    hitObject.TimePreempt +
                    hitObject.TimeFadeIn;

                double fadeOutDuration =
                    hitObject.TimePreempt *
                    FADE_OUT_DURATION_MULTIPLIER;

                double longFadeDuration =
                    hitObject.GetEndTime() -
                    fadeOutStartTime;

                switch (hitObject)
                {
                    case Slider:
                        return (
                            fadeOutStartTime,
                            longFadeDuration);

                    case SliderTick:
                        double tickFadeOutDuration =
                            Math.Min(
                                hitObject.TimePreempt -
                                DrawableSliderTick.ANIM_DURATION,
                                1000);

                        return (
                            hitObject.StartTime -
                            tickFadeOutDuration,
                            tickFadeOutDuration);

                    case Spinner:
                        return (
                            fadeOutStartTime +
                            longFadeDuration,
                            fadeOutDuration);

                    default:
                        return (
                            fadeOutStartTime,
                            fadeOutDuration);
                }
            }
        }

        private static void hideSpinnerApproachCircle(
            DrawableSpinner spinner)
        {
            var approachCircle =
                (spinner.Body.Drawable as IHasApproachCircle)
                ?.ApproachCircle;

            if (approachCircle == null)
                return;

            using (spinner.BeginAbsoluteSequence(
                       spinner.HitObject.StartTime -
                       spinner.HitObject.TimePreempt))
            {
                approachCircle.Hide();
            }
        }
    }
}