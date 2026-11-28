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
using osuTK;
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

            var objects = drawableRuleset.Beatmap.HitObjects;
            for (int i = 0; i + 1 < objects.Count; i++)
                nextObject[objects[i]] = objects[i + 1];
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

        private Vector2? lastMouse;
        private double lastUpdateTime;

        // How long updates have lately been apart (the longest, slowly forgotten): a hit waiting to come late is made in the update
        // before the cursor could be gone, not the one after.
        private double frameInterval = 1000 / 60.0;

        private readonly Dictionary<OsuHitObject, OsuHitObject> nextObject = new Dictionary<OsuHitObject, OsuHitObject>();

        /// <summary>
        /// The latest a late hit on <paramref name="note"/> can safely wait for, the cursor still on it: a cursor going on to the next
        /// note leaves it as early as autoplay's does (eased out: at twice its average speed straight away), with a fifth to spare;
        /// on a slider's head, as early as its ball leaves. Never, for a cursor that needn't leave (the next note on top of it).
        /// </summary>
        private double cursorLeaves(OsuHitObject note, DrawableSlider? slider)
        {
            double radius = note.Radius;

            if (slider != null)
                return slider.HitObject.Velocity > 0 && slider.HitObject.Path.Distance > radius ? note.StartTime + 0.8 * radius / slider.HitObject.Velocity : double.PositiveInfinity;

            if (!nextObject.TryGetValue(note, out var next) || next is Spinner)
                return double.PositiveInfinity;

            double distance = Vector2.Distance(note.StackedPosition, next.StackedPosition);
            if (distance <= radius)
                return double.PositiveInfinity;

            return note.StartTime + 0.8 * 0.5 * (next.StartTime - note.StartTime) * radius / distance;
        }

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

        /// <summary>For tests: each decision, written out.</summary>
        public static Action<string>? TestLog { get; set; }

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
            else if (random.NextDouble() < measures.SwitchFrom(last == OsuAction.LeftButton))
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

            TestLog?.Invoke(FormattableString.Invariant($"PLAN {offset / rate:0.00}"));

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

            // Where the cursor is and how it's moving (per millisecond), for "is it about to leave the note".
            var mouse = osuInputManager.CurrentState.Mouse.Position;
            if (time > lastUpdateTime)
                frameInterval = Math.Clamp(Math.Max(time - lastUpdateTime, frameInterval * 0.95), 1, 50);
            var mouseVelocity = lastMouse is Vector2 previous && time > lastUpdateTime ? (mouse - previous) / (float)(time - lastUpdateTime) : Vector2.Zero;
            lastMouse = mouse;
            lastUpdateTime = time;

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

                        // Only once its head is judged (hit with its own key, or missed): holding a key down sooner would hit the head with
                        // the key held, early (copied timing looks further ahead) or not with the key planned for it.
                        requiresHold |= slider.HeadCircle.Judged && slider.SliderInputManager.IsMouseInFollowArea(slider.Tracking.Value);
                        break;

                    case DrawableSpinner spinner:
                        requiresHold |= time >= spinner.HitObject.StartTime && spinner.HitObject.SpinsRequired > 0;
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

                if (!circle.HitArea.IsHovered)
                    return;

                double start = circle.HitObject.StartTime;

                if (!copyTiming)
                {
                    if (time < start - RELAX_LENIENCY)
                        return;
                }
                else if (time < pressAt)
                {
                    // Waiting to hit late, as they would, while the cursor surely stays on the note. Hit now instead when the next
                    // update could come after the cursor has gone (on to the next note, or with its slider's ball), or when it's
                    // already moving off it: going as it goes, off it by the next update or so.
                    double leaves = cursorLeaves(circle.HitObject, slider);
                    bool lastChance = time + frameInterval > leaves;

                    // With updates far apart, the one nearest their timing (not always the one after it).
                    bool nearest = time + frameInterval / 2 > pressAt;
                    bool leaving = Vector2.Dot(mouseVelocity, circle.HitArea.ScreenSpaceDrawQuad.Centre - mouse) < 0
                                   && !circle.HitArea.ReceivePositionalInputAt(mouse + mouseVelocity * (float)(frameInterval * 1.5));

                    TestLog?.Invoke($"t {time:0.0} note {start:0} pressAt {pressAt:0.0} leaves {leaves:0.0} interval {frameInterval:0.0} last {lastChance} leaving {leaving} nearest {nearest}");

                    if (time < start - Math.Max(RELAX_LENIENCY, frameInterval) || (!lastChance && !leaving && !nearest))
                        return;
                }

                Debug.Assert(circle.HitObject.HitWindows != null);
                if (!circle.HitObject.HitWindows.CanBeHit(time - circle.HitObject.StartTime))
                    return;

                // The same key held from before: let go first, as a finger would.
                if (held.Contains(note.Key))
                    release(note.Key);

                press(note.Key, time);
                pressed = true;
                TestLog?.Invoke($"PRESS t {time:0.0} note {circle.HitObject.StartTime:0} key {note.Key}");

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
