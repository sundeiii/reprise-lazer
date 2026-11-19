// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Tests.Database
{
    /// <summary>
    /// Reprise: osu!lazer's skins, beatmaps and collections read from its data folder (a copy of its database), and a skin and beatmap as
    /// archives.
    /// </summary>
    [TestFixture]
    public class LazerDataSourceTest
    {
        private string dataPath = null!;

        private const string skin_hash = "abcdef0123456789";
        private const string beatmap_hash = "bcdef0123456789a";

        [SetUp]
        public void SetUp()
        {
            dataPath = Path.Combine(Path.GetTempPath(), $"lazer-data-source-test-{Guid.NewGuid():N}");
            CreateLazerData(dataPath);
        }

        /// <summary>A small osu!lazer data folder: a skin, a beatmap set and a collection (and a deleted or built-in one of each).</summary>
        public static void CreateLazerData(string dataPath)
        {
            // Written once per test run and copied (realm trips over these written over and over in one process).
            foreach (string file in Directory.GetFiles(template.Value, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) != "client.realm" && !Path.GetRelativePath(template.Value, file).StartsWith("files", StringComparison.Ordinal))
                    continue;

                string destination = Path.Combine(dataPath, Path.GetRelativePath(template.Value, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
        }

        private static readonly Lazy<string> template = new Lazy<string>(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), $"lazer-data-template-{Guid.NewGuid():N}");
            writeLazerData(path);
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch
                {
                    // A leftover in the temporary folder.
                }
            };
            return path;
        });

        private static void writeLazerData(string dataPath)
        {
            Directory.CreateDirectory(dataPath);

            writeFile(dataPath, skin_hash, "[General]\nName: Imported\n");
            writeFile(dataPath, beatmap_hash, "osu file format v14\n");

            // osu!lazer's schema (ours) for these, as its database has it.
            var config = new RealmConfiguration(Path.Combine(dataPath, "client.realm"))
            {
                Schema = new[]
                {
                    typeof(SkinInfo), typeof(RealmNamedFileUsage), typeof(RealmFile), typeof(BeatmapSetInfo), typeof(BeatmapInfo), typeof(BeatmapMetadata),
                    typeof(BeatmapDifficulty), typeof(BeatmapUserSettings), typeof(RealmUser), typeof(RulesetInfo), typeof(BeatmapCollection), typeof(ScoreInfo),
                },
            };

            using (var realm = Realm.GetInstance(config))
            {
                realm.Write(() =>
                {
                    var skinFile = realm.Add(new RealmFile { Hash = skin_hash });
                    var beatmapFile = realm.Add(new RealmFile { Hash = beatmap_hash });

                    var imported = new SkinInfo("Imported", "Someone");
                    imported.Files.Add(new RealmNamedFileUsage(skinFile, "skin.ini"));
                    realm.Add(imported);

                    var deleted = new SkinInfo("Deleted", "Someone") { DeletePending = true };
                    deleted.Files.Add(new RealmNamedFileUsage(skinFile, "skin.ini"));
                    realm.Add(deleted);

                    realm.Add(new SkinInfo("Built in", "osu!") { Protected = true });

                    var ruleset = realm.Add(new RulesetInfo("osu", "osu!", string.Empty, 0) { Available = true });
                    var metadata = new BeatmapMetadata(new RealmUser { Username = "Mapper" }) { Artist = "Artist", Title = "Song" };

                    var set = new BeatmapSetInfo
                    {
                        OnlineID = 123,
                        Beatmaps =
                        {
                            new BeatmapInfo(ruleset, new BeatmapDifficulty(), metadata) { MD5Hash = "easy" },
                            new BeatmapInfo(ruleset, new BeatmapDifficulty(), metadata) { MD5Hash = "hard" },
                        }
                    };
                    set.Files.Add(new RealmNamedFileUsage(beatmapFile, "Artist - Song (Mapper) [Easy].osu"));
                    realm.Add(set);

                    var deletedSet = new BeatmapSetInfo { DeletePending = true, Beatmaps = { new BeatmapInfo(ruleset, new BeatmapDifficulty(), metadata) } };
                    deletedSet.Files.Add(new RealmNamedFileUsage(beatmapFile, "deleted.osu"));
                    realm.Add(deletedSet);

                    realm.Add(new BeatmapCollection("Favourites", new[] { "easy", "hard", "easy" }.ToList()));
                    realm.Add(new BeatmapCollection("Empty"));
                });
            }
        }

        private static void writeFile(string dataPath, string hash, string content)
        {
            string filePath = Path.Combine(dataPath, "files", hash.Remove(1), hash.Remove(2), hash);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(dataPath, true);

        [Test]
        public void TestSkins()
        {
            var source = LazerDataSource.FromPath(dataPath);
            Assert.That(source, Is.Not.Null);

            var skins = source!.ReadSkins();
            Assert.That(skins.Select(s => s.Name), Is.EqualTo(new[] { "Imported" }));
            Assert.That(skins[0].Creator, Is.EqualTo("Someone"));

            string osk = source.WriteOsk(skins[0], Path.Combine(dataPath, "out"));

            Assert.That(Path.GetFileName(osk), Is.EqualTo("Imported.osk"));

            using (var zip = ZipFile.OpenRead(osk))
            {
                Assert.That(zip.Entries.Select(e => e.FullName), Is.EqualTo(new[] { "skin.ini" }));

                using (var reader = new StreamReader(zip.Entries[0].Open()))
                    Assert.That(reader.ReadToEnd(), Does.Contain("Name: Imported"));
            }
        }

        [Test]
        public void TestBeatmaps()
        {
            var source = LazerDataSource.FromPath(dataPath)!;

            var sets = source.ReadBeatmapSets();
            Assert.That(sets, Has.Count.EqualTo(1));
            Assert.That(sets[0].ToString(), Is.EqualTo("Artist - Song (Mapper)"));
            Assert.That(sets[0].OnlineID, Is.EqualTo(123));
            Assert.That(sets[0].MD5Hashes, Is.EquivalentTo(new[] { "easy", "hard" }));

            string osz = source.WriteOsz(sets[0], Path.Combine(dataPath, "out"));

            Assert.That(Path.GetFileName(osz), Is.EqualTo("123 Artist - Song.osz"));

            using (var zip = ZipFile.OpenRead(osz))
                Assert.That(zip.Entries.Select(e => e.FullName), Is.EqualTo(new[] { "Artist - Song (Mapper) [Easy].osu" }));
        }

        [Test]
        public void TestCollections()
        {
            var collections = LazerDataSource.FromPath(dataPath)!.ReadCollections();

            Assert.That(collections.Select(c => c.Name), Is.EqualTo(new[] { "Favourites" }));
            Assert.That(collections[0].MD5Hashes, Is.EqualTo(new[] { "easy", "hard" }));
        }

        [Test]
        public void TestNoData() => Assert.That(LazerDataSource.FromPath(Path.Combine(dataPath, "nothing")), Is.Null);
    }
}
