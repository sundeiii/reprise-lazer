// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;
using osu.Game.Configuration;

namespace osu.Game.Audio
{
    /// <summary>
    /// Reprise: the menu and interface sounds as lazer had them in 2023 (<see cref="OsuSetting.Sounds2023"/>), in front of the game's own:
    /// the ones that changed since (Resources/Samples2023, from osu!'s resources of 2023.417.0), under the names the game asks for now.
    /// Read when the game starts, so a change applies after restarting it.
    /// </summary>
    public class Sounds2023Store : IResourceStore<byte[]>
    {
        /// <summary>Whether the 2023 sounds are in use (for the places that pick their sound by name: the main menu, the music player).</summary>
        public static bool Enabled { get; private set; }

        /// <summary>Today's names for sounds 2023 had under another (several of today's share one of 2023's).</summary>
        private static readonly Dictionary<string, string> renamed = new Dictionary<string, string>
        {
            ["UI/notification-default"] = "UI/notification-pop-in",
            ["UI/notification-mention"] = "UI/notification-pop-in",
            ["UI/notification-friend-online"] = "UI/notification-pop-in",
            ["UI/notification-friend-offline"] = "UI/notification-pop-in",
            ["UI/notification-error"] = "UI/error-notification-pop-in",
            ["UI/overlay-big-pop-in"] = "UI/overlay-pop-in",
            ["UI/overlay-big-pop-out"] = "UI/overlay-pop-out",
            ["Menu/back-to-top"] = "Menu/button-back-select",
            ["Menu/back-to-logo"] = "Menu/button-back-select",
        };

        private const string samples_prefix = @"Samples/";

        private readonly IResourceStore<byte[]> sounds;
        private readonly HashSet<string> available;

        public Sounds2023Store(bool enabled)
        {
            Enabled = enabled;
            sounds = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Sounds2023Store).Assembly), @"Resources/Samples2023");
            available = sounds.GetAvailableResources().Select(r => r.Replace('\\', '/')).ToHashSet();
        }

        /// <summary>The 2023 file for a sample the game asks for ("Samples/UI/notification-default.wav"), or null to leave it to the game's own.</summary>
        private string? lookup(string name)
        {
            if (!Enabled || !name.StartsWith(samples_prefix, System.StringComparison.Ordinal))
                return null;

            string path = name[samples_prefix.Length..];
            // Any extension the game tries (wav, mp3, ...): the 2023 ones are all wav.
            string stem = Path.ChangeExtension(path, null).Replace('\\', '/');
            string file = (renamed.GetValueOrDefault(stem) ?? stem) + @".wav";
            return available.Contains(file) ? file : null;
        }

        public byte[]? Get(string name) => lookup(name) is string file ? sounds.Get(file) : null;

        public Task<byte[]?> GetAsync(string name, CancellationToken cancellationToken = default) =>
            lookup(name) is string file ? sounds.GetAsync(file, cancellationToken)! : Task.FromResult<byte[]?>(null);

        public Stream? GetStream(string name) => lookup(name) is string file ? sounds.GetStream(file) : null;

        public IEnumerable<string> GetAvailableResources() => Enumerable.Empty<string>();

        public void Dispose() => sounds.Dispose();
    }
}
