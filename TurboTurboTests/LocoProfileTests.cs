using System.IO;
using System.Xml.Serialization;

using Shouldly;

using TurboTurbo;
using TurboTurbo.Configuration;
using TurboTurbo.Effects;
using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.Profiles.Storage;
using TurboTurbo.Profiles.Storage.Current;
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
                    new LocoExhaust { Kind = ExhaustKind.Replacement, Path = "ExhaustSmoke", Offset = new Vector3(0f, 0.1f, 0f) },
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
        public void Validate_ReplacementWithoutPath_Fails(string path)
        {
            var profile = ValidProfile();
            profile.Exhausts[0].Path = path;

            profile.Validate().ShouldNotBeNull();
        }

        [Fact]
        public void Validate_IndependentWithoutPath_Passes()
        {
            var profile = ValidProfile();
            profile.Exhausts[1].Path = "";

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
                .TryBuild("test-livery").ShouldSucceed();

            config.LiveryId.ShouldBe("test-livery");
            config.ChargerKind.ShouldBe(ChargerKind.Turbo);
            config.Exhausts.Count.ShouldBe(1);
            config.Smoke.ShouldNotBeNull();
            config.SmokeEmitter.ShouldNotBeNull();
            config.ShimmerEmitter.ShouldNotBeNull();
            config.Velocity.ShouldNotBeNull();
            config.Stack.ShouldNotBeNull();
            config.TurboCharger.ShouldNotBeNull();
            config.Atmospheric.ShouldBeNull();
            config.TurboCharger.TauUp.ShouldBe(3f);
            config.BuildCharger().ShouldBeOfType<TurboCharger>();
            config.Validate().ShouldBeNull();
        }

        [Fact]
        public void Build_ConfiguredValuesFlowThrough()
        {
            var config = new EngineOptions()
                .AddEngineExhaust()
                .ConfigureTurboCharger(t => t.TauUp = 5f)
                .ConfigureExhaustVelocity(v => v.ExhaustVelocityCoefficient = 2.5f)
                .TryBuild("test-livery").ShouldSucceed();

            config.TurboCharger.TauUp.ShouldBe(5f);
            config.Velocity.ExhaustVelocityCoefficient.ShouldBe(2.5f);
        }

        [Fact]
        public void Build_AtmosphericSwitchesCharger()
        {
            var config = new EngineOptions()
                .AddEngineExhaust()
                .UseAtmosphericCharger(a => a.EtaPeak = 0.8f)
                .TryBuild("test-livery").ShouldSucceed();

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
                .TryBuild("test-livery").ShouldSucceed();

            var smoke = config.Smoke;
            smoke.SootOpaqueLambda.ShouldBeLessThan(smoke.SootOnsetLambda);
        }

        [Fact]
        public void Build_InvalidConfiguration_ReturnsNull()
        {
            // no exhausts, and an empty livery id: both structurally invalid, so Build refuses
            new EngineOptions().TryBuild("test-livery").ShouldFail().Message.ShouldNotBeNullOrEmpty();
            new EngineOptions().AddEngineExhaust().TryBuild("").ShouldFail().Message.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void Complete_InvalidProfile_ReturnsError()
        {
            new LocoProfile { LiveryId = "" }.Normalize().ShouldNotBeNull();
        }

        [Fact]
        public void LoadUserProfiles_CompletesSparseProfile()
        {
            var sparse = new LocoProfileXml
            {
                LiveryId = "sparse",
                Enabled = false,
                Exhausts = [new ExhaustXml { Kind = ExhaustKind.Replacement, Path = "ExhaustSmoke" }],
            };

            var loaded = ProfileLoader.LoadUserProfiles([LocoProfileCodec.ToElement(sparse)]);

            var completed = loaded["sparse"];
            completed.LiveryId.ShouldBe("sparse");
            completed.Enabled.ShouldBeFalse();
            completed.ChargerKind.ShouldBe(ChargerKind.Turbo);
            completed.Smoke.ShouldNotBeNull();
            completed.SmokeEmitter.ShouldNotBeNull();
            completed.ShimmerEmitter.ShouldNotBeNull();
            completed.Velocity.ShouldNotBeNull();
            completed.Stack.ShouldNotBeNull();
            completed.TurboCharger.ShouldNotBeNull();
            completed.Atmospheric.ShouldBeNull();
            completed.Validate().ShouldBeNull();
        }

        [Fact]
        public void LoadUserProfiles_CompleteAtmosphericProfile_NullsTurboBlock()
        {
            var sparse = new LocoProfileXml
            {
                LiveryId = "sparse-na",
                ChargerKind = ChargerKind.Atmospheric,
                Exhausts = [new ExhaustXml { Kind = ExhaustKind.Replacement, Path = "ExhaustSmoke" }],
            };

            var completed = ProfileLoader.LoadUserProfiles([LocoProfileCodec.ToElement(sparse)])["sparse-na"];

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
            clone.Exhausts[0].Path = "changed";
            clone.TurboCharger.TauUp = 9f;

            profile.Exhausts[0].Path.ShouldNotBe("changed");
            profile.TurboCharger.TauUp.ShouldBe(5f);
        }

        [Fact]
        public void Clone_NullBlocks_StayNull()
        {
            var clone = ValidProfile("clone").Clone();

            clone.Velocity.ShouldBeNull();
        }

        [Fact]
        public void Xml_NullBlocksAreOmitted()
        {
            var xml = Serialize(ValidProfile());

            xml.ShouldNotContain("<TurboCharger>");
        }

        [Fact]
        public void Xml_DefaultValuedBlock_OmitsMembers()
        {
            var profile = ValidProfile("sparse");
            profile.TurboCharger = new TurboCharger.Settings();

            var xml = Serialize(profile);

            xml.ShouldContain("<TurboCharger");
            xml.ShouldNotContain("TauUp");
            xml.ShouldNotContain("TauDown");
        }

        [Fact]
        public void Xml_PartiallyTunedBlock_WritesOnlyTunedMembers()
        {
            var profile = ValidProfile("sparse");
            profile.TurboCharger = new TurboCharger.Settings { TauUp = 5f };

            var xml = Serialize(profile);

            xml.ShouldContain("TauUp");
            xml.ShouldNotContain("TauDown");
        }

        [Fact]
        public void Xml_SparseBlock_RoundTripsWithDefaults()
        {
            var profile = ValidProfile("sparse");
            profile.TurboCharger = new TurboCharger.Settings { TauUp = 5f };

            var restored = Deserialize(Serialize(profile));

            restored.TurboCharger.TauUp.ShouldBe(5f);
            restored.TurboCharger.TauDown.ShouldBe(TurboCharger.Settings.DefaultTauDown);
        }

        [Fact]
        public void Xml_FieldBasedSettings_AreSparse()
        {
            var defaults = ValidProfile("sparse");
            defaults.Velocity = new ExhaustVelocitySettings();
            defaults.Combustion = new CombustionModel.Settings();

            var defaultXml = Serialize(defaults);
            defaultXml.ShouldContain("<Velocity");
            defaultXml.ShouldNotContain("ExhaustVelocityCoefficient");
            defaultXml.ShouldContain("<Combustion");
            defaultXml.ShouldNotContain("RatedExhaustTempK");
            defaultXml.ShouldNotContain("TauChamber");
            defaultXml.ShouldNotContain("TauCooldownOpen");
            defaultXml.ShouldNotContain("TauCooldownClosed");
            defaultXml.ShouldNotContain("IgnitionTempK");

            var tuned = ValidProfile("sparse");
            tuned.Velocity = new ExhaustVelocitySettings { ExhaustVelocityCoefficient = 2.5f };

            var tunedXml = Serialize(tuned);
            tunedXml.ShouldContain("ExhaustVelocityCoefficient");
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
            restored.Smoke.ColorIdleHazeHex.ShouldBe("FF0000FF");
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
            xml.ShouldNotContain("startSize");
            xml.ShouldNotContain("turbulenceStrengthMin");
            xml.ShouldNotContain("turbulenceStrengthMax");
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
            restored.Exhausts[0].Path.ShouldBe("ExhaustSmoke");
            restored.Exhausts[0].Offset.ShouldBe(new Vector3(1f, 2f, 3f));
            restored.Exhausts[1].Kind.ShouldBe(ExhaustKind.Independent);
            restored.TurboCharger.TauUp.ShouldBe(5f);
            ProfileMapper.ToRuntime(restored).Validate().ShouldBeNull();
        }

        [Fact]
        public void Xml_PartialBlock_FallsBackToDefaults()
        {
            var restored = Deserialize(
                @"<LocoProfile><Version>1</Version><LiveryId>partial</LiveryId>" +
                @"<Enabled>true</Enabled><Exhausts><LocoExhaust><Kind>Replacement</Kind>" +
                @"<Path>ExhaustSmoke</Path></LocoExhaust></Exhausts>" +
                @"<ChargerKind>Turbo</ChargerKind>" +
                @"<TurboCharger><TauUp>5</TauUp></TurboCharger></LocoProfile>");

            restored.TurboCharger.TauUp.ShouldBe(5f);
            restored.TurboCharger.TauDown.ShouldBe(TurboChargerXml.DefaultTauDown);
            restored.Exhausts[0].Offset.ShouldBe(Vector3.zero);
            ProfileMapper.ToRuntime(restored).Validate().ShouldBeNull();
        }

        [Fact]
        public void Xml_RoundTrip_PreservesFullTunedSettings()
        {
            var profile = ValidProfile("full");
            profile.TurboCharger = new TurboCharger.Settings
            {
                LambdaCalibration = 1.7f,
                TauUp = 4f,
                TauDown = 2.5f,
                CompressionRatio = 18f,
                IntakeNtu = 0.15f,
            };
            profile.Smoke = new ExhaustSmokeModel.Settings
            {
                Density = 90f,
                ColorOilBurn = new Color(0f, 1f, 0f, 1f),
                SootMaxAlpha = 0.5f,
            };
            profile.SmokeEmitter = new SmokeParticles.Settings { startSize = 0.5f, drag = 0.9f };
            profile.ShimmerEmitter = new ShimmerParticles.Settings { strength = 0.02f, yOffset = 0.3f };
            profile.Velocity = new ExhaustVelocitySettings { ExhaustVelocityCoefficient = 3.25f };
            profile.Combustion = new CombustionModel.Settings
            {
                RatedExhaustTempK = 800f,
                TauChamber = 9f,
                TauCooldownOpen = 420f,
                TauCooldownClosed = 3600f,
                IgnitionSpread = 0.8f,
            };
            profile.Stack = new StackModel.Settings { FillTime = 75f, ClearTime = 45f, StickMax = 0.6f };

            var restored = ProfileMapper.ToRuntime(Deserialize(Serialize(profile)));

            restored.TurboCharger.LambdaCalibration.ShouldBe(1.7f);
            restored.TurboCharger.TauUp.ShouldBe(4f);
            restored.TurboCharger.TauDown.ShouldBe(2.5f);
            restored.TurboCharger.CompressionRatio.ShouldBe(18f);
            restored.TurboCharger.IntakeNtu.ShouldBe(0.15f);
            restored.Smoke.Density.ShouldBe(90f);
            restored.Smoke.ColorOilBurn.ShouldBe(new Color(0f, 1f, 0f, 1f));
            restored.Smoke.SootMaxAlpha.ShouldBe(0.5f);
            restored.SmokeEmitter.startSize.ShouldBe(0.5f);
            restored.SmokeEmitter.drag.ShouldBe(0.9f);
            restored.ShimmerEmitter.strength.ShouldBe(0.02f);
            restored.ShimmerEmitter.yOffset.ShouldBe(0.3f);
            restored.Velocity.ExhaustVelocityCoefficient.ShouldBe(3.25f);
            restored.Combustion.RatedExhaustTempK.ShouldBe(800f);
            restored.Combustion.TauChamber.ShouldBe(9f);
            restored.Combustion.TauCooldownOpen.ShouldBe(420f);
            restored.Combustion.TauCooldownClosed.ShouldBe(3600f);
            restored.Combustion.IgnitionSpread.ShouldBe(0.8f);
            restored.Stack.FillTime.ShouldBe(75f);
            restored.Stack.ClearTime.ShouldBe(45f);
            restored.Stack.StickMax.ShouldBe(0.6f);
            restored.Validate().ShouldBeNull();
        }

        [Fact]
        public void Xml_StackBlock_IsSparse()
        {
            var profile = ValidProfile("sparse");
            profile.Stack = new StackModel.Settings { FillTime = 30f };

            var xml = Serialize(profile);

            xml.ShouldContain("<Stack");
            xml.ShouldContain("FillTime");
            xml.ShouldNotContain("ClearTime");
            xml.ShouldNotContain("TauWall");
        }

        [Fact]
        public void Xml_UnknownElement_IsIgnored()
        {
            // a legacy field that no longer exists must not break loading
            var restored = Deserialize(
                @"<LocoProfile><Version>1</Version><LiveryId>legacy</LiveryId>" +
                @"<Exhausts><LocoExhaust><Kind>Replacement</Kind><Path>ExhaustSmoke</Path></LocoExhaust></Exhausts>" +
                @"<Velocity><FullLoad>12</FullLoad><ExhaustVelocityCoefficient>4</ExhaustVelocityCoefficient></Velocity>" +
                @"</LocoProfile>");

            restored.Velocity.ExhaustVelocityCoefficient.ShouldBe(4f);
            ProfileMapper.ToRuntime(restored).Validate().ShouldBeNull();
        }

        [Fact]
        public void UserSource_SaveAndGet_RoundTrip()
        {
            SettingsStore.Current = new Settings();

            ProfileService.User.Save(ValidProfile("repo")).ShouldBeNull();

            ProfileService.User.Get("repo").LiveryId.ShouldBe("repo");
            ProfileService.User.Get("missing").ShouldBeNull();
        }

        [Fact]
        public void UserSource_SaveInvalid_ReturnsErrorAndStoresNothing()
        {
            SettingsStore.Current = new Settings();
            var profile = ValidProfile();
            profile.LiveryId = "";

            ProfileService.User.Save(profile).ShouldNotBeNull();
            ProfileService.User.Get("").ShouldBeNull();
        }

        [Fact]
        public void UserSource_Save_ReplacesExisting()
        {
            var settings = new Settings();
            SettingsStore.Current = settings;
            ProfileService.User.Save(ValidProfile("dup")).ShouldBeNull();

            var updated = ValidProfile("dup");
            updated.Enabled = false;
            ProfileService.User.Save(updated).ShouldBeNull();

            settings.LocoProfiles.Items.Count.ShouldBe(1);
            ProfileService.User.Get("dup").Enabled.ShouldBeFalse();
        }

        [Fact]
        public void UserSource_Delete_Removes()
        {
            SettingsStore.Current = new Settings();
            ProfileService.User.Save(ValidProfile("gone")).ShouldBeNull();

            ProfileService.User.DeleteProfile("gone").ShouldBeTrue();
            ProfileService.User.Get("gone").ShouldBeNull();
            ProfileService.User.DeleteProfile("gone").ShouldBeFalse();
        }

        [Fact]
        public void UserSource_SetEnabled_Toggles()
        {
            var settings = new Settings();
            SettingsStore.Current = settings;
            ProfileService.User.Save(ValidProfile("toggle")).ShouldBeNull();

            ProfileService.User.SetEnabled("toggle", false).ShouldBeTrue();
            ProfileService.User.Get("toggle").Enabled.ShouldBeFalse();

            // the registry aliases the stored profile, so the flag must reach settings too
            ((bool)settings.LocoProfiles.Items[0].Element("Enabled")).ShouldBeFalse();

            ProfileService.User.SetEnabled("missing", false).ShouldBeFalse();
        }

        [Fact]
        public void UserSource_Load_SkipsInvalid()
        {
            var settings = new Settings();
            var bad = ValidProfile();
            bad.LiveryId = "";
            settings.LocoProfiles.Items.Add(LocoProfileCodec.ToElement(bad));
            settings.LocoProfiles.Items.Add(LocoProfileCodec.ToElement(ValidProfile("good")));

            SettingsStore.Current = settings;
            ProfileService.User.Load(SettingsStore.Current);

            ProfileService.User.Get("good").ShouldNotBeNull();
            ProfileService.User.Get("").ShouldBeNull();
        }

        [Fact]
        public void UserSource_Load_NullList_DoesNotThrow()
        {
            var settings = new Settings();
            settings.LocoProfiles = null;

            SettingsStore.Current = settings;
            ProfileService.User.Load(SettingsStore.Current);

            ProfileService.User.Get("anything").ShouldBeNull();
        }

        [Fact]
        public void Build_RootsSwitchesCharger()
        {
            var config = new EngineOptions()
                .AddEngineExhaust()
                .UseRootsCharger(r => r.PressureRatio = 1.4f)
                .TryBuild("test-livery").ShouldSucceed();

            config.ChargerKind.ShouldBe(ChargerKind.Roots);
            config.Roots.PressureRatio.ShouldBe(1.4f);
            config.TurboCharger.ShouldBeNull();
            config.Atmospheric.ShouldBeNull();
            config.BuildCharger().ShouldBeOfType<RootsCharger>();
        }

        [Fact]
        public void LoadUserProfiles_CompleteRootsProfile_NullsOtherChargerBlocks()
        {
            var sparse = new LocoProfileXml
            {
                LiveryId = "sparse-roots",
                ChargerKind = ChargerKind.Roots,
                Exhausts = [new ExhaustXml { Kind = ExhaustKind.Replacement, Path = "ExhaustSmoke" }],
            };

            var completed = ProfileLoader.LoadUserProfiles([LocoProfileCodec.ToElement(sparse)])["sparse-roots"];

            completed.Roots.ShouldNotBeNull();
            completed.TurboCharger.ShouldBeNull();
            completed.Atmospheric.ShouldBeNull();
        }

        [Fact]
        public void Normalize_SwitchingAwayFromRoots_DropsRootsBlock()
        {
            var profile = ValidProfile();
            profile.ChargerKind = ChargerKind.Roots;
            profile.Normalize().ShouldBeNull();
            profile.Roots.ShouldNotBeNull();

            profile.ChargerKind = ChargerKind.Turbo;
            profile.Normalize().ShouldBeNull();

            profile.Roots.ShouldBeNull();
            profile.TurboCharger.ShouldNotBeNull();
        }

        [Fact]
        public void Normalize_Roots_ValidatesSettings()
        {
            var profile = ValidProfile();
            profile.ChargerKind = ChargerKind.Roots;
            profile.Roots = new RootsCharger.Settings { PressureRatio = 0.5f };

            profile.Normalize().ShouldBeNull();

            profile.Roots.PressureRatio.ShouldBe(1f);
        }

        [Fact]
        public void Clone_DeepCopiesRootsSettings()
        {
            var profile = ValidProfile();
            profile.ChargerKind = ChargerKind.Roots;
            profile.Roots = new RootsCharger.Settings { PressureRatio = 1.4f };

            var clone = profile.Clone();
            clone.Roots.PressureRatio = 1.2f;

            clone.ChargerKind.ShouldBe(ChargerKind.Roots);
            profile.Roots.PressureRatio.ShouldBe(1.4f);
        }

        [Fact]
        public void Xml_RoundTrip_PreservesRootsSettings()
        {
            var profile = ValidProfile("roots");
            profile.ChargerKind = ChargerKind.Roots;
            profile.Roots = new RootsCharger.Settings
            {
                PressureRatio = 1.35f,
                BoostExponent = 1.8f,
                CompressorEfficiency = 0.6f,
                LambdaCalibration = 0.85f,
                CompressionRatio = 15f,
                IntakeNtu = 0.2f,
            };

            var xml = Serialize(profile);
            xml.ShouldContain("<ChargerKind>Roots</ChargerKind>");

            var restored = ProfileMapper.ToRuntime(Deserialize(xml));
            restored.ChargerKind.ShouldBe(ChargerKind.Roots);
            restored.Roots.PressureRatio.ShouldBe(1.35f);
            restored.Roots.BoostExponent.ShouldBe(1.8f);
            restored.Roots.CompressorEfficiency.ShouldBe(0.6f);
            restored.Roots.LambdaCalibration.ShouldBe(0.85f);
            restored.Roots.CompressionRatio.ShouldBe(15f);
            restored.Roots.IntakeNtu.ShouldBe(0.2f);
            restored.Validate().ShouldBeNull();
        }

        [Fact]
        public void Xml_PartialRootsBlock_FallsBackToDefaults()
        {
            var restored = Deserialize(
                @"<LocoProfile><Version>1</Version><LiveryId>partial</LiveryId>" +
                @"<Enabled>true</Enabled><Exhausts><LocoExhaust><Kind>Replacement</Kind>" +
                @"<Path>ExhaustSmoke</Path></LocoExhaust></Exhausts>" +
                @"<ChargerKind>Roots</ChargerKind>" +
                @"<Roots><PressureRatio>1.4</PressureRatio></Roots></LocoProfile>");

            restored.Roots.PressureRatio.ShouldBe(1.4f);
            restored.Roots.BoostExponent.ShouldBe(RootsChargerXml.DefaultBoostExponent);
            ProfileMapper.ToRuntime(restored).Validate().ShouldBeNull();
        }

        [Fact]
        public void Xml_RootsDefaults_MatchRuntimeDefaults()
        {
            var xml = new RootsChargerXml();
            var runtime = new RootsCharger.Settings();

            xml.PressureRatio.ShouldBe(runtime.PressureRatio);
            xml.BoostExponent.ShouldBe(runtime.BoostExponent);
            xml.CompressorEfficiency.ShouldBe(runtime.CompressorEfficiency);
            xml.LambdaCalibration.ShouldBe(runtime.LambdaCalibration);
            xml.CompressionRatio.ShouldBe(runtime.CompressionRatio);
            xml.IntakeNtu.ShouldBe(runtime.IntakeNtu);
        }

        [Fact]
        public void Xml_DefaultRootsBlock_OmitsMembers()
        {
            var profile = ValidProfile("sparse");
            profile.ChargerKind = ChargerKind.Roots;
            profile.Roots = new RootsCharger.Settings();

            var xml = Serialize(profile);

            xml.ShouldContain("<Roots");
            xml.ShouldNotContain("PressureRatio");
            xml.ShouldNotContain("BoostExponent");
        }

        private static string Serialize(LocoProfile profile) => Serialize(ProfileMapper.ToXml(profile));

        private static string Serialize(LocoProfileXml xml)
        {
            var serializer = new XmlSerializer(typeof(LocoProfileXml));
            using var writer = new StringWriter();
            serializer.Serialize(writer, xml);
            return writer.ToString();
        }

        private static LocoProfileXml Deserialize(string xml)
        {
            var serializer = new XmlSerializer(typeof(LocoProfileXml));
            using var reader = new StringReader(xml);
            return (LocoProfileXml)serializer.Deserialize(reader);
        }
    }
}
