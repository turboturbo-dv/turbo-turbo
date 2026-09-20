using System.IO;
using System.Xml.Serialization;

using Shouldly;

using TurboTurbo;
using TurboTurbo.Configuration;
using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.Setup;
using TurboTurbo.WorkBench;

using UnityEngine;

using Xunit;

namespace TurboTurboTests
{
    public class LocoProfileTests
    {
        private static LocoProfile ValidProfile(string liveryId = "test-loco")
        {
            return new LocoProfile
            {
                LiveryId = liveryId,
                Exhausts =
                [
                    new LocoExhaust { Kind = ExhaustKind.Replacement, Name = "ExhaustSmoke", Offset = new Vector3(0f, 0.1f, 0f) },
                    new LocoExhaust { Kind = ExhaustKind.Independent, Offset = new Vector3(1f, 2f, 3f) },
                ],
            };
        }

        [Fact]
        public void Validate_MinimalProfile_Passes()
        {
            ValidProfile().Validate().ShouldBeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Validate_BadLiveryId_Fails(string liveryId)
        {
            var profile = ValidProfile();
            profile.LiveryId = liveryId;

            profile.Validate().ShouldNotBeNull();
        }

        [Fact]
        public void Validate_NoExhausts_Fails()
        {
            var profile = ValidProfile();
            profile.Exhausts.Clear();

            profile.Validate().ShouldNotBeNull();
        }

        [Fact]
        public void Validate_NullExhausts_Fails()
        {
            var profile = ValidProfile();
            profile.Exhausts = null;

            profile.Validate().ShouldNotBeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_ReplacementWithoutName_Fails(string name)
        {
            var profile = ValidProfile();
            profile.Exhausts[0].Name = name;

            profile.Validate().ShouldNotBeNull();
        }

        [Fact]
        public void Validate_IndependentWithoutName_Passes()
        {
            var profile = ValidProfile();
            profile.Exhausts[1].Name = "";

            profile.Validate().ShouldBeNull();
        }

        [Fact]
        public void Validate_NonFiniteOffset_Fails()
        {
            var profile = ValidProfile();
            profile.Exhausts[0].Offset = new Vector3(float.NaN, 0f, 0f);

            profile.Validate().ShouldNotBeNull();

            profile.Exhausts[0].Offset = new Vector3(0f, float.PositiveInfinity, 0f);

            profile.Validate().ShouldNotBeNull();
        }

        [Fact]
        public void Validate_UnknownVersion_Fails()
        {
            var profile = ValidProfile();
            profile.Version = LocoProfile.CurrentVersion + 1;

            profile.Validate().ShouldNotBeNull();
        }

        [Fact]
        public void Validate_UnknownChargerKind_Fails()
        {
            var profile = ValidProfile();
            profile.ChargerKind = (ChargerKind)42;

            profile.Validate().ShouldNotBeNull();
        }

        [Fact]
        public void Validate_UnknownExhaustKind_Fails()
        {
            var profile = ValidProfile();
            profile.Exhausts[0].Kind = (ExhaustKind)42;

            profile.Validate().ShouldNotBeNull();
        }

        [Fact]
        public void Build_ProducesCompleteValidatedProfile()
        {
            var config = new EngineOptions()
                .AddEngineExhaust()
                .TryBuild("test-livery");

            config.LiveryId.ShouldBe("test-livery");
            config.ChargerKind.ShouldBe(ChargerKind.Turbo);
            config.Exhausts.Count.ShouldBe(1);
            config.Combustion.ShouldNotBeNull();
            config.Smoke.ShouldNotBeNull();
            config.SmokeEmitter.ShouldNotBeNull();
            config.ShimmerEmitter.ShouldNotBeNull();
            config.Velocity.ShouldNotBeNull();
            config.TurboCharger.ShouldNotBeNull();
            config.Atmospheric.ShouldBeNull();
            config.TurboCharger.TauUp.ShouldBe(3f);
            config.Combustion.TorqueLambdaFloor.ShouldBe(0.86f);
            config.BuildCharger().ShouldBeOfType<TurboCharger>();
            config.Validate().ShouldBeNull();
        }

        [Fact]
        public void Build_ConfiguredValuesFlowThrough()
        {
            var config = new EngineOptions()
                .AddEngineExhaust()
                .ConfigureTurboCharger(t => t.TauUp = 5f)
                .ConfigureExhaustVelocity(v => v.Idle = 2.5f)
                .TryBuild("test-livery");

            config.TurboCharger.TauUp.ShouldBe(5f);
            config.Velocity.Idle.ShouldBe(2.5f);
        }

        [Fact]
        public void Build_AtmosphericSwitchesCharger()
        {
            var config = new EngineOptions()
                .AddEngineExhaust()
                .UseAtmosphericCharger(a => a.EtaPeak = 0.8f)
                .TryBuild("test-livery");

            config.ChargerKind.ShouldBe(ChargerKind.Atmospheric);
            config.Atmospheric.EtaPeak.ShouldBe(0.8f);
            config.TurboCharger.ShouldBeNull();
            config.BuildCharger().ShouldBeOfType<AtmosphericCharger>();
        }

        [Fact]
        public void Build_NormalizesSmokeBlock()
        {
            var config = new EngineOptions()
                .AddEngineExhaust()
                .ConfigureSmoke(s => { s.SootOpaqueLambda = 2f; s.SootOnsetLambda = 1f; })
                .TryBuild("test-livery");

            var smoke = config.Smoke;
            smoke.SootOpaqueLambda.ShouldBeLessThan(smoke.SootOnsetLambda);
        }

        [Fact]
        public void Build_InvalidConfiguration_ReturnsNull()
        {
            // no exhausts, and an empty livery id: both structurally invalid, so Build refuses
            new EngineOptions().TryBuild("test-livery").ShouldBeNull();
            new EngineOptions().AddEngineExhaust().TryBuild("").ShouldBeNull();
        }

        [Fact]
        public void Complete_InvalidProfile_ReturnsError()
        {
            new LocoProfile { LiveryId = "" }.Complete().ShouldNotBeNull();
        }

        [Fact]
        public void LoadUserProfiles_CompletesSparseProfile()
        {
            var sparse = new LocoProfile
            {
                LiveryId = "sparse",
                Enabled = false,
                Exhausts = [new LocoExhaust { Kind = ExhaustKind.Replacement, Name = "ExhaustSmoke" }],
            };

            var loaded = ProfileLoader.LoadUserProfiles([sparse]);

            var completed = loaded["sparse"];
            completed.ShouldBeSameAs(sparse);
            completed.LiveryId.ShouldBe("sparse");
            completed.Enabled.ShouldBeFalse();
            completed.ChargerKind.ShouldBe(ChargerKind.Turbo);
            completed.Combustion.ShouldNotBeNull();
            completed.Smoke.ShouldNotBeNull();
            completed.SmokeEmitter.ShouldNotBeNull();
            completed.ShimmerEmitter.ShouldNotBeNull();
            completed.Velocity.ShouldNotBeNull();
            completed.TurboCharger.ShouldNotBeNull();
            completed.Atmospheric.ShouldBeNull();
            completed.Validate().ShouldBeNull();
        }

        [Fact]
        public void LoadUserProfiles_CompleteAtmosphericProfile_NullsTurboBlock()
        {
            var sparse = new LocoProfile
            {
                LiveryId = "sparse-na",
                ChargerKind = ChargerKind.Atmospheric,
                Exhausts = [new LocoExhaust { Kind = ExhaustKind.Replacement, Name = "ExhaustSmoke" }],
            };

            var completed = ProfileLoader.LoadUserProfiles([sparse])["sparse-na"];

            completed.Atmospheric.ShouldNotBeNull();
            completed.TurboCharger.ShouldBeNull();
        }

        [Fact]
        public void Clone_DeepCopiesSettingsAndExhausts()
        {
            var profile = ValidProfile("clone");
            profile.TurboCharger = new TurboCharger.Settings { TauUp = 5f };

            var clone = profile.Clone();

            clone.LiveryId.ShouldBe("clone");
            clone.Exhausts.Count.ShouldBe(2);
            clone.TurboCharger.TauUp.ShouldBe(5f);

            // mutating the clone must never touch the original
            clone.Exhausts[0].Name = "changed";
            clone.TurboCharger.TauUp = 9f;

            profile.Exhausts[0].Name.ShouldNotBe("changed");
            profile.TurboCharger.TauUp.ShouldBe(5f);
        }

        [Fact]
        public void Clone_NullBlocks_StayNull()
        {
            var clone = ValidProfile("clone").Clone();

            clone.Combustion.ShouldBeNull();
            clone.Velocity.ShouldBeNull();
        }

        [Fact]
        public void Xml_NullBlocksAreOmitted()
        {
            var xml = Serialize(ValidProfile());

            xml.ShouldNotContain("<Combustion>");
            xml.ShouldNotContain("<TurboCharger>");
        }

        [Fact]
        public void Xml_DefaultValuedBlock_OmitsMembers()
        {
            var profile = ValidProfile("sparse");
            profile.Combustion = new CombustionModel.Settings();

            var xml = Serialize(profile);

            xml.ShouldContain("<Combustion");
            xml.ShouldNotContain("RpmTorqueExponent");
            xml.ShouldNotContain("TorqueLambdaFloor");
        }

        [Fact]
        public void Xml_PartiallyTunedBlock_WritesOnlyTunedMembers()
        {
            var profile = ValidProfile("sparse");
            profile.Combustion = new CombustionModel.Settings { TorqueLambdaFloor = 0.9f };

            var xml = Serialize(profile);

            xml.ShouldContain("TorqueLambdaFloor");
            xml.ShouldNotContain("RpmTorqueExponent");
        }

        [Fact]
        public void Xml_SparseBlock_RoundTripsWithDefaults()
        {
            var profile = ValidProfile("sparse");
            profile.Combustion = new CombustionModel.Settings { TorqueLambdaFloor = 0.9f };

            var restored = Deserialize(Serialize(profile));

            restored.Combustion.TorqueLambdaFloor.ShouldBe(0.9f);
            restored.Combustion.RpmTorqueExponent.ShouldBe(CombustionModel.Settings.DefaultRpmTorqueExponent);
        }

        [Fact]
        public void Xml_FieldBasedSettings_AreSparse()
        {
            var defaults = ValidProfile("sparse");
            defaults.Velocity = new ExhaustVelocitySettings();

            var defaultXml = Serialize(defaults);
            defaultXml.ShouldContain("<Velocity");
            defaultXml.ShouldNotContain("Idle");
            defaultXml.ShouldNotContain("FullLoad");

            var tuned = ValidProfile("sparse");
            tuned.Velocity = new ExhaustVelocitySettings { Idle = 2.5f };

            var tunedXml = Serialize(tuned);
            tunedXml.ShouldContain("Idle");
            tunedXml.ShouldNotContain("FullLoad");
        }

        [Fact]
        public void Xml_EnumAndBool_AreAlwaysWritten()
        {
            var profile = ValidProfile("sparse");

            var xml = Serialize(profile);
            xml.ShouldContain("<ChargerKind>Turbo</ChargerKind>");
            xml.ShouldContain("<Enabled>true</Enabled>");
            xml.ShouldContain("<Kind>Replacement</Kind>");

            profile.ChargerKind = ChargerKind.Atmospheric;
            Serialize(profile).ShouldContain("<ChargerKind>Atmospheric</ChargerKind>");
        }

        [Fact]
        public void Xml_DefaultColors_AreOmitted_ViaHex()
        {
            var profile = ValidProfile("sparse");
            profile.Smoke = new ExhaustSmokeModel.Settings();

            var xml = Serialize(profile);

            xml.ShouldContain("<Smoke");
            xml.ShouldNotContain("ColorIdleHazeHex");
            xml.ShouldNotContain("ColorOilBurnHex");
        }

        [Fact]
        public void Xml_ChangedColor_IsWrittenAsHexAndRoundTrips()
        {
            var profile = ValidProfile("sparse");
            profile.Smoke = new ExhaustSmokeModel.Settings { ColorIdleHaze = new Color(1f, 0f, 0f, 1f) };

            var xml = Serialize(profile);
            xml.ShouldContain("ColorIdleHazeHex");
            xml.ShouldContain("FF0000FF");
            xml.ShouldNotContain("ColorOilBurnHex");

            var restored = Deserialize(xml);
            restored.Smoke.ColorIdleHaze.ShouldBe(new Color(1f, 0f, 0f, 1f));
        }

        [Fact]
        public void Xml_Offset_IsAlwaysWritten()
        {
            var zero = ValidProfile("sparse");
            zero.Exhausts = [new LocoExhaust { Kind = ExhaustKind.Independent }];

            var moved = ValidProfile("sparse");
            moved.Exhausts = [new LocoExhaust { Kind = ExhaustKind.Independent, Offset = new Vector3(0f, 1f, 0f) }];

            Serialize(zero).ShouldContain("Offset");
            Serialize(moved).ShouldContain("Offset");
        }

        [Fact]
        public void Xml_ParticleEmitterSettings_AreSparse()
        {
            var profile = ValidProfile("sparse");
            profile.SmokeEmitter = new SmokeParticles.Settings();
            profile.ShimmerEmitter = new ShimmerParticles.Settings();

            var xml = Serialize(profile);

            xml.ShouldContain("<SmokeEmitter");
            xml.ShouldContain("<ShimmerEmitter");
            xml.ShouldNotContain("idleEmissionRate");
            xml.ShouldNotContain("turbulenceStrength");
            xml.ShouldNotContain("idleRate");
            xml.ShouldNotContain("yOffset");
        }

        [Fact]
        public void Xml_RoundTrip_PreservesValues()
        {
            var profile = ValidProfile("roundtrip");
            profile.TurboCharger = new TurboCharger.Settings { TauUp = 5f };
            profile.Exhausts[0].Offset = new Vector3(1f, 2f, 3f);

            var restored = Deserialize(Serialize(profile));

            restored.LiveryId.ShouldBe("roundtrip");
            restored.ChargerKind.ShouldBe(ChargerKind.Turbo);
            restored.Exhausts.Count.ShouldBe(2);
            restored.Exhausts[0].Kind.ShouldBe(ExhaustKind.Replacement);
            restored.Exhausts[0].Name.ShouldBe("ExhaustSmoke");
            restored.Exhausts[0].Offset.ShouldBe(new Vector3(1f, 2f, 3f));
            restored.Exhausts[1].Kind.ShouldBe(ExhaustKind.Independent);
            restored.TurboCharger.TauUp.ShouldBe(5f);
            restored.Combustion.ShouldBeNull();
            restored.Validate().ShouldBeNull();
        }

        [Fact]
        public void Xml_PartialBlock_FallsBackToDefaults()
        {
            var restored = Deserialize(
                @"<LocoProfile><Version>1</Version><LiveryId>partial</LiveryId>" +
                @"<Enabled>true</Enabled><Exhausts><LocoExhaust><Kind>Replacement</Kind>" +
                @"<Name>ExhaustSmoke</Name></LocoExhaust></Exhausts>" +
                @"<ChargerKind>Turbo</ChargerKind>" +
                @"<TurboCharger><TauUp>5</TauUp></TurboCharger></LocoProfile>");

            restored.TurboCharger.TauUp.ShouldBe(5f);
            restored.TurboCharger.TauDown.ShouldBe(1f);
            restored.Combustion.ShouldBeNull();
            restored.Exhausts[0].Offset.ShouldBe(Vector3.zero);
            restored.Validate().ShouldBeNull();
        }

        [Fact]
        public void Repository_SaveAndGet_RoundTrip()
        {
            ProfileRepository.Initialize(new Settings(), null);

            ProfileRepository.SaveProfile(ValidProfile("repo")).ShouldBeNull();

            ProfileRepository.TryGetUserProfile("repo").LiveryId.ShouldBe("repo");
            ProfileRepository.TryGetUserProfile("missing").ShouldBeNull();
        }

        [Fact]
        public void Repository_SaveInvalid_ReturnsErrorAndStoresNothing()
        {
            ProfileRepository.Initialize(new Settings(), null);
            var profile = ValidProfile();
            profile.LiveryId = "";

            ProfileRepository.SaveProfile(profile).ShouldNotBeNull();
            ProfileRepository.TryGetUserProfile("").ShouldBeNull();
        }

        [Fact]
        public void Repository_Save_ReplacesExisting()
        {
            var settings = new Settings();
            ProfileRepository.Initialize(settings, null);
            ProfileRepository.SaveProfile(ValidProfile("dup")).ShouldBeNull();

            var updated = ValidProfile("dup");
            updated.Enabled = false;
            ProfileRepository.SaveProfile(updated).ShouldBeNull();

            settings.LocoProfiles.Count.ShouldBe(1);
            ProfileRepository.TryGetUserProfile("dup").Enabled.ShouldBeFalse();
        }

        [Fact]
        public void Repository_Delete_Removes()
        {
            ProfileRepository.Initialize(new Settings(), null);
            ProfileRepository.SaveProfile(ValidProfile("gone")).ShouldBeNull();

            ProfileRepository.DeleteProfile("gone").ShouldBeTrue();
            ProfileRepository.TryGetUserProfile("gone").ShouldBeNull();
            ProfileRepository.DeleteProfile("gone").ShouldBeFalse();
        }

        [Fact]
        public void Repository_SetEnabled_Toggles()
        {
            var settings = new Settings();
            ProfileRepository.Initialize(settings, null);
            ProfileRepository.SaveProfile(ValidProfile("toggle")).ShouldBeNull();

            ProfileRepository.SetEnabled("toggle", false).ShouldBeTrue();
            ProfileRepository.TryGetUserProfile("toggle").Enabled.ShouldBeFalse();

            // the registry aliases the stored profile, so the flag must reach settings too
            settings.LocoProfiles[0].Enabled.ShouldBeFalse();

            ProfileRepository.SetEnabled("missing", false).ShouldBeFalse();
        }

        [Fact]
        public void Repository_Reload_SkipsInvalid()
        {
            var settings = new Settings();
            var bad = ValidProfile();
            bad.LiveryId = "";
            settings.LocoProfiles.Add(bad);
            settings.LocoProfiles.Add(ValidProfile("good"));

            ProfileRepository.Initialize(settings, null);
            var user = ProfileLoader.LoadUserProfiles(settings.LocoProfiles);
            ProfileRepository.SetUserProfiles(user);

            ProfileRepository.TryGetUserProfile("good").ShouldNotBeNull();
            ProfileRepository.TryGetUserProfile("").ShouldBeNull();
        }

        [Fact]
        public void Repository_Reload_NullList_DoesNotThrow()
        {
            var settings = new Settings();
            settings.LocoProfiles = null;

            ProfileRepository.Initialize(settings, null);

            ProfileRepository.TryGetUserProfile("anything").ShouldBeNull();
        }

        private static string Serialize(LocoProfile profile)
        {
            var serializer = new XmlSerializer(typeof(LocoProfile));
            using var writer = new StringWriter();
            serializer.Serialize(writer, profile);
            return writer.ToString();
        }

        private static LocoProfile Deserialize(string xml)
        {
            var serializer = new XmlSerializer(typeof(LocoProfile));
            using var reader = new StringReader(xml);
            return (LocoProfile)serializer.Deserialize(reader);
        }
    }
}
