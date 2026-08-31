using System;
using System.IO;
using ES2Access.Core.Settings;
using Xunit;

namespace ES2Access.Tests.Settings
{
    public class SettingsFileOnDiskTests : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "es2access-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, true);
                }
            }
            catch (IOException)
            {
                // A test that deliberately left a file open; the temp directory can outlive us.
            }
        }

        private string In(string name)
        {
            return Path.Combine(_directory, name);
        }

        [Fact]
        public void AFileThatIsNotThereReadsAsAnEmptyOne()
        {
            SettingsFile file = SettingsFileOnDisk.Read(In("nothing.cfg"), "settings");

            Assert.Empty(file.ToLines());
            Assert.Null(file.Get("a"));
        }

        [Fact]
        public void AFileThatCannotBeReadReadsAsAnEmptyOneRatherThanThrowing()
        {
            Directory.CreateDirectory(_directory);
            string path = In("locked.cfg");
            File.WriteAllText(path, "a = 1\n");

            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                SettingsFile file = SettingsFileOnDisk.Read(path, "settings");
                Assert.Null(file.Get("a"));
            }
        }

        [Fact]
        public void WhatIsWrittenIsWhatIsReadBackAndTheDirectoryIsMadeForIt()
        {
            string path = In(Path.Combine("bookmarks", "campaign.cfg"));
            SettingsFile written = SettingsFile.Parse(new[] { "# mine", "slot1 = 5,1,2" });

            Assert.True(SettingsFileOnDisk.Write(path, written, "bookmarks"));

            SettingsFile back = SettingsFileOnDisk.Read(path, "bookmarks");
            Assert.Equal("5,1,2", back.Get("slot1"));
            Assert.Equal("# mine", back.ToLines()[0]);
        }

        [Fact]
        public void AWriteThatCannotLandAnswersFalseRatherThanThrowing()
        {
            Directory.CreateDirectory(_directory);
            string inTheWay = In("file.cfg");
            File.WriteAllText(inTheWay, "a = 1\n");

            Assert.False(
                SettingsFileOnDisk.Write(Path.Combine(inTheWay, "settings.cfg"),
                                         new SettingsFile(), "settings"));
        }
    }
}
