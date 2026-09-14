using System.IO;
using System.Xml.Serialization;

using Shouldly;

using TurboTurbo.Configuration;
using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.Setup;

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
        public void Apply_NullBlocksKeepDefaults()
        {
            var options = new EngineOptions();
            options.ApplyLocoProfile(ValidProfile());
            var config = options.Build();

            config.Exhausts.Count.ShouldBe(2);
            config.ChargerKind.ShouldBe(ChargerKind.Turbo);
            config.TurboCharger.TauUp.ShouldBe(3f);
            config.Combustion.TorqueLambdaFloor.ShouldBe(0.7f);
            config.BuildCharger().ShouldBeOfType<TurboCharger>();
        }

        [Fact]
        public void Apply_TurboValuesFlowThrough()
        {
            var profile = ValidProfile();
            profile.TurboCharger = new TurboCharger.Settings { TauUp = 5f };

            var options = new EngineOptions();
            options.ApplyLocoProfile(profile);

            options.Build().TurboCharger.TauUp.ShouldBe(5f);
        }

        [Fact]
        public void Apply_AtmosphericSwitchesCharger()
        {
            var profile = ValidProfile();
            profile.ChargerKind = ChargerKind.Atmospheric;
            profile.Atmospheric = new AtmosphericCharger.Settings { EtaPeak = 0.8f };

            var options = new EngineOptions();
            options.ApplyLocoProfile(profile);
            var config = options.Build();

            config.ChargerKind.ShouldBe(ChargerKind.Atmospheric);
            config.Atmospheric.EtaPeak.ShouldBe(0.8f);
            config.BuildCharger().ShouldBeOfType<AtmosphericCharger>();
        }

        [Fact]
        public void Apply_NormalizesSmokeBlock()
        {
            var profile = ValidProfile();
            profile.Smoke = new ExhaustSmokeModel.Settings { SootOpaqueLambda = 2f, SootOnsetLambda = 1f };

            var options = new EngineOptions();
            options.ApplyLocoProfile(profile);

            var smoke = options.Build().Smoke;
            smoke.SootOpaqueLambda.ShouldBeLessThan(smoke.SootOnsetLambda);
        }

        [Fact]
        public void Apply_VelocityValuesFlowThrough()
        {
            var profile = ValidProfile();
            profile.Velocity = new ExhaustVelocitySettings { Idle = 2.5f };

            var options = new EngineOptions();
            options.ApplyLocoProfile(profile);

            options.Build().Velocity.Idle.ShouldBe(2.5f);
        }

        [Fact]
        public void Xml_NullBlocksAreOmitted()
        {
            var xml = Serialize(ValidProfile());

            xml.ShouldNotContain("<Combustion>");
            xml.ShouldNotContain("<TurboCharger>");
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
            EngineConfigurationRepository.Initialize(new Settings(), null);

            EngineConfigurationRepository.SaveProfile(ValidProfile("repo")).ShouldBeNull();

            EngineConfigurationRepository.GetProfile("repo").LiveryId.ShouldBe("repo");
            EngineConfigurationRepository.GetProfile("missing").ShouldBeNull();
        }

        [Fact]
        public void Repository_SaveInvalid_ReturnsErrorAndStoresNothing()
        {
            EngineConfigurationRepository.Initialize(new Settings(), null);
            var profile = ValidProfile();
            profile.LiveryId = "";

            EngineConfigurationRepository.SaveProfile(profile).ShouldNotBeNull();
            EngineConfigurationRepository.GetProfile("").ShouldBeNull();
        }

        [Fact]
        public void Repository_Save_ReplacesExisting()
        {
            var settings = new Settings();
            EngineConfigurationRepository.Initialize(settings, null);
            EngineConfigurationRepository.SaveProfile(ValidProfile("dup")).ShouldBeNull();

            var updated = ValidProfile("dup");
            updated.Enabled = false;
            EngineConfigurationRepository.SaveProfile(updated).ShouldBeNull();

            settings.LocoProfiles.Count.ShouldBe(1);
            EngineConfigurationRepository.GetProfile("dup").Enabled.ShouldBeFalse();
        }

        [Fact]
        public void Repository_Delete_Removes()
        {
            EngineConfigurationRepository.Initialize(new Settings(), null);
            EngineConfigurationRepository.SaveProfile(ValidProfile("gone")).ShouldBeNull();

            EngineConfigurationRepository.DeleteProfile("gone").ShouldBeTrue();
            EngineConfigurationRepository.GetProfile("gone").ShouldBeNull();
            EngineConfigurationRepository.DeleteProfile("gone").ShouldBeFalse();
        }

        [Fact]
        public void Repository_SetEnabled_Toggles()
        {
            EngineConfigurationRepository.Initialize(new Settings(), null);
            EngineConfigurationRepository.SaveProfile(ValidProfile("toggle")).ShouldBeNull();

            EngineConfigurationRepository.SetEnabled("toggle", false).ShouldBeTrue();
            EngineConfigurationRepository.GetProfile("toggle").Enabled.ShouldBeFalse();
            EngineConfigurationRepository.SetEnabled("missing", false).ShouldBeFalse();
        }

        [Fact]
        public void Repository_Reload_SkipsInvalid()
        {
            var settings = new Settings();
            var bad = ValidProfile();
            bad.LiveryId = "";
            settings.LocoProfiles.Add(bad);
            settings.LocoProfiles.Add(ValidProfile("good"));

            EngineConfigurationRepository.Initialize(settings, null);
            var user = ProfileLoader.LoadUserProfiles(settings.LocoProfiles);
            EngineConfigurationRepository.SetUserProfiles(user);

            EngineConfigurationRepository.GetProfile("good").ShouldNotBeNull();
            EngineConfigurationRepository.GetProfile("").ShouldBeNull();
        }

        [Fact]
        public void Repository_Reload_NullList_DoesNotThrow()
        {
            var settings = new Settings();
            settings.LocoProfiles = null;

            EngineConfigurationRepository.Initialize(settings, null);

            EngineConfigurationRepository.GetProfile("anything").ShouldBeNull();
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
