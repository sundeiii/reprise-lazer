// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Configuration
{
    public static class GameplayOverrides
    {
        public static bool Relax { get; set; }

        /// <summary>
        /// How far from a hit object's start time Relax clicks, in milliseconds. Negative is early.
        /// </summary>
        public static double RelaxOffset { get; set; }

        /// <summary>
        /// A random amount, up to this many milliseconds either way, added to <see cref="RelaxOffset"/> for each hit object.
        /// </summary>
        public static double RelaxJitter { get; set; }

        public static bool RemoveHidden { get; set; }

        private static readonly BindableBool timewarp_enabled = new BindableBool();

        public static bool TimewarpEnabled
        {
            get => timewarp_enabled.Value;
            set => timewarp_enabled.Value = value;
        }

        /// <summary>
        /// Allows displays which show speed dependent values (like length and BPM) to update when Timewarp is toggled.
        /// </summary>
        public static IBindable<bool> TimewarpEnabledBindable => timewarp_enabled;

        public static BindableDouble TimewarpRate { get; } =
            new BindableDouble(1.0)
            {
                MinValue = 0.5,
                MaxValue = 3.0,
                Precision = 0.01,
            };

        public static bool ApproachRateEnabled { get; set; }

        public static double ApproachRate { get; set; } = 9.0;

        public static bool AimAssist { get; set; }

        public static BindableInt AimCorrectionStrength { get; } =
            new BindableInt(30)
            {
                MinValue = 0,
                MaxValue = 60,
            };

        public static bool AimCorrectionRelative { get; set; }

        public static bool AnyActive => Relax || AimAssist || RemoveHidden || TimewarpEnabled || ApproachRateEnabled;

        /// <summary>
        /// Whether plays with overrides on go online for this account. Everyone else's stay offline.
        /// </summary>
        public static bool MaySubmit(string? username) =>
            username != null && Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(username.ToLowerInvariant())))
                                        .Equals(submit_account, StringComparison.OrdinalIgnoreCase);

        private const string submit_account = "0b8ed14b0c4f4928ef3075c60bc551be7598de1bbd7fffa0f5c22c95b9abe421";

        private static int playHolds;

        /// <summary>
        /// Whether a play is holding the setup steady. Changes from the companion wait until no play holds it,
        /// so each play runs (and is submitted or not) with one setup from start to end.
        /// </summary>
        public static bool Held => Volatile.Read(ref playHolds) > 0;

        public static void Hold() => Interlocked.Increment(ref playHolds);

        public static void Release() => Interlocked.Decrement(ref playHolds);

        /// <summary>
        /// The speed gameplay actually runs at, given the selected <paramref name="mods"/>.
        /// While Timewarp is on it replaces Double Time and Half Time (they are ignored during gameplay),
        /// but any other mod which changes the speed still applies on top of it.
        /// </summary>
        public static double GetEffectiveRate(IEnumerable<Mod> mods)
        {
            if (!TimewarpEnabled)
                return ModUtils.CalculateRateWithMods(mods);

            return ModUtils.CalculateRateWithMods(mods.Where(m => m is not (ModDoubleTime or ModHalfTime))) * TimewarpRate.Value;
        }
    }
}
