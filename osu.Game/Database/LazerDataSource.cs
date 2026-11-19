// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using osu.Framework.Platform;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Reprise: osu!'s own lazer on this computer (its data folder, "osu" next to ours, or where its storage.ini moved it): its skins,
    /// beatmaps and collections, read from a copy of its database (osu!lazer's own is never opened, and can be running meanwhile), and a
    /// skin or beatmap as osu!lazer's export of it would be (its files in an .osk or .osz) to import here.
    /// </summary>
    public class LazerDataSource
    {
        /// <summary>A skin there: its files by name, each stored under its hash.</summary>
        public record LazerSkin(Guid ID, string Name, string Creator, IReadOnlyList<(string Filename, string Hash)> Files)
        {
            public override string ToString() => string.IsNullOrEmpty(Creator) ? Name : $"{Name} ({Creator})";
        }

        /// <summary>A beatmap set there: what it is, its difficulties' hashes (to know whether it's here), and its files.</summary>
        public record LazerBeatmapSet(Guid ID, int OnlineID, string Artist, string Title, string Mapper, IReadOnlyList<string> MD5Hashes,
                                      IReadOnlyList<(string Filename, string Hash)> Files)
        {
            /// <summary>"Artist - Title", or whichever it has (a set made without them: "Untitled").</summary>
            public string DisplayTitle => string.Join(" - ", new[] { Artist.Trim(), Title.Trim() }.Where(s => s.Length > 0)) is string t && t.Length > 0 ? t : "Untitled";

            public override string ToString() => string.IsNullOrEmpty(Mapper) ? DisplayTitle : $"{DisplayTitle} ({Mapper})";
        }

        /// <summary>A collection there: its name and its beatmaps (by their hashes, as ours keeps them).</summary>
        public record LazerCollection(Guid ID, string Name, IReadOnlyList<string> MD5Hashes)
        {
            public override string ToString() => Name;
        }

        /// <summary>osu!lazer's data folder.</summary>
        public readonly string DataPath;

        private LazerDataSource(string dataPath)
        {
            DataPath = dataPath;
        }

        /// <summary>osu!lazer's data on this computer, if it's there.</summary>
        public static LazerDataSource? Find(GameHost host)
        {
            foreach (string basePath in host.UserStoragePaths)
            {
                string path = Path.Combine(basePath, @"osu");

                // Moved elsewhere (its settings' "data location"): storage.ini in the default folder says where.
                string storageIni = Path.Combine(path, @"storage.ini");

                if (File.Exists(storageIni))
                {
                    string? fullPath = File.ReadLines(storageIni)
                                           .Select(l => l.Split('=', 2))
                                           .Where(p => p.Length == 2 && p[0].Trim() == @"FullPath")
                                           .Select(p => p[1].Trim())
                                           .FirstOrDefault();

                    if (!string.IsNullOrEmpty(fullPath))
                        path = fullPath;
                }

                if (FromPath(path) is LazerDataSource source)
                    return source;
            }

            return null;
        }

        /// <summary>osu!lazer's data in the given folder, if it's there.</summary>
        public static LazerDataSource? FromPath(string path) =>
            File.Exists(Path.Combine(path, @"client.realm")) && Directory.Exists(Path.Combine(path, @"files")) ? new LazerDataSource(path) : null;

        /// <summary>
        /// Its skins (not osu!'s built-in ones, nor deleted ones), by name. Reads a copy of its database, so it can take a moment.
        /// </summary>
        public IReadOnlyList<LazerSkin> ReadSkins() => read(@"Skin", skin =>
        {
            if (skin.DynamicApi.Get<bool>(@"Protected") || skin.DynamicApi.Get<bool>(@"DeletePending"))
                return null;

            var files = readFiles(skin);

            if (files.Count == 0)
                return null;

            return new LazerSkin(skin.DynamicApi.Get<Guid>(@"ID"), skin.DynamicApi.Get<string>(@"Name") ?? string.Empty,
                skin.DynamicApi.Get<string>(@"Creator") ?? string.Empty, files);
        }, s => s.Name);

        /// <summary>
        /// Its beatmap sets (not osu!'s bundled ones, nor deleted ones), by artist and title. Reads a copy of its database, so it can take a moment.
        /// </summary>
        public IReadOnlyList<LazerBeatmapSet> ReadBeatmapSets() => read(@"BeatmapSet", set =>
        {
            if (set.DynamicApi.Get<bool>(@"Protected") || set.DynamicApi.Get<bool>(@"DeletePending"))
                return null;

            var files = readFiles(set);
            var beatmaps = set.DynamicApi.GetList<IRealmObject>(@"Beatmaps").ToList();

            if (files.Count == 0 || beatmaps.Count == 0)
                return null;

            // The set's metadata is its first difficulty's (as osu!lazer shows it).
            var metadata = beatmaps[0].DynamicApi.Get<IRealmObject>(@"Metadata");
            var author = metadata?.DynamicApi.Get<IEmbeddedObject>(@"Author");

            return new LazerBeatmapSet(set.DynamicApi.Get<Guid>(@"ID"), set.DynamicApi.Get<int>(@"OnlineID"),
                metadata?.DynamicApi.Get<string>(@"Artist") ?? string.Empty,
                metadata?.DynamicApi.Get<string>(@"Title") ?? string.Empty,
                author?.DynamicApi.Get<string>(@"Username") ?? string.Empty,
                beatmaps.Select(b => b.DynamicApi.Get<string>(@"MD5Hash") ?? string.Empty).Where(h => h.Length > 0).ToList(),
                files);
        }, s => s.DisplayTitle);

        /// <summary>Its collections (with at least a beatmap), by name. Reads a copy of its database, so it can take a moment.</summary>
        public IReadOnlyList<LazerCollection> ReadCollections() => read(@"BeatmapCollection", collection =>
        {
            var hashes = collection.DynamicApi.GetList<string>(@"BeatmapMD5Hashes").Where(h => !string.IsNullOrEmpty(h)).Distinct().ToList();

            if (hashes.Count == 0)
                return null;

            return new LazerCollection(collection.DynamicApi.Get<Guid>(@"ID"), collection.DynamicApi.Get<string>(@"Name") ?? string.Empty, hashes);
        }, c => c.Name);

        private static List<(string Filename, string Hash)> readFiles(IRealmObject model) =>
            model.DynamicApi.GetList<IEmbeddedObject>(@"Files")
                 .Select(f => (Filename: f.DynamicApi.Get<string>(@"Filename") ?? string.Empty,
                     Hash: f.DynamicApi.Get<IRealmObject>(@"File")?.DynamicApi.Get<string>(@"Hash") ?? string.Empty))
                 .Where(f => f.Filename.Length > 0 && f.Hash.Length > 0)
                 .ToList();

        /// <summary>The given type's objects in a copy of osu!lazer's database, each as <paramref name="create"/> makes it (or left out), by name.</summary>
        private IReadOnlyList<T> read<T>(string type, Func<IRealmObject, T?> create, Func<T, string> sortName)
            where T : class
        {
            // In a folder of its own, gone afterwards with what Realm leaves next to it (its lock file).
            string folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"reprise-lazer-data-{Guid.NewGuid():N}")).FullName;
            string copy = Path.Combine(folder, @"client.realm");

            try
            {
                // Shared, as osu!lazer may have it open.
                using (var source = new FileStream(Path.Combine(DataPath, @"client.realm"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var destination = File.Create(copy))
                    source.CopyTo(destination);

                // Dynamic: by whatever its schema is (osu!lazer's version can be ahead of ours or behind).
                using (var realm = Realm.GetInstance(new RealmConfiguration(copy) { IsDynamic = true }))
                {
                    if (!realm.Schema.TryFindObjectSchema(type, out _))
                        return Array.Empty<T>();

                    var found = new List<T>();

                    foreach (var obj in realm.DynamicApi.All(type))
                    {
                        if (create(obj) is T item)
                            found.Add(item);
                    }

                    return found.OrderBy(sortName, StringComparer.OrdinalIgnoreCase).ToList();
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch
                {
                    // A leftover in the temporary folder.
                }
            }
        }

        /// <summary>
        /// The skin as an .osk (as osu!lazer exports it) in a folder of its own in the given one, to import (the import deletes it after).
        /// Files missing from osu!lazer's folder are left out.
        /// </summary>
        /// <returns>The .osk's path.</returns>
        public string WriteOsk(LazerSkin skin, string folder) => writeArchive(skin.ID, skin.Name, skin.Files, folder, @"osk");

        /// <summary>
        /// The beatmap set as an .osz (as osu!lazer exports it) in a folder of its own in the given one, to import (the import deletes it after).
        /// Files missing from osu!lazer's folder are left out.
        /// </summary>
        /// <returns>The .osz's path.</returns>
        public string WriteOsz(LazerBeatmapSet set, string folder) =>
            writeArchive(set.ID, set.OnlineID > 0 ? $@"{set.OnlineID} {set.DisplayTitle}" : set.DisplayTitle, set.Files, folder, @"osz");

        private string writeArchive(Guid id, string name, IReadOnlyList<(string Filename, string Hash)> files, string folder, string extension)
        {
            // Named for what it is, each in a folder of its own as names can repeat.
            name = string.Join('_', name.Split(Path.GetInvalidFileNameChars())).Trim();
            string path = Path.Combine(Directory.CreateDirectory(Path.Combine(folder, id.ToString(@"N"))).FullName,
                $@"{(string.IsNullOrEmpty(name) ? extension : name)}.{extension}");

            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (var (filename, hash) in files)
                {
                    if (hash.Length < 2)
                        continue;

                    string filePath = Path.Combine(DataPath, @"files", hash.Remove(1), hash.Remove(2), hash);

                    if (!File.Exists(filePath))
                        continue;

                    var entry = zip.CreateEntry(filename.Replace('\\', '/'), CompressionLevel.NoCompression);

                    using (var entryStream = entry.Open())
                    using (var file = File.OpenRead(filePath))
                        file.CopyTo(entryStream);
                }
            }

            return path;
        }
    }
}
