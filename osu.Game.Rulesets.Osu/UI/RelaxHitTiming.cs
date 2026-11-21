// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Decides when the override Relax should click for a hit object, from the configured offset and jitter.
    /// </summary>
    public class RelaxHitTiming
    {
        /// <summary>
        /// The latest (relative to the start time) Relax is ever allowed to click.
        /// </summary>
        public const double MAX_LATE = 30;

        private readonly Random random;
        private readonly Dictionary<HitObject, double> jitterByObject = new Dictionary<HitObject, double>();

        public RelaxHitTiming(int? seed = null)
        {
            random = seed == null ? new Random() : new Random(seed.Value);
        }

        /// <summary>
        /// The time at which Relax should click <paramref name="hitObject"/>.
        /// </summary>
        /// <remarks>
        /// The random part is chosen once per object, so asking again for the same object always gives the same time.
        /// The result is never earlier than <see cref="OsuModRelax.RELAX_LENIENCY"/> before the start time,
        /// as objects are not looked at before that.
        /// </remarks>
        /// <param name="hitObject">The object to click.</param>
        /// <param name="offset">The fixed offset from the start time, in milliseconds. Negative is early.</param>
        /// <param name="jitter">The most the time may randomly move either way, in milliseconds.</param>
        public double GetHitTime(HitObject hitObject, double offset, double jitter)
        {
            double jitterUnit = 0;

            if (jitter > 0 && !jitterByObject.TryGetValue(hitObject, out jitterUnit))
                jitterByObject[hitObject] = jitterUnit = random.NextDouble() * 2 - 1;

            double effectiveOffset = Math.Clamp(offset + jitterUnit * jitter, -OsuModRelax.RELAX_LENIENCY, MAX_LATE);
            return hitObject.StartTime + effectiveOffset;
        }

        /// <summary>
        /// Drops what is remembered about <paramref name="hitObject"/>, once it has been dealt with.
        /// </summary>
        public void Forget(HitObject hitObject) => jitterByObject.Remove(hitObject);
    }
}
