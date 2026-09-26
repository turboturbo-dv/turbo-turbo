using System;
using System.IO;

using Shouldly;

using TurboTurbo.Profiles;

using Xunit;

namespace TurboTurboTests
{
    public class ProfileWriterTests : IDisposable
    {
        private readonly string _dir;

        public ProfileWriterTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "turboturbo-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static LocoProfile Profile(string liveryId, string path)
        {
            return new LocoProfile
            {
                LiveryId = liveryId,
                Exhausts = [LocoExhaust.Replacement(path)],
            };
        }

        private ProfileLoader.ModProfile Load(string liveryId)
        {
            var source = new ProfileLoader.ModSource("mod-a", "Mod A", true, _dir);
            return ProfileLoader.LoadModProfile(source, "turbo")[liveryId];
        }

        [Fact]
        public void Write_CreatesFile_AndRoundTrips()
        {
            var profile = Profile("loco-a", "Engine/ExhaustEngineSmoke");

            ProfileWriter.Write(profile, _dir).ShouldBeNull();

            File.Exists(Path.Combine(_dir, ProfileWriter.ConfigFileName)).ShouldBeTrue();

            var loaded = Load("loco-a");
            loaded.Profile.Exhausts[0].Path.ShouldBe("Engine/ExhaustEngineSmoke");
        }

        [Fact]
        public void Write_Upserts_PreservingOtherProfiles()
        {
            ProfileWriter.Write(Profile("loco-a", "ExhaustA"), _dir).ShouldBeNull();
            ProfileWriter.Write(Profile("loco-b", "ExhaustB"), _dir).ShouldBeNull();

            var source = new ProfileLoader.ModSource("mod-a", "Mod A", true, _dir);
            var loaded = ProfileLoader.LoadModProfile(source, "turbo");

            loaded.Count.ShouldBe(2);
            loaded["loco-a"].Profile.Exhausts[0].Path.ShouldBe("ExhaustA");
            loaded["loco-b"].Profile.Exhausts[0].Path.ShouldBe("ExhaustB");
        }

        [Fact]
        public void Write_ReplacesExistingLivery()
        {
            ProfileWriter.Write(Profile("loco-a", "OldPath"), _dir).ShouldBeNull();
            ProfileWriter.Write(Profile("loco-a", "NewPath"), _dir).ShouldBeNull();

            var source = new ProfileLoader.ModSource("mod-a", "Mod A", true, _dir);
            var loaded = ProfileLoader.LoadModProfile(source, "turbo");

            loaded.Count.ShouldBe(1);
            loaded["loco-a"].Profile.Exhausts[0].Path.ShouldBe("NewPath");
        }

        [Fact]
        public void Delete_RemovesOnlyTheGivenLivery()
        {
            ProfileWriter.Write(Profile("loco-a", "ExhaustA"), _dir).ShouldBeNull();
            ProfileWriter.Write(Profile("loco-b", "ExhaustB"), _dir).ShouldBeNull();

            ProfileWriter.Delete("loco-a", _dir).ShouldBeNull();

            var source = new ProfileLoader.ModSource("mod-a", "Mod A", true, _dir);
            var loaded = ProfileLoader.LoadModProfile(source, "turbo");
            loaded.ContainsKey("loco-a").ShouldBeFalse();
            loaded.ContainsKey("loco-b").ShouldBeTrue();
        }

        [Fact]
        public void Delete_MissingLivery_IsNoOp()
        {
            ProfileWriter.Write(Profile("loco-a", "ExhaustA"), _dir).ShouldBeNull();

            ProfileWriter.Delete("missing", _dir).ShouldBeNull();
        }

        [Fact]
        public void Delete_WithoutFile_ReturnsError()
        {
            ProfileWriter.Delete("loco-a", _dir).ShouldNotBeNull();
        }

        [Fact]
        public void Write_IsIndentedAndBomlessUtf8()
        {
            ProfileWriter.Write(Profile("loco-a", "ExhaustA"), _dir).ShouldBeNull();

            var file = Path.Combine(_dir, ProfileWriter.ConfigFileName);

            File.ReadAllBytes(file)[0].ShouldBe((byte)'<');

            var text = File.ReadAllText(file);
            text.ShouldContain("\n");
            text.ShouldContain("  <LocoProfiles>");
            text.ShouldContain("    <LocoProfile>");
        }

        [Fact]
        public void SerializeFragment_IsAParseableLocoProfile()
        {
            var xml = ProfileWriter.SerializeFragment(Profile("loco-a", "ExhaustA"));

            xml.ShouldStartWith("<LocoProfile");
            xml.ShouldContain("<LiveryId>loco-a</LiveryId>");
            xml.ShouldContain("<Path>ExhaustA</Path>");
            xml.ShouldNotContain("<TurboConfig");
            xml.ShouldNotContain("xmlns:xsd");
        }
    }
}
