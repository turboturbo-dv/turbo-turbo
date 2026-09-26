using Shouldly;

using TurboTurbo.Profiles;

using Xunit;

namespace TurboTurboTests
{
    public class ProfileStatusTests
    {
        private static LocoProfile Profile(bool enabled = true)
        {
            return new LocoProfile
            {
                LiveryId = "test",
                Enabled = enabled,
                Exhausts = [new LocoExhaust { Kind = ExhaustKind.Replacement, Path = "ExhaustSmoke" }],
            };
        }

        [Fact]
        public void Nothing_IsNotConfigured()
        {
            var status = ProfileStatus.Resolve(null, null, null, null);

            status.Kind.ShouldBe(ProfileStatusKind.NotConfigured);
            status.Label.ShouldBe("not configured");
        }

        [Fact]
        public void BuiltInOnly()
        {
            var status = ProfileStatus.Resolve(null, null, null, Profile());

            status.Kind.ShouldBe(ProfileStatusKind.BuiltIn);
            status.Label.ShouldBe("built-in");
        }

        [Fact]
        public void Supplied_OverridesBuiltIn()
        {
            var status = ProfileStatus.Resolve(null, Profile(), "Some Mod", Profile());

            status.Kind.ShouldBe(ProfileStatusKind.Supplied);
            status.Label.ShouldBe("mod: Some Mod (overrides built-in)");
        }

        [Fact]
        public void User_OverridesSuppliedAndBuiltIn()
        {
            var status = ProfileStatus.Resolve(Profile(), Profile(), "Some Mod", Profile());

            status.Kind.ShouldBe(ProfileStatusKind.User);
            status.Label.ShouldBe("yours (overrides mod + built-in)");
        }

        [Fact]
        public void UserOnly_HasNoOverrideSuffix()
        {
            var status = ProfileStatus.Resolve(Profile(), null, null, null);

            status.Kind.ShouldBe(ProfileStatusKind.User);
            status.Label.ShouldBe("yours");
        }

        [Fact]
        public void DisabledUserProfile_DoesNotFallThrough()
        {
            var status = ProfileStatus.Resolve(Profile(enabled: false), Profile(), "Some Mod", Profile());

            status.Kind.ShouldBe(ProfileStatusKind.UserDisabled);
            status.Label.ShouldBe("yours (disabled)");
        }

        [Fact]
        public void DisabledSuppliedProfile_DoesNotFallThroughToBuiltIn()
        {
            var status = ProfileStatus.Resolve(null, Profile(enabled: false), "Some Mod", Profile());

            status.Kind.ShouldBe(ProfileStatusKind.SuppliedDisabled);
            status.Label.ShouldBe("mod: Some Mod (disabled)");
        }
    }
}