// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using static osu.Game.Input.Handlers.ReplayInputHandler;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModRelax : ModRelax, IUpdatableByPlayfield, IApplicableToDrawableRuleset<OsuHitObject>, IApplicableToPlayer, IHasNoTimedInputs, IReadFromConfig
    {
        public override LocalisableString Description => @"You don't need to click. Give your clicking/tapping fingers a break from the heat of things.";

        public override Type[] IncompatibleMods =>
            base.IncompatibleMods.Concat(new[] { typeof(OsuModAutopilot), typeof(OsuModMagnetised), typeof(OsuModAlternate), typeof(OsuModSingleTap) }).ToArray();

        /// <summary>
        /// How early before a hitobject's start time to trigger a hit.
        /// </summary>
        public const float RELAX_LENIENCY = 12;

        private bool isDownState;
        private bool wasLeft;

        private OsuInputManager osuInputManager = null!;

        private ReplayState<OsuAction> state = null!;
        private double lastStateChangeTime;

        private DrawableOsuRuleset ruleset = null!;
        private IPressHandler pressHandler = null!;

        private bool hasReplay;
        private bool legacyReplay;

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            ruleset = (DrawableOsuRuleset)drawableRuleset;

            // grab the input manager for future use.
            osuInputManager = ruleset.KeyBindingInputManager;

            rate = drawableRuleset.Mods.OfType<IApplicableToRate>().Aggregate(1.0, (r, m) => m.ApplyToRate(0, r));
        }

        // ---- Reprise: tapping like a real player (OsuSetting.RelaxTapping, RelaxTappingProfile) ----

        private RelaxTappingProfile? profile;
        private bool copyTiming;
        private double rate = 1;
        private readonly Random random = new Random();

        private readonly Dictionary<OsuHitObject, NotePlan> plans = new Dictionary<OsuHitObject, NotePlan>();
        private double? lastPlannedStart;
        private OsuAction? lastPlannedKey;

        private readonly HashSet<OsuAction> held = new HashSet<OsuAction>();
        private readonly Dictionary<OsuAction, double> releaseAt = new Dictionary<OsuAction, double>();
        private OsuAction lastPressed = OsuAction.LeftButton;

        private record NotePlan(OsuAction Key, double Offset, double Hold);

        public void ReadFromConfig(OsuConfigManager config)
        {
            profile = TestProfile ?? RelaxTappingProfile.Load(config.Get<string>(OsuSetting.RelaxTapping));
            copyTiming = profile != null && (TestCopyTiming ?? config.Get<bool>(OsuSetting.RelaxCopyTiming));
        }

        /// <summary>
        /// For tests: a profile (and timing choice) instead of the settings', also used with a replay that only moves the cursor.
        /// Static, as the mod played is a copy of the one selected.
        /// </summary>
        public static RelaxTappingProfile? TestProfile { get; set; }

        /// <inheritdoc cref="TestProfile"/>
        public static bool? TestCopyTiming { get; set; }

        /// <summary>The key, timing and hold of a note, drawn from the player's measures for the gap before it (in the order notes come).</summary>
        private NotePlan plan(OsuHitObject note)
        {
            if (plans.TryGetValue(note, out var existing))
                return existing;

            double gap = lastPlannedStart is double previous ? (note.StartTime - previous) / rate : double.PositiveInfinity;
            var measures = profile!.For(gap);

            OsuAction key;
            if (lastPlannedKey is not OsuAction last || gap >= RelaxTappingProfile.BREAK_GAP)
                key = random.NextDouble() < profile.FirstKeyLeft ? OsuAction.LeftButton : OsuAction.RightButton;
            else if (random.NextDouble() < measures.Alternate)
                key = last == OsuAction.LeftButton ? OsuAction.RightButton : OsuAction.LeftButton;
            else
                key = last;

            double offset = 0;

            if (copyTiming)
            {
                // Their timing, in the map's time; never so far off the note that it isn't hit at all.
                double meh = note.HitWindows.WindowFor(HitResult.Meh);
                offset = Math.Clamp(RelaxTappingProfile.Sample(measures.Offset, random, 0) * rate, -meh + 1, meh - 1);
            }

            double hold = Math.Max(10, RelaxTappingProfile.Sample(measures.Hold, random, AutoGenerator.KEY_UP_DELAY) * rate);

            lastPlannedStart = note.StartTime;
            lastPlannedKey = key;
            return plans[note] = new NotePlan(key, offset, hold);
        }

        private void updateLikePlayer(Playfield playfield)
        {
            double time = playfield.Clock.CurrentTime;
            bool requiresHold = false;
            bool pressed = false;
            double lookahead = Math.Max(RELAX_LENIENCY, copyTiming ? 200 : 0);

            foreach (var h in playfield.HitObjectContainer.AliveObjects.OfType<DrawableOsuHitObject>())
            {
                if (time < h.HitObject.StartTime - lookahead)
                    break;

                if (h.IsHit || (h.HitObject is IHasDuration hasEnd && time > hasEnd.EndTime))
                    continue;

                switch (h)
                {
                    case DrawableHitCircle circle:
                        tryHit(circle, null);
                        break;

                    case DrawableSlider slider:
                        if (!slider.HeadCircle.IsHit)
                            tryHit(slider.HeadCircle, slider);

                        requiresHold |= slider.SliderInputManager.IsMouseInFollowArea(slider.Tracking.Value);
                        break;

                    case DrawableSpinner spinner:
                        requiresHold |= spinner.HitObject.SpinsRequired > 0;
                        break;
                }
            }

            // A slider or spinner being held keeps its key down (the one pressed last).
            if (requiresHold)
            {
                if (!held.Contains(lastPressed))
                    press(lastPressed, time);
                releaseAt[lastPressed] = Math.Max(releaseAt.GetValueOrDefault(lastPressed), time + 1);
            }

            foreach (var key in held.ToArray())
            {
                if (time >= releaseAt.GetValueOrDefault(key))
                    release(key);
            }

            void tryHit(DrawableHitCircle circle, DrawableSlider? slider)
            {
                if (pressed)
                    return;

                var note = plan(circle.HitObject);
                double pressAt = circle.HitObject.StartTime + (copyTiming ? note.Offset : -RELAX_LENIENCY);

                if (time < pressAt || !circle.HitArea.IsHovered)
                    return;

                Debug.Assert(circle.HitObject.HitWindows != null);
                if (!circle.HitObject.HitWindows.CanBeHit(time - circle.HitObject.StartTime))
                    return;

                // The same key held from before: let go first, as a finger would.
                if (held.Contains(note.Key))
                    release(note.Key);

                press(note.Key, time);
                pressed = true;

                releaseAt[note.Key] = slider != null
                    ? slider.HitObject.EndTime + Math.Max(0, RelaxTappingProfile.Sample(profile!.SliderRelease, random, AutoGenerator.KEY_UP_DELAY) * rate)
                    : time + note.Hold;
            }
        }

        private void press(OsuAction key, double time)
        {
            held.Add(key);
            lastPressed = key;
            releaseAt[key] = time + AutoGenerator.KEY_UP_DELAY;

            // With a replay moving the cursor, its input state wins: the keys are triggered themselves (as for old replays).
            if (hasReplay)
                osuInputManager.KeyBindingContainer.TriggerPressed(key);
            else
                applyHeld();
        }

        private void release(OsuAction key)
        {
            if (!held.Remove(key))
                return;

            if (hasReplay)
                osuInputManager.KeyBindingContainer.TriggerReleased(key);
            else
                applyHeld();
        }

        private void applyHeld()
        {
            state = new ReplayState<OsuAction> { PressedActions = held.ToList() };
            state.Apply(osuInputManager.CurrentState, osuInputManager);
        }

        public void ApplyToPlayer(Player player)
        {
            if (osuInputManager.ReplayInputHandler != null)
            {
                hasReplay = true;

                Debug.Assert(ruleset.ReplayScore != null);
                legacyReplay = ruleset.ReplayScore.ScoreInfo.IsLegacyScore;

                pressHandler = legacyReplay ? new LegacyReplayPressHandler(this) : new PressHandler(this);

                return;
            }

            pressHandler = new PressHandler(this);
            osuInputManager.AllowGameplayInputs = false;
        }

        public void Update(Playfield playfield)
        {
            if (hasReplay && !legacyReplay)
                return;

            // Reprise: tapping like a real player, when one is picked (not for an old replay: its presses are Relax's own).
            if (profile != null && (!hasReplay || TestProfile != null))
            {
                updateLikePlayer(playfield);
                return;
            }

            bool requiresHold = false;
            bool requiresHit = false;

            double time = playfield.Clock.CurrentTime;

            foreach (var h in playfield.HitObjectContainer.AliveObjects.OfType<DrawableOsuHitObject>())
            {
                // we are not yet close enough to the object.
                if (time < h.HitObject.StartTime - RELAX_LENIENCY)
                    break;

                // already hit or beyond the hittable end time.
                if (h.IsHit || (h.HitObject is IHasDuration hasEnd && time > hasEnd.EndTime))
                    continue;

                switch (h)
                {
                    case DrawableHitCircle circle:
                        handleHitCircle(circle);
                        break;

                    case DrawableSlider slider:
                        // Handles cases like "2B" beatmaps, where sliders may be overlapping and simply holding is not enough.
                        if (!slider.HeadCircle.IsHit)
                            handleHitCircle(slider.HeadCircle);

                        requiresHold |= slider.SliderInputManager.IsMouseInFollowArea(slider.Tracking.Value);
                        break;

                    case DrawableSpinner spinner:
                        requiresHold |= spinner.HitObject.SpinsRequired > 0;
                        break;
                }
            }

            if (requiresHit)
            {
                changeState(false);
                changeState(true);
            }

            if (requiresHold)
                changeState(true);
            else if (isDownState && time - lastStateChangeTime > AutoGenerator.KEY_UP_DELAY)
                changeState(false);

            void handleHitCircle(DrawableHitCircle circle)
            {
                if (!circle.HitArea.IsHovered)
                    return;

                Debug.Assert(circle.HitObject.HitWindows != null);
                requiresHit |= circle.HitObject.HitWindows.CanBeHit(time - circle.HitObject.StartTime);
            }

            void changeState(bool down)
            {
                if (isDownState == down)
                    return;

                isDownState = down;
                lastStateChangeTime = time;

                state = new ReplayState<OsuAction>
                {
                    PressedActions = new List<OsuAction>()
                };

                if (down)
                {
                    pressHandler.HandlePress(wasLeft);
                    wasLeft = !wasLeft;
                }
                else
                {
                    pressHandler.HandleRelease(wasLeft);
                }
            }
        }

        private interface IPressHandler
        {
            void HandlePress(bool wasLeft);
            void HandleRelease(bool wasLeft);
        }

        private class PressHandler : IPressHandler
        {
            private readonly OsuModRelax mod;

            public PressHandler(OsuModRelax mod)
            {
                this.mod = mod;
            }

            public void HandlePress(bool wasLeft)
            {
                mod.state.PressedActions.Add(wasLeft ? OsuAction.LeftButton : OsuAction.RightButton);
                mod.state.Apply(mod.osuInputManager.CurrentState, mod.osuInputManager);
            }

            public void HandleRelease(bool wasLeft)
            {
                mod.state.Apply(mod.osuInputManager.CurrentState, mod.osuInputManager);
            }
        }

        // legacy replays do not contain key-presses with Relax mod, so they need to be triggered by themselves.
        private class LegacyReplayPressHandler : IPressHandler
        {
            private readonly OsuModRelax mod;

            public LegacyReplayPressHandler(OsuModRelax mod)
            {
                this.mod = mod;
            }

            public void HandlePress(bool wasLeft)
            {
                mod.osuInputManager.KeyBindingContainer.TriggerPressed(wasLeft ? OsuAction.LeftButton : OsuAction.RightButton);
            }

            public void HandleRelease(bool wasLeft)
            {
                // this intentionally releases right when `wasLeft` is true because `wasLeft` is set at point of press and not at point of release
                mod.osuInputManager.KeyBindingContainer.TriggerReleased(wasLeft ? OsuAction.RightButton : OsuAction.LeftButton);
            }
        }
    }
}
