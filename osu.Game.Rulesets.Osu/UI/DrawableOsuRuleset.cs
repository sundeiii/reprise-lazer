// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osuTK;
using static osu.Game.Input.Handlers.ReplayInputHandler;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class DrawableOsuRuleset : DrawableRuleset<OsuHitObject>
    {
        private Bindable<bool>? cursorHideEnabled;

        public new OsuInputManager KeyBindingInputManager =>
            (OsuInputManager)base.KeyBindingInputManager;

        public new OsuPlayfield Playfield =>
            (OsuPlayfield)base.Playfield;

        protected new OsuRulesetConfigManager Config =>
            (OsuRulesetConfigManager)base.Config;

        // Hidden Relax state.
        private readonly RelaxHitTiming relaxTiming = new RelaxHitTiming();

        private bool relaxWasEnabled;
        private bool relaxIsDown;
        private bool relaxWasLeft;
        private double relaxLastStateChangeTime;

        private DrawableHitCircle? lastAimCorrectedCircle;

        public DrawableOsuRuleset(
            Ruleset ruleset,
            IBeatmap beatmap,
            IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        [BackgroundDependencyLoader]
        private void load(ReplayPlayer? replayPlayer)
        {
            if (replayPlayer != null)
            {
                ReplayAnalysisOverlay analysisOverlay;

                PlayfieldAdjustmentContainer.Add(
                    analysisOverlay =
                        new ReplayAnalysisOverlay(
                            replayPlayer.Score.Replay));

                Overlays.Add(
                    analysisOverlay.CreateProxy().With(
                        proxy =>
                            proxy.Depth =
                                float.NegativeInfinity));

                replayPlayer.AddSettings(
                    new ReplayAnalysisSettings(Config));

                cursorHideEnabled =
                    Config.GetBindable<bool>(
                        OsuRulesetSetting
                            .ReplayCursorHideEnabled);

                cursorHideEnabled.BindValueChanged(
                    enabled =>
                        Playfield.Cursor.FadeTo(
                            enabled.NewValue ? 0 : 1),
                    true);
            }
        }

        protected override void Update()
        {
            base.Update();

            updateOverrideRelax();
            updateOverrideAimCorrection();
        }

        private void updateOverrideRelax()
        {
            bool enabled = GameplayOverrides.Relax;

            /*
             * Never generate Relax input while watching a replay.
             */
            bool hasReplay =
                KeyBindingInputManager.ReplayInputHandler != null;

            if (hasReplay)
            {
                if (relaxWasEnabled)
                    disableOverrideRelax();

                relaxWasEnabled = false;
                return;
            }

            /*
             * Detect live setting changes.
             */
            if (enabled != relaxWasEnabled)
            {
                relaxWasEnabled = enabled;

                if (enabled)
                    enableOverrideRelax();
                else
                    disableOverrideRelax();
            }

            if (!enabled)
                return;

            bool requiresHold = false;
            bool requiresHit = false;

            double time = Playfield.Clock.CurrentTime;

            foreach (DrawableOsuHitObject drawable in
                     Playfield.HitObjectContainer
                              .AliveObjects
                              .OfType<DrawableOsuHitObject>())
            {
                /*
                 * Not close enough to the object's hit time.
                 */
                if (time <
                    drawable.HitObject.StartTime -
                    OsuModRelax.RELAX_LENIENCY)
                {
                    break;
                }

                if (drawable.Judged)
                    forgetRelaxTiming(drawable);

                /*
                 * Already judged, or beyond the object's
                 * hittable duration.
                 */
                if (drawable.IsHit ||
                    drawable.HitObject is IHasDuration duration &&
                    time > duration.EndTime)
                {
                    continue;
                }

                switch (drawable)
                {
                    case DrawableHitCircle circle:
                        handleHitCircle(circle);
                        break;

                    case DrawableSlider slider:
                        /*
                         * Handles overlapping / 2B slider heads.
                         */
                        if (!slider.HeadCircle.IsHit)
                            handleHitCircle(slider.HeadCircle);

                        /*
                         * Holding presses the key, which would hit the slider
                         * head early, so only hold once the hit time is reached.
                         */
                        if (time >=
                            getRelaxHitTime(
                                slider.HeadCircle.HitObject))
                        {
                            requiresHold |=
                                slider.SliderInputManager
                                      .IsMouseInFollowArea(
                                          slider.Tracking.Value);
                        }

                        break;

                    case DrawableSpinner spinner:
                        requiresHold |=
                            spinner.HitObject.SpinsRequired > 0;
                        break;
                }
            }

            if (requiresHit)
            {
                changeRelaxState(false, time);
                changeRelaxState(true, time);
            }

            if (requiresHold)
            {
                changeRelaxState(true, time);
            }
            else if (relaxIsDown &&
                     time - relaxLastStateChangeTime >
                     AutoGenerator.KEY_UP_DELAY)
            {
                changeRelaxState(false, time);
            }

            void handleHitCircle(
                DrawableHitCircle circle)
            {
                if (!circle.HitArea.IsHovered)
                    return;

                double hitTime =
                    getRelaxHitTime(circle.HitObject);

                if (time < hitTime)
                    return;

                Debug.Assert(
                    circle.HitObject.HitWindows != null);

                requiresHit |=
                    circle.HitObject.HitWindows.CanBeHit(
                        time -
                        circle.HitObject.StartTime);
            }
        }

        private double getRelaxHitTime(OsuHitObject hitObject) =>
            relaxTiming.GetHitTime(
                hitObject,
                GameplayOverrides.RelaxOffset,
                GameplayOverrides.RelaxJitter);

        private void forgetRelaxTiming(DrawableOsuHitObject drawable)
        {
            relaxTiming.Forget(drawable.HitObject);

            if (drawable is DrawableSlider slider)
                relaxTiming.Forget(slider.HeadCircle.HitObject);
        }

        private void updateOverrideAimCorrection()
        {
            if (!GameplayOverrides.AimAssist)
            {
                lastAimCorrectedCircle = null;
                return;
            }

            if (KeyBindingInputManager.ReplayInputHandler != null)
            {
                lastAimCorrectedCircle = null;
                return;
            }

            double time = Playfield.Clock.CurrentTime;

            DrawableOsuHitObject? target =
                Playfield.HitObjectContainer.AliveObjects
                         .OfType<DrawableOsuHitObject>()
                         .Where(drawable => !drawable.IsHit)
                         .OrderBy(drawable => drawable.HitObject.StartTime)
                         .FirstOrDefault();

            if (target == null || target is DrawableSpinner)
                return;

            DrawableHitCircle? targetCircle = getJudgementCircle(target);

            if (lastAimCorrectedCircle?.IsHit == true)
                lastAimCorrectedCircle = null;

            if (targetCircle == null || targetCircle.IsHit)
                return;

            if (targetCircle == lastAimCorrectedCircle)
                return;

            /*
             * Stable Skooter performs correction immediately around the hit time,
             * rather than throughout the complete early hittable window.
             */
            /*
             * The correction is only held for a couple of input updates.
             * Relax doesn't click until its hit time, so start the correction
             * then, otherwise it would already have expired by the time Relax clicks.
             */
            bool tooEarly =
                GameplayOverrides.Relax
                    ? time <
                      getRelaxHitTime(targetCircle.HitObject)
                    : targetCircle.HitObject.StartTime - time >= 12;

            if (tooEarly)
                return;

            Vector2 cursorPosition =
                Playfield.ToLocalSpace(
                    KeyBindingInputManager.CurrentState.Mouse.Position);

            /*
             * Equivalent to stable's:
             *
             * FindCircleAt(x, y, hittableRangeOnly: true) == null
             *
             * Do not correct if the cursor is already over a hittable circle.
             */
            if (isCursorOverHittableCircle(cursorPosition, time))
                return;

            float correctionRange =
                GameplayOverrides.AimCorrectionStrength.Value;

            if (GameplayOverrides.AimCorrectionRelative)
            {
                correctionRange +=
                    (float)targetCircle.HitObject.Radius + 1;
            }

            correctionRange = Math.Min(correctionRange, 60f);

            if (Vector2.Distance(cursorPosition, targetCircle.Position) >
                correctionRange)
            {
                return;
            }

            lastAimCorrectedCircle = targetCircle;

            KeyBindingInputManager.ApplyAimCorrection(
                Playfield.ToScreenSpace(targetCircle.Position));
        }

        private bool isCursorOverHittableCircle(
            Vector2 cursorPosition,
            double currentTime)
        {
            foreach (DrawableOsuHitObject drawable in
                     Playfield.HitObjectContainer
                              .AliveObjects
                              .OfType<DrawableOsuHitObject>())
            {
                DrawableHitCircle? circle =
                    getJudgementCircle(drawable);

                if (circle == null)
                    continue;

                if (!isCircleHittable(
                        circle,
                        currentTime))
                {
                    continue;
                }

                float distance =
                    Vector2.Distance(
                        cursorPosition,
                        circle.Position);

                if (distance <=
                    circle.HitObject.Radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static DrawableHitCircle?
            getJudgementCircle(
                DrawableOsuHitObject drawable)
        {
            return drawable switch
            {
                DrawableHitCircle circle =>
                    circle,

                DrawableSlider slider
                    when !slider.HeadCircle.IsHit =>
                    slider.HeadCircle,

                _ => null,
            };
        }

        private static bool isCircleHittable(
            DrawableHitCircle circle,
            double currentTime)
        {
            if (circle.IsHit)
                return false;

            if (circle.HitObject.HitWindows == null)
                return false;

            return circle.HitObject
                         .HitWindows
                         .CanBeHit(
                             currentTime -
                             circle.HitObject.StartTime);
        }

        private void enableOverrideRelax()
        {
            /*
             * Prevent physical keyboard/mouse tapping from
             * being sent as gameplay input while hidden
             * Relax is active.
             */
            KeyBindingInputManager.AllowGameplayInputs =
                false;

            /*
             * Start from a released input state.
             */
            releaseRelaxInput();
        }

        private void disableOverrideRelax()
        {
            /*
             * Release any generated key before restoring
             * normal tapping.
             */
            releaseRelaxInput();

            KeyBindingInputManager.AllowGameplayInputs =
                true;
        }

        private void changeRelaxState(
            bool down,
            double time)
        {
            if (relaxIsDown == down)
                return;

            relaxIsDown = down;
            relaxLastStateChangeTime = time;

            var state = new ReplayState<OsuAction>
            {
                PressedActions =
                    new List<OsuAction>()
            };

            if (down)
            {
                state.PressedActions.Add(
                    relaxWasLeft
                        ? OsuAction.LeftButton
                        : OsuAction.RightButton);

                relaxWasLeft = !relaxWasLeft;
            }

            state.Apply(
                KeyBindingInputManager.CurrentState,
                KeyBindingInputManager);
        }

        private void releaseRelaxInput()
        {
            if (!relaxIsDown)
                return;

            relaxIsDown = false;

            var state = new ReplayState<OsuAction>
            {
                PressedActions =
                    new List<OsuAction>()
            };

            state.Apply(
                KeyBindingInputManager.CurrentState,
                KeyBindingInputManager);
        }

        public override DrawableHitObject<OsuHitObject>?
            CreateDrawableRepresentation(
                OsuHitObject hitObject) => null;

        public override bool ReceivePositionalInputAt(
            Vector2 screenSpacePos) => true;

        protected override Playfield CreatePlayfield() =>
            new OsuPlayfield();

        protected override PassThroughInputManager
            CreateInputManager() =>
                new OsuInputManager(
                    Ruleset.RulesetInfo);

        public override PlayfieldAdjustmentContainer
            CreatePlayfieldAdjustmentContainer() =>
                new OsuPlayfieldAdjustmentContainer
                {
                    AlignWithStoryboard = true
                };

        protected override ResumeOverlay
            CreateResumeOverlay()
        {
            if (Mods.Any(
                    mod =>
                        mod is OsuModAutopilot
                            or OsuModTouchDevice))
            {
                return new DelayedResumeOverlay
                {
                    Scale = new Vector2(0.65f)
                };
            }

            return new OsuResumeOverlay();
        }

        protected override ReplayInputHandler
            CreateReplayInputHandler(
                Replay replay) =>
                    new OsuFramedReplayInputHandler(
                        replay);

        protected override ReplayRecorder
            CreateReplayRecorder(
                Score score) =>
                    new OsuReplayRecorder(score);

        public override double GameplayStartTime
        {
            get
            {
                if (Objects.FirstOrDefault()
                    is OsuHitObject first)
                {
                    return first.StartTime -
                           Math.Max(
                               2000,
                               first.TimePreempt);
                }

                return 0;
            }
        }
    }
}