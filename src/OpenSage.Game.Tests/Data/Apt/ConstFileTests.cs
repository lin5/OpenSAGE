﻿using System.IO;
using OpenSage.Data;
using OpenSage.Data.Apt;
using OpenSage.Data.Big;
using Xunit;
using Xunit.Abstractions;

namespace OpenSage.Tests.Data.Apt
{
    public class ConstFileTests
    {
        private readonly ITestOutputHelper _output;

        public ConstFileTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [GameFact(SageGame.BattleForMiddleEarth, SageGame.BattleForMiddleEarthII)]
        public void CanReadConstFiles()
        {
            InstalledFilesTestData.ReadFiles(".const", _output, entry =>
            {
                var constFile = ConstantData.FromFileSystemEntry(entry);

                Assert.NotNull(constFile);
            });
        }

        [GameFact(SageGame.BattleForMiddleEarthII)]
        public void CheckEntryCount()
        {
            var bigFilePath = Path.Combine(InstalledFilesTestData.GetInstallationDirectory(SageGame.BattleForMiddleEarthII), "apt/MainMenu.big");

            using (var bigArchive = new BigArchive(bigFilePath))
            {
                var entry = bigArchive.GetEntry(@"MainMenu.const");

                var data = ConstantData.FromFileSystemEntry(new FileSystemEntry(null, entry.FullName, entry.Length, entry.Open));
                Assert.NotNull(data);

                //requires unmodified main menu
                Assert.Equal(412, data.Entries.Count);
            }
        }
    }
}
