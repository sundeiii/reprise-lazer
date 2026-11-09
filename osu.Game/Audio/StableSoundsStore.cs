// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.IO.Stores;
using osu.Game.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Audio
{
    /// <summary>
    /// Reprise: osu!stable's sounds in front of the game's own, in groups each turned on by its own setting (<see cref="OsuSetting.StableSounds"/>
    /// for typing, <see cref="OsuSetting.StableChatSounds"/>, <see cref="OsuSetting.StableSongSelectSounds"/>): stable's default ones
    /// (Resources/SamplesStable, from osu!ui.dll), under the names the game asks for. Read when the game starts, so a change applies after restarting it.
    /// </summary>
    public class StableSoundsStore : IResourceStore<byte[]>
    {
        /// <summary>The groups of stable's sounds in use.</summary>
        public static StableSoundGroups EnabledGroups { get; private set; }

        /// <summary>Whether stable's chat sounds are in use (for the places that pick their sound by name: a private message's notification).</summary>
        public static bool ChatEnabled => EnabledGroups.HasFlag(StableSoundGroups.Chat);

        /// <summary>The game's names for the sounds, stable's file for each and the group it's in.</summary>
        private static readonly Dictionary<string, (string File, StableSoundGroups Group)> files = new Dictionary<string, (string, StableSoundGroups)>
        {
            ["Keyboard/key-press-1"] = ("key-press-1.mp3", StableSoundGroups.Typing),
            ["Keyboard/key-press-2"] = ("key-press-2.mp3", StableSoundGroups.Typing),
            ["Keyboard/key-press-3"] = ("key-press-3.mp3", StableSoundGroups.Typing),
            ["Keyboard/key-press-4"] = ("key-press-4.mp3", StableSoundGroups.Typing),
            ["Keyboard/key-caps"] = ("key-caps.mp3", StableSoundGroups.Typing),
            ["Keyboard/key-delete"] = ("key-delete.mp3", StableSoundGroups.Typing),
            ["Keyboard/key-movement"] = ("key-movement.mp3", StableSoundGroups.Typing),
            ["Keyboard/key-confirm"] = ("key-confirm.mp3", StableSoundGroups.Typing),
            // Stable's chat highlight, which a private message is too.
            ["UI/notification-mention"] = ("match-start.wav", StableSoundGroups.Chat),
            ["Multiplayer/player-joined"] = ("match-join.wav", StableSoundGroups.Chat),
            ["Multiplayer/player-left"] = ("match-leave.wav", StableSoundGroups.Chat),
            // Song select: opening a beatmap set, picking a difficulty, and starting it (stable's menuhit, which its pause menu's retry is too).
            ["SongSelect/select-expand"] = ("select-expand.mp3", StableSoundGroups.SongSelect),
            ["SongSelect/select-difficulty"] = ("select-difficulty.mp3", StableSoundGroups.SongSelect),
            ["SongSelect/confirm-selection"] = ("menuhit.mp3", StableSoundGroups.SongSelect),
            ["Gameplay/restart"] = ("menuhit.mp3", StableSoundGroups.SongSelect),
        };

        private const string samples_prefix = @"Samples/";

        private readonly IResourceStore<byte[]> sounds;

        public StableSoundsStore(StableSoundGroups enabled)
        {
            EnabledGroups = enabled;
            sounds = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(StableSoundsStore).Assembly), @"Resources/SamplesStable");
        }

        /// <summary>Stable's name for a sound the game plays ("Keyboard/key-press-1" → "key-press-1"), which a stable skin has its own of, or null when it isn't one of stable's (or its group is off).</summary>
        public static string? StableName(string name) =>
            fileFor(name) is string file ? Path.GetFileNameWithoutExtension(file) : null;

        private static string? fileFor(string name) =>
            files.TryGetValue(name, out var sound) && EnabledGroups.HasFlag(sound.Group) ? sound.File : null;

        /// <summary>
        /// A sound the game plays (its name, "Keyboard/key-press-1"): the current skin's own when it's a stable skin that has it, as stable
        /// plays them, otherwise stable's default (or the game's own when stable's are off). Looked up when played, so a skin change applies at once.
        /// </summary>
        public static ISample? Get(AudioManager audio, SkinManager? skins, string name)
        {
            if (skins?.CurrentSkin.Value is LegacySkin skin && StableName(name) is string stableName && skin.GetSample(new SampleInfo(stableName)) is ISample skinned)
                return skinned;

            return audio.Samples.Get(name);
        }

        /// <summary>Stable's file for a sample the game asks for ("Samples/Keyboard/key-press-1.wav"), or null to leave it to the game's own.</summary>
        private string? lookup(string name)
        {
            if (EnabledGroups == StableSoundGroups.None || !name.StartsWith(samples_prefix, System.StringComparison.Ordinal))
                return null;

            // Any extension the game tries (wav, mp3, ...).
            string stem = Path.ChangeExtension(name[samples_prefix.Length..], null).Replace('\\', '/');
            return fileFor(stem);
        }

        public byte[]? Get(string name) => lookup(name) is string file ? sounds.Get(file) : null;

        public Task<byte[]?> GetAsync(string name, CancellationToken cancellationToken = default) =>
            lookup(name) is string file ? sounds.GetAsync(file, cancellationToken)! : Task.FromResult<byte[]?>(null);

        public Stream? GetStream(string name) => lookup(name) is string file ? sounds.GetStream(file) : null;

        public IEnumerable<string> GetAvailableResources() => Enumerable.Empty<string>();

        public void Dispose() => sounds.Dispose();
    }

    /// <summary>
    /// Reprise: the groups of osu!stable's sounds, each with its own setting.
    /// </summary>
    [System.Flags]
    public enum StableSoundGroups
    {
        None = 0,

        /// <summary>Typing in text boxes.</summary>
        Typing = 1,

        /// <summary>Chat highlights and private messages, players joining or leaving a multiplayer room.</summary>
        Chat = 1 << 1,

        /// <summary>Song select (a set opening, a difficulty picked, a map started) and retrying.</summary>
        SongSelect = 1 << 2,
    }
}
