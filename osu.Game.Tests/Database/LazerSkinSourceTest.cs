// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Tests.Database
{
    /// <summary>
    /// Reprise: osu!lazer's skins read from its data folder (a copy of its database), and one as an .osk.
    /// </summary>
    [TestFixture]
    public class LazerSkinSourceTest
    {
        private string dataPath = null!;

        [SetUp]
        public void SetUp()
        {
            dataPath = Path.Combine(Path.GetTempPath(), $"lazer-skin-source-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dataPath);

            const string hash = "abcdef0123456789";
            string filePath = Path.Combine(dataPath, "files", "a", "ab", hash);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "[General]\nName: Imported\n", Encoding.UTF8);

            var config = new RealmConfiguration(Path.Combine(dataPath, "client.realm"))
            {
                Schema = new[] { typeof(SkinInfo), typeof(RealmNamedFileUsage), typeof(RealmFile) },
            };

            using (var realm = Realm.GetInstance(config))
            {
                realm.Write(() =>
                {
                    var file = realm.Add(new RealmFile { Hash = hash });

                    var imported = new SkinInfo("Imported", "Someone");
                    imported.Files.Add(new RealmNamedFileUsage(file, "skin.ini"));
                    realm.Add(imported);

                    var deleted = new SkinInfo("Deleted", "Someone") { DeletePending = true };
                    deleted.Files.Add(new RealmNamedFileUsage(file, "skin.ini"));
                    realm.Add(deleted);

                    realm.Add(new SkinInfo("Built in", "osu!") { Protected = true });
                });
            }
        }

        [TearDown]
        public void TearDown() => Directory.Delete(dataPath, true);

        [Test]
        public void TestReadAndWrite()
        {
            var source = LazerSkinSource.FromPath(dataPath);
            Assert.That(source, Is.Not.Null);

            var skins = source!.ReadSkins();
            Assert.That(skins.Select(s => s.Name), Is.EqualTo(new[] { "Imported" }));
            Assert.That(skins[0].Creator, Is.EqualTo("Someone"));

            string outFolder = Path.Combine(dataPath, "out");
            string osk = source.WriteOsk(skins[0], outFolder);

            Assert.That(Path.GetFileName(osk), Is.EqualTo("Imported.osk"));

            using (var zip = ZipFile.OpenRead(osk))
            {
                Assert.That(zip.Entries.Select(e => e.FullName), Is.EqualTo(new[] { "skin.ini" }));

                using (var reader = new StreamReader(zip.Entries[0].Open()))
                    Assert.That(reader.ReadToEnd(), Does.Contain("Name: Imported"));
            }
        }

        [Test]
        public void TestNoData() => Assert.That(LazerSkinSource.FromPath(Path.Combine(dataPath, "nothing")), Is.Null);
    }
}
