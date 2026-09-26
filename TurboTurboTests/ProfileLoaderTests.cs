using System;
using System.Collections.Generic;
using System.IO;

using Shouldly;

using TurboTurbo.Profiles;

using Xunit;

using static TurboTurbo.Profiles.ProfileLoader;

namespace TurboTurboTests
{
    public class ProfileLoaderTests : IDisposable
    {
        private const string ValidXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>" +
            @"<TurboConfig>" +
            @"<LocoProfiles><LocoProfile>" +
            @"<Version>1</Version><LiveryId>test-livery</LiveryId><Enabled>true</Enabled>" +
            @"<Exhausts><LocoExhaust><Kind>Replacement</Kind><Path>ExhaustEngineSmoke(Clone)</Path></LocoExhaust></Exhausts>" +
            @"<ChargerKind>Turbo</ChargerKind>" +
            @"<TurboCharger><LambdaCalibration>1.6</LambdaCalibration></TurboCharger>" +
            @"<Velocity><Idle>4.0</Idle></Velocity>" +
            @"</LocoProfile></LocoProfiles>" +
            @"</TurboConfig>";

        private readonly List<string> _dirs = new();

        public void Dispose()
        {
            foreach (var dir in _dirs)
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void LoadModProfiles_ValidFile_Loads()
        {
            var mods = new List<ModSource> { Mod("mod-a", ValidXml) };

            var loaded = LoadModProfiles(mods, "own");

            loaded.Count.ShouldBe(1);
            loaded["test-livery"].ModName.ShouldBe("mod-a");
            var profile = loaded["test-livery"].Profile;
            profile.LiveryId.ShouldBe("test-livery");
            profile.TurboCharger.LambdaCalibration.ShouldBe(1.6f);
            profile.Validate().ShouldBeNull();
        }

        [Fact]
        public void LoadModProfiles_SkipsSelfDisabledMissingAndMalformed()
        {
            var mods = new List<ModSource>
            {
                Mod("own", ValidXml.Replace("test-livery", "self")),
                Mod("disabled", ValidXml.Replace("test-livery", "off"), enabled: false),
                new ModSource("nodir", "No Dir", true, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())),
                Mod("malformed", "not xml at all"),
            };

            LoadModProfiles(mods, "own").ShouldBeEmpty();
        }

        [Fact]
        public void LoadModProfiles_Conflict_LastWins()
        {
            var mods = new List<ModSource>
            {
                Mod("mod-a", ValidXml),
                Mod("mod-b", ValidXml.Replace("1.6", "1.9")),
            };

            var loaded = LoadModProfiles(mods, "own");

            loaded["test-livery"].Profile.TurboCharger.LambdaCalibration.ShouldBe(1.9f);
            loaded["test-livery"].ModName.ShouldBe("mod-b");
        }

        [Fact]
        public void LoadModProfiles_SkipsInvalid()
        {
            var mods = new List<ModSource> { Mod("mod-a", ValidXml.Replace("<LiveryId>test-livery</LiveryId>", "<LiveryId></LiveryId>")) };

            LoadModProfiles(mods, "own").ShouldBeEmpty();
        }

        [Fact]
        public void LoadUserProfiles_LoadsValidAndSkipsInvalid()
        {
            var bad = ValidLocoProfile();
            bad.LiveryId = "";

            var loaded = LoadUserProfiles([ValidLocoProfile(), bad, null]);

            loaded.Count.ShouldBe(1);
            loaded["test-livery"].LiveryId.ShouldBe("test-livery");
        }

        [Fact]
        public void LoadUserProfiles_NullList_LoadsNothing()
        {
            LoadUserProfiles(null).ShouldBeEmpty();
        }

        private static LocoProfile ValidLocoProfile()
        {
            return new LocoProfile
            {
                LiveryId = "test-livery",
                Exhausts = [new LocoExhaust { Kind = ExhaustKind.Replacement, Path = "ExhaustEngineSmoke(Clone)" }],
            };
        }

        private ModSource Mod(string id, string fileContent, bool enabled = true)
        {
            var dir = Path.Combine(Path.GetTempPath(), $"turboturbo-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(dir);
            _dirs.Add(dir);
            File.WriteAllText(Path.Combine(dir, "TurboConfig.xml"), fileContent);
            return new ModSource(id, id, enabled, dir + Path.DirectorySeparatorChar);
        }
    }
}
