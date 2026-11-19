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
    /// Reprise: the skins in osu!'s own lazer on this computer (its data folder, "osu" next to ours, or where its storage.ini moved it),
    /// read from a copy of its database (osu!lazer's own is never opened, and can be running meanwhile), and each one as osu!lazer's
    /// export of it would be (its files in an .osk) to import here.
    /// </summary>
    public class LazerSkinSource
    {
        /// <summary>A skin there: its files by name, each stored under its hash.</summary>
        public record LazerSkin(Guid ID, string Name, string Creator, IReadOnlyList<(string Filename, string Hash)> Files)
        {
            public override string ToString() => string.IsNullOrEmpty(Creator) ? Name : $"{Name} ({Creator})";
        }

        /// <summary>osu!lazer's data folder.</summary>
        public readonly string DataPath;

        private LazerSkinSource(string dataPath)
        {
            DataPath = dataPath;
        }

        /// <summary>osu!lazer's data on this computer, if it's there.</summary>
        public static LazerSkinSource? Find(GameHost host)
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

                if (FromPath(path) is LazerSkinSource source)
                    return source;
            }

            return null;
        }

        /// <summary>osu!lazer's data in the given folder, if it's there.</summary>
        public static LazerSkinSource? FromPath(string path) =>
            File.Exists(Path.Combine(path, @"client.realm")) && Directory.Exists(Path.Combine(path, @"files")) ? new LazerSkinSource(path) : null;

        /// <summary>
        /// Its skins (not osu!'s built-in ones, nor deleted ones), by name. Reads a copy of its database, so it can take a moment.
        /// </summary>
        public IReadOnlyList<LazerSkin> ReadSkins()
        {
            // In a folder of its own, gone afterwards with what Realm leaves next to it (its lock file).
            string folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"reprise-lazer-skins-{Guid.NewGuid():N}")).FullName;
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
                    if (!realm.Schema.TryFindObjectSchema(@"Skin", out _))
                        return Array.Empty<LazerSkin>();

                    var skins = new List<LazerSkin>();

                    foreach (var skin in realm.DynamicApi.All(@"Skin"))
                    {
                        if (skin.DynamicApi.Get<bool>(@"Protected") || skin.DynamicApi.Get<bool>(@"DeletePending"))
                            continue;

                        var files = skin.DynamicApi.GetList<IEmbeddedObject>(@"Files")
                                        .Select(f => (f.DynamicApi.Get<string>(@"Filename"), f.DynamicApi.Get<IRealmObject>(@"File").DynamicApi.Get<string>(@"Hash")))
                                        .ToList();

                        if (files.Count == 0)
                            continue;

                        skins.Add(new LazerSkin(skin.DynamicApi.Get<Guid>(@"ID"), skin.DynamicApi.Get<string>(@"Name") ?? string.Empty,
                            skin.DynamicApi.Get<string>(@"Creator") ?? string.Empty, files));
                    }

                    return skins.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
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
        public string WriteOsk(LazerSkin skin, string folder)
        {
            // Named for the skin (the import's name for it without a skin.ini), each in its own folder as names can repeat.
            string name = string.Join('_', skin.Name.Split(Path.GetInvalidFileNameChars())).Trim();
            string oskPath = Path.Combine(Directory.CreateDirectory(Path.Combine(folder, skin.ID.ToString(@"N"))).FullName,
                $@"{(string.IsNullOrEmpty(name) ? @"skin" : name)}.osk");

            using (var zip = ZipFile.Open(oskPath, ZipArchiveMode.Create))
            {
                foreach (var (filename, hash) in skin.Files)
                {
                    if (hash.Length < 2)
                        continue;

                    string path = Path.Combine(DataPath, @"files", hash.Remove(1), hash.Remove(2), hash);

                    if (!File.Exists(path))
                        continue;

                    var entry = zip.CreateEntry(filename.Replace('\\', '/'), CompressionLevel.NoCompression);

                    using (var entryStream = entry.Open())
                    using (var file = File.OpenRead(path))
                        file.CopyTo(entryStream);
                }
            }

            return oskPath;
        }
    }
}
