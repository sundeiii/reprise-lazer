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
    /// Reprise: osu!stable's typing, chat highlight and multiplayer room sounds (<see cref="OsuSetting.StableSounds"/>), in front of the game's own:
    /// stable's default ones (Resources/SamplesStable, from osu!ui.dll), under the names the game asks for.
    /// Read when the game starts, so a change applies after restarting it.
    /// </summary>
    public class StableSoundsStore : IResourceStore<byte[]>
    {
        /// <summary>Whether stable's sounds are in use (for the places that pick their sound by name: a private message's notification).</summary>
        public static bool Enabled { get; private set; }

        /// <summary>The game's names for the sounds and stable's file for each.</summary>
        private static readonly Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["Keyboard/key-press-1"] = "key-press-1.mp3",
            ["Keyboard/key-press-2"] = "key-press-2.mp3",
            ["Keyboard/key-press-3"] = "key-press-3.mp3",
            ["Keyboard/key-press-4"] = "key-press-4.mp3",
            ["Keyboard/key-caps"] = "key-caps.mp3",
            ["Keyboard/key-delete"] = "key-delete.mp3",
            ["Keyboard/key-movement"] = "key-movement.mp3",
            ["Keyboard/key-confirm"] = "key-confirm.mp3",
            // Stable's chat highlight, which a private message is too.
            ["UI/notification-mention"] = "match-start.wav",
            ["Multiplayer/player-joined"] = "match-join.wav",
            ["Multiplayer/player-left"] = "match-leave.wav",
        };

        private const string samples_prefix = @"Samples/";

        private readonly IResourceStore<byte[]> sounds;

        public StableSoundsStore(bool enabled)
        {
            Enabled = enabled;
            sounds = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(StableSoundsStore).Assembly), @"Resources/SamplesStable");
        }

        /// <summary>Stable's file for a sample the game asks for ("Samples/Keyboard/key-press-1.wav"), or null to leave it to the game's own.</summary>
        private string? lookup(string name)
        {
            if (!Enabled || !name.StartsWith(samples_prefix, System.StringComparison.Ordinal))
                return null;

            // Any extension the game tries (wav, mp3, ...).
            string stem = Path.ChangeExtension(name[samples_prefix.Length..], null).Replace('\\', '/');
            return files.GetValueOrDefault(stem);
        }

        public byte[]? Get(string name) => lookup(name) is string file ? sounds.Get(file) : null;

        public Task<byte[]?> GetAsync(string name, CancellationToken cancellationToken = default) =>
            lookup(name) is string file ? sounds.GetAsync(file, cancellationToken)! : Task.FromResult<byte[]?>(null);

        public Stream? GetStream(string name) => lookup(name) is string file ? sounds.GetStream(file) : null;

        public IEnumerable<string> GetAvailableResources() => Enumerable.Empty<string>();

        public void Dispose() => sounds.Dispose();
    }
}
