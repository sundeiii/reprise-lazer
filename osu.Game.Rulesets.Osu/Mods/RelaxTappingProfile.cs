// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Reprise: how a real player taps, measured from their replays (Tools/relax-tapping/analyse.py), for <see cref="OsuModRelax"/>
    /// to press keys the way they do: which key a note gets (alternating or not, by the gap before it), how long it's held, how early
    /// or late it's hit, and how long after a slider's end the key is let go. Every measure is in real milliseconds, by its percentiles (the ends trimmed to the 0.5th and 99.5th).
    /// </summary>
    public class RelaxTappingProfile
    {
        /// <summary>A note this long or more after the one before starts afresh (the first key the player tends to use).</summary>
        public const double BREAK_GAP = 1000;

        private const string resource_prefix = "osu.Game.Rulesets.Osu.Resources.RelaxTapping.";

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("replays")]
        public int Replays { get; set; }

        [JsonProperty("notes")]
        public int Notes { get; set; }

        [JsonProperty("firstKeyLeft")]
        public double FirstKeyLeft { get; set; } = 0.5;

        [JsonProperty("buckets")]
        public Bucket[] Buckets { get; set; } = Array.Empty<Bucket>();

        [JsonProperty("summary")]
        public ProfileSummary? Summary { get; set; }

        public class ProfileSummary
        {
            [JsonProperty("unstableRate")]
            public double UnstableRate { get; set; }
        }

        [JsonProperty("sliderRelease")]
        public double[]? SliderRelease { get; set; }

        public class Bucket
        {
            /// <summary>The longest gap before a note (real ms) this covers; null for any longer.</summary>
            [JsonProperty("maxGap")]
            public double? MaxGap { get; set; }

            /// <summary>How many of their notes it's measured from.</summary>
            [JsonProperty("notes")]
            public int Notes { get; set; }

            /// <summary>How often a note is hit with the other key than the note before.</summary>
            [JsonProperty("alternate")]
            public double Alternate { get; set; } = 0.5;

            /// <summary>
            /// How often a note is hit with the other key, by the key before (left / right): a player favouring one key leaves it less
            /// often than the other. Null where not measured, when <see cref="Alternate"/> stands for both.
            /// </summary>
            [JsonProperty("switchFromLeft")]
            public double? SwitchFromLeft { get; set; }

            /// <inheritdoc cref="SwitchFromLeft"/>
            [JsonProperty("switchFromRight")]
            public double? SwitchFromRight { get; set; }

            /// <summary>How often the note after one hit with <paramref name="leftBefore"/>'s key is hit with the other.</summary>
            public double SwitchFrom(bool leftBefore) => (leftBefore ? SwitchFromLeft : SwitchFromRight) ?? Alternate;

            [JsonProperty("hold")]
            public double[]? Hold { get; set; }

            [JsonProperty("offset")]
            public double[]? Offset { get; set; }
        }

        /// <summary>The measures for a note <paramref name="gap"/> real milliseconds after the one before.</summary>
        public Bucket For(double gap) => Buckets.FirstOrDefault(b => b.MaxGap == null || gap < b.MaxGap) ?? Buckets.Last();

        /// <summary>A value drawn from <paramref name="deciles"/> (points evenly through the values; evenly between neighbours), or <paramref name="fallback"/> without any.</summary>
        public static double Sample(double[]? deciles, Random random, double fallback)
        {
            if (deciles == null || deciles.Length < 2)
                return fallback;

            double position = random.NextDouble() * (deciles.Length - 1);
            int lower = (int)Math.Floor(position);
            int upper = Math.Min(lower + 1, deciles.Length - 1);
            return deciles[lower] + (deciles[upper] - deciles[lower]) * (position - lower);
        }

        /// <summary>The players there are profiles of, by name.</summary>
        public static IReadOnlyList<string> Available { get; } = typeof(RelaxTappingProfile).Assembly.GetManifestResourceNames()
                                                                                               .Where(n => n.StartsWith(resource_prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
                                                                                               .Select(n => n[resource_prefix.Length..^".json".Length])
                                                                                               .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                                                                                               .ToArray();

        /// <summary>The profile of <paramref name="name"/>; null for none (Relax's own perfect tapping) or one there isn't.</summary>
        public static RelaxTappingProfile? Load(string? name)
        {
            if (string.IsNullOrEmpty(name) || !Available.Contains(name))
                return null;

            using var stream = typeof(RelaxTappingProfile).Assembly.GetManifestResourceStream(resource_prefix + name + ".json");
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            var profile = JsonConvert.DeserializeObject<RelaxTappingProfile>(reader.ReadToEnd());
            return profile?.Buckets.Length > 0 ? profile : null;
        }
    }
}
