// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace osu.Game.Skinning.Mixer
{
    /// <summary>
    /// Reprise: one part of a skin the mixer offers (a group of them, some images or sounds, a skin.ini setting or section), and what's
    /// chosen for it.
    /// </summary>
    public abstract class SkinMixerOption
    {
        public virtual string Name { get; set; } = string.Empty;

        public SkinMixerChoice Choice { get; set; } = SkinMixerChoice.KEEP;

        /// <summary>The parts under this (a group's), all the way down, or just this one.</summary>
        public virtual IEnumerable<SkinMixerOption> Leaves => new[] { this };

        /// <summary>Whether it can be made blank (images and sounds can; skin.ini settings go back to the default instead).</summary>
        public virtual bool CanBeBlank => false;
    }

    /// <summary>Reprise: parts grouped under a name ("Hitcircles").</summary>
    public class SkinMixerGroup : SkinMixerOption
    {
        public SkinMixerOption[] Children { get; set; } = Array.Empty<SkinMixerOption>();

        /// <summary>The sounds that make up a hitsound set (OsuSkinMixer plays them as a preview).</summary>
        public string[]? PreviewFileNames { get; set; }

        public override IEnumerable<SkinMixerOption> Leaves => Children.SelectMany(c => c.Leaves);

        public override bool CanBeBlank => Leaves.Any(l => l.CanBeBlank);
    }

    /// <summary>
    /// Reprise: images or sounds by name, with their @2x versions; with <see cref="IsAnimatable"/>, their numbered frames ("menu-back-0");
    /// a name ending in "*" takes the given suffixes after it (all of them without).
    /// </summary>
    public class SkinMixerFileOption : SkinMixerOption
    {
        private static readonly string[] image_extensions = { ".png", ".jpg", ".jpeg" };
        private static readonly string[] audio_extensions = { ".wav", ".mp3", ".ogg" };

        public readonly string FileName;
        public readonly bool IsAudio;
        public readonly bool IsAnimatable;
        public readonly string[]? AllowedSuffixes;

        private readonly string? displayName;

        public SkinMixerFileOption(string fileName, bool isAudio, string? displayName = null, bool isAnimatable = false, string[]? allowedSuffixes = null)
        {
            FileName = fileName;
            IsAudio = isAudio;
            IsAnimatable = isAnimatable;
            AllowedSuffixes = allowedSuffixes;
            this.displayName = displayName;
        }

        public override string Name => displayName ?? FileName + (IsAudio ? ".wav" : ".png");

        public override bool CanBeBlank => true;

        /// <summary>Whether a skin's file (by its path in the skin) is one of these. Only files at the skin's top level are.</summary>
        public bool Matches(string path)
        {
            if (path.Contains('/') || path.Contains('\\'))
                return false;

            string extension = Path.GetExtension(path).ToLowerInvariant();

            if (!(IsAudio ? audio_extensions : image_extensions).Contains(extension))
                return false;

            string name = Path.GetFileNameWithoutExtension(path);
            string baseName = FileName.TrimEnd('*');

            if (name.Equals(FileName, StringComparison.OrdinalIgnoreCase) || name.Equals(FileName + "@2x", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                return false;

            string suffix = name[baseName.Length..];

            // An animation's frame: "menu-back-10", "sliderb10" (and their @2x).
            if (IsAnimatable && int.TryParse(trimEnd(suffix, "@2x"), out _))
                return true;

            if (!FileName.EndsWith('*'))
                return false;

            return AllowedSuffixes == null || AllowedSuffixes.Any(s => trimEnd(suffix, "@2x").Equals(s, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The files a blank version of these is made of (a 1x1 transparent image and its @2x, or a silent sound).</summary>
        public IEnumerable<string> BlankFiles()
        {
            var names = FileName.EndsWith('*')
                ? (AllowedSuffixes ?? new[] { string.Empty }).Select(s => FileName.TrimEnd('*') + s)
                : new[] { FileName };

            foreach (string name in names)
            {
                if (IsAudio)
                    yield return name + ".wav";
                else
                {
                    yield return name + ".png";
                    yield return name + "@2x.png";
                }
            }
        }

        private static string trimEnd(string s, string end) => s.EndsWith(end, StringComparison.OrdinalIgnoreCase) ? s[..^end.Length] : s;
    }

    /// <summary>Reprise: one skin.ini setting ("[Colours] Combo1").</summary>
    public class SkinMixerIniProperty : SkinMixerOption
    {
        public readonly string Section;
        public readonly string Property;

        public SkinMixerIniProperty(string section, string property)
        {
            Section = section;
            Property = property;
        }

        public override string Name => $"{Property} (skin.ini)";
    }

    /// <summary>Reprise: a whole skin.ini section, the one with the given setting ("[Mania]" with "Keys: 4").</summary>
    public class SkinMixerIniSection : SkinMixerOption
    {
        public readonly string Section;
        public readonly string Key;
        public readonly string Value;

        public SkinMixerIniSection(string section, string key, string value)
        {
            Section = section;
            Key = key;
            Value = value;
        }

        public override string Name => $"{Section} {Value}K (skin.ini)";

        public bool Matches(SkinIni.Section section) =>
            section.Name.Equals(Section, StringComparison.OrdinalIgnoreCase) && section.Get(Key)?.Trim() == Value;
    }

    /// <summary>Reprise: what's chosen for a part: the base skin's own, the game's default, nothing (blank), or another skin's.</summary>
    public sealed record SkinMixerChoice(SkinMixerChoice.Kind Type, SkinMixerSource? Skin = null)
    {
        public enum Kind
        {
            Keep,
            Default,
            Blank,
            FromSkin,

            /// <summary>A group whose parts differ (only shown).</summary>
            Mixed,
        }

        public static readonly SkinMixerChoice KEEP = new SkinMixerChoice(Kind.Keep);
        public static readonly SkinMixerChoice DEFAULT = new SkinMixerChoice(Kind.Default);
        public static readonly SkinMixerChoice BLANK = new SkinMixerChoice(Kind.Blank);
        public static readonly SkinMixerChoice MIXED = new SkinMixerChoice(Kind.Mixed);

        public static SkinMixerChoice From(SkinMixerSource skin) => new SkinMixerChoice(Kind.FromSkin, skin);

        public override string ToString() => Type switch
        {
            Kind.Keep => "Keep (base skin's)",
            Kind.Default => "Game default",
            Kind.Blank => "Blank (hidden)",
            Kind.Mixed => "Mixed",
            _ => Skin!.Name,
        };

        public bool Equals(SkinMixerChoice? other) => other != null && other.Type == Type && ReferenceEquals(other.Skin, Skin);

        public override int GetHashCode() => HashCode.Combine(Type, Skin);
    }

    /// <summary>Reprise: a skin's files, by their paths in it, read when needed.</summary>
    public class SkinMixerSource
    {
        public readonly string Name;
        public readonly IReadOnlyDictionary<string, Func<byte[]>> Files;

        public SkinMixerSource(string name, IEnumerable<KeyValuePair<string, Func<byte[]>>> files)
        {
            Name = name;

            var dictionary = new Dictionary<string, Func<byte[]>>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
                dictionary[file.Key.Replace('\\', '/')] = file.Value;

            Files = dictionary;
        }

        public override string ToString() => Name;

        public SkinIni Ini => SkinIni.Parse(Files.FirstOrDefault(f => f.Key.Equals(@"skin.ini", StringComparison.OrdinalIgnoreCase)).Value?.Invoke());
    }

    /// <summary>
    /// Reprise: a new skin from a base skin and what's chosen for its parts (as OsuSkinMixer makes one): a part from another skin replaces
    /// the base's files for it with that skin's; the game's default removes them; blank puts empty ones in their place. skin.ini settings
    /// and sections are taken (or removed) the same way, with the files a setting names (a font's prefix) along with it.
    /// </summary>
    public static class SkinMixing
    {
        /// <summary>A 1x1 transparent image (an empty file would make the game fall back to its default instead).</summary>
        public static readonly byte[] TRANSPARENT_PNG =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x04, 0x00, 0x00, 0x00, 0xB5, 0x1C, 0x0C,
            0x02, 0x00, 0x00, 0x00, 0x0B, 0x49, 0x44, 0x41, 0x54, 0x78, 0xDA, 0x63, 0x64, 0x60, 0x00, 0x00,
            0x00, 0x06, 0x00, 0x02, 0x30, 0x81, 0xD0, 0x2F, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
            0xAE, 0x42, 0x60, 0x82
        };

        /// <summary>A silent sound (a valid wave file with no samples).</summary>
        public static readonly byte[] SILENT_WAV = createSilentWav();

        /// <summary>The new skin's files, by path (its skin.ini named <paramref name="name"/>).</summary>
        public static Dictionary<string, byte[]> Mix(SkinMixerSource baseSkin, IEnumerable<SkinMixerOption> options, string name)
        {
            var files = new Dictionary<string, Func<byte[]>>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in baseSkin.Files.Where(f => !f.Key.Equals(@"skin.ini", StringComparison.OrdinalIgnoreCase)))
                files[file.Key] = file.Value;

            var ini = baseSkin.Ini;
            var retimed = new List<(SkinMixerFileOption file, SkinMixerSource from)>();

            foreach (var option in options.SelectMany(o => o.Leaves).Where(o => o.Choice.Type != SkinMixerChoice.Kind.Keep))
            {
                switch (option)
                {
                    case SkinMixerFileOption file:
                        foreach (string path in files.Keys.Where(file.Matches).ToArray())
                            files.Remove(path);

                        if (option.Choice.Type == SkinMixerChoice.Kind.Blank)
                        {
                            foreach (string path in file.BlankFiles())
                                files[path] = file.IsAudio ? () => SILENT_WAV : () => TRANSPARENT_PNG;
                        }
                        else if (option.Choice.Skin is SkinMixerSource from)
                        {
                            foreach (var f in from.Files.Where(f => file.Matches(f.Key)))
                                files[f.Key] = f.Value;

                            if (file.IsAnimatable && TIMED_BY_FRAMERATE.Contains(file.FileName.TrimEnd('*')))
                                retimed.Add((file, from));
                        }

                        break;

                    case SkinMixerIniProperty property:
                    {
                        string? value = option.Choice.Skin?.Ini.Get(property.Section, property.Property);

                        if (value == null)
                            ini.Remove(property.Section, property.Property);
                        else
                        {
                            ini.Set(property.Section, property.Property, value);
                            copyNamedFiles(option.Choice.Skin!, property.Property, value, files);
                        }

                        break;
                    }

                    case SkinMixerIniSection section:
                        ini.Sections.RemoveAll(section.Matches);

                        if (option.Choice.Skin is SkinMixerSource sectionFrom)
                        {
                            foreach (var s in sectionFrom.Ini.Sections.Where(section.Matches))
                            {
                                ini.Sections.Add(s);

                                foreach (var (key, value) in s.Entries)
                                    copyNamedFiles(sectionFrom, key, value, files);
                            }
                        }

                        break;
                }
            }

            // Animations the skin's frame rate times, from a skin timed otherwise: their frames again, so they play as in that skin.
            foreach (var (file, from) in retimed)
                retime(file.FileName.TrimEnd('*'), from, ini, files);

            ini.Set(@"General", @"Name", name);

            // A base without a skin.ini (the game's classic skin) is read as the latest version; a skin.ini without one would be read as 1.0.
            if (ini.Get(@"General", @"Version") == null)
                ini.Set(@"General", @"Version", @"latest");

            var result = files.ToDictionary(f => f.Key, f => f.Value(), StringComparer.OrdinalIgnoreCase);
            result[@"skin.ini"] = Encoding.UTF8.GetBytes(ini.ToString());
            return result;
        }

        /// <summary>
        /// Animations the game plays at the skin's AnimationFramerate (all their frames over a second when it has none).
        /// </summary>
        public static readonly HashSet<string> TIMED_BY_FRAMERATE = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"followpoint", @"sliderfollowcircle", @"scorebar-colour", @"play-skip", @"menu-back",
        };

        // A frame's length (ms) in a skin: 1000 / its AnimationFramerate, else a second over all of the animation's frames.
        private static double frameLength(SkinIni ini, int frames) =>
            int.TryParse(ini.Get(@"General", @"AnimationFramerate"), out int rate) && rate > 0 ? 1000.0 / rate : 1000.0 / Math.Max(frames, 1);

        // The animation's frames ("name-0", "name-1"... and their @2x), from the skin it came from, as many as the new skin's frame rate
        // needs to play them as long as they played there, each frame repeated (or skipped) to fit.
        private static void retime(string name, SkinMixerSource from, SkinIni ini, Dictionary<string, Func<byte[]>> files)
        {
            var frames = new SortedDictionary<int, List<(string suffix, Func<byte[]> data)>>();

            foreach (var (path, data) in from.Files)
            {
                if (path.Contains('/'))
                    continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                string extension = Path.GetExtension(path);
                bool hd = stem.EndsWith(@"@2x", StringComparison.OrdinalIgnoreCase);
                if (hd)
                    stem = stem[..^3];

                if (!stem.StartsWith(name + "-", StringComparison.OrdinalIgnoreCase) || !int.TryParse(stem[(name.Length + 1)..], out int index) || index < 0)
                    continue;

                if (!frames.TryGetValue(index, out var list))
                    frames[index] = list = new List<(string, Func<byte[]>)>();
                list.Add(((hd ? @"@2x" : string.Empty) + extension.ToLowerInvariant(), data));
            }

            // The game takes frames from 0 up to the first missing one.
            int count = 0;
            while (frames.ContainsKey(count))
                count++;

            if (count < 2 || !int.TryParse(ini.Get(@"General", @"AnimationFramerate"), out int rate) || rate <= 0)
                return;

            double sourceLength = frameLength(from.Ini, count);
            double targetLength = 1000.0 / rate;

            if (Math.Abs(sourceLength - targetLength) < 0.5)
                return;

            int newCount = Math.Max(1, (int)Math.Round(count * sourceLength / targetLength));

            foreach (string path in files.Keys.Where(k => isFrame(k, name)).ToArray())
                files.Remove(path);

            for (int i = 0; i < newCount; i++)
            {
                int source = Math.Min(count - 1, (int)Math.Floor((i + 0.5) * targetLength / sourceLength));

                foreach (var (suffix, data) in frames[source])
                    files[$@"{name}-{i}{suffix}"] = data;
            }
        }

        private static bool isFrame(string path, string name)
        {
            if (path.Contains('/'))
                return false;

            string stem = Path.GetFileNameWithoutExtension(path);
            if (stem.EndsWith(@"@2x", StringComparison.OrdinalIgnoreCase))
                stem = stem[..^3];

            return stem.StartsWith(name + "-", StringComparison.OrdinalIgnoreCase) && int.TryParse(stem[(name.Length + 1)..], out _);
        }

        /// <summary>
        /// Whether a skin.ini setting names files (a font's prefix, a mania key's image), as OsuSkinMixer reads them.
        /// </summary>
        public static bool NamesFiles(string property) =>
            (property.StartsWith(@"Stage", StringComparison.Ordinal) && property != @"StageSeparation")
            || property.EndsWith(@"Prefix", StringComparison.Ordinal)
            || property.StartsWith(@"KeyImage", StringComparison.Ordinal)
            || property.StartsWith(@"NoteImage", StringComparison.Ordinal)
            || property is @"LightingN" or @"LightingL" or @"WarningArrow" or @"Hit0" or @"Hit50" or @"Hit100" or @"Hit200" or @"Hit300" or @"Hit300g";

        // The files a setting names (all those starting with it, in its folder), from the skin it's taken from.
        private static void copyNamedFiles(SkinMixerSource from, string property, string value, Dictionary<string, Func<byte[]>> files)
        {
            if (!NamesFiles(property) || string.IsNullOrWhiteSpace(value))
                return;

            string prefix = value.Trim().Replace('\\', '/');

            foreach (var f in from.Files.Where(f => f.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                files[f.Key] = f.Value;
        }

        private static byte[] createSilentWav()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(44100);
            writer.Write(44100 * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(0);
            writer.Flush();

            return stream.ToArray();
        }
    }

    /// <summary>
    /// Reprise: a skin.ini as sections of settings, kept in order (comments and all) so a mixed skin's reads as its base's did.
    /// </summary>
    public class SkinIni
    {
        public readonly List<Section> Sections = new List<Section>();

        public class Section
        {
            /// <summary>Its name ("Colours"; empty for the lines before the first section).</summary>
            public readonly string Name;

            /// <summary>Its lines: a setting (key and value) or anything else as it was (key null).</summary>
            public readonly List<(string? Key, string Value)> Lines = new List<(string? Key, string Value)>();

            public Section(string name)
            {
                Name = name;
            }

            public IEnumerable<(string Key, string Value)> Entries => Lines.Where(l => l.Key != null).Select(l => (l.Key!, l.Value));

            /// <summary>A setting's value (its last), or null.</summary>
            public string? Get(string key)
            {
                for (int i = Lines.Count - 1; i >= 0; i--)
                {
                    if (Lines[i].Key?.Equals(key, StringComparison.OrdinalIgnoreCase) == true)
                        return Lines[i].Value;
                }

                return null;
            }
        }

        public static SkinIni Parse(byte[]? data)
        {
            var ini = new SkinIni();

            if (data == null)
                return ini;

            var current = new Section(string.Empty);
            ini.Sections.Add(current);

            using var reader = new StreamReader(new MemoryStream(data), Encoding.UTF8, true);

            while (reader.ReadLine() is string line)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    current = new Section(trimmed[1..^1]);
                    ini.Sections.Add(current);
                    continue;
                }

                int colon = trimmed.IndexOf(':');

                if (colon > 0 && !trimmed.StartsWith(@"//", StringComparison.Ordinal))
                    current.Lines.Add((trimmed[..colon].Trim(), trimmed[(colon + 1)..].Trim()));
                else
                    current.Lines.Add((null, line));
            }

            return ini;
        }

        /// <summary>A setting's value (from the last section of that name that has it), or null.</summary>
        public string? Get(string section, string key) =>
            Sections.Where(s => s.Name.Equals(section, StringComparison.OrdinalIgnoreCase)).Select(s => s.Get(key)).LastOrDefault(v => v != null);

        /// <summary>Sets a setting (in the first section of that name, made at the end if there's none).</summary>
        public void Set(string section, string key, string value)
        {
            var target = Sections.FirstOrDefault(s => s.Name.Equals(section, StringComparison.OrdinalIgnoreCase) && s.Get(key) != null)
                         ?? Sections.FirstOrDefault(s => s.Name.Equals(section, StringComparison.OrdinalIgnoreCase));

            if (target == null)
                Sections.Add(target = new Section(section));

            int index = target.Lines.FindIndex(l => l.Key != null && l.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
                target.Lines[index] = (key, value);
            else
                target.Lines.Add((key, value));
        }

        /// <summary>Removes a setting from every section of that name (back to the game's default).</summary>
        public void Remove(string section, string key)
        {
            foreach (var s in Sections.Where(s => s.Name.Equals(section, StringComparison.OrdinalIgnoreCase)))
                s.Lines.RemoveAll(l => l.Key != null && l.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            foreach (var section in Sections)
            {
                if (section.Name.Length > 0)
                    builder.Append('[').Append(section.Name).Append(']').Append("\r\n");

                foreach (var (key, value) in section.Lines)
                    builder.Append(key == null ? value : $"{key}: {value}").Append("\r\n");
            }

            return builder.ToString();
        }
    }
}
