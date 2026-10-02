using System.Collections.Generic;
using System.Linq;

using Shouldly;

using TurboTurbo.Profiles;

using Xunit;

namespace TurboTurboTests
{
    public class ProfileResolverTests
    {
        private const string Livery = "test";

        private sealed class FakeSource : IProfileSource
        {
            private readonly Dictionary<string, LocoProfile> _profiles = new();

            public FakeSource(ProfileTier tier)
            {
                Tier = tier;
            }

            public ProfileTier Tier { get; }

            public FakeSource With(string liveryId, bool enabled = true)
            {
                _profiles[liveryId] = new LocoProfile
                {
                    LiveryId = liveryId,
                    Enabled = enabled,
                    Exhausts = [new LocoExhaust { Kind = ExhaustKind.Replacement, Path = "ExhaustSmoke" }],
                };
                return this;
            }

            public bool TryGet(string liveryId, out ProfileEntry entry)
            {
                if (_profiles.TryGetValue(liveryId, out var profile))
                {
                    var mod = Tier == ProfileTier.Mod;
                    entry = new ProfileEntry(profile, Tier, mod ? "mod-a" : null, mod ? "Some Mod" : null);
                    return true;
                }

                entry = default;
                return false;
            }
        }

        private static FakeSource Source(ProfileTier tier) => new(tier);

        private static ProfileResolver Resolver(FakeSource user, FakeSource mod, FakeSource builtIn) =>
            new(user, mod, builtIn);

        [Fact]
        public void Normal_Nothing_ResolvesToNone()
        {
            var resolution = Resolver(Source(ProfileTier.User), Source(ProfileTier.Mod), Source(ProfileTier.BuiltIn))
                .Resolve(Livery, ResolutionMode.Normal);

            resolution.Winner.ShouldBeNull();
            resolution.Effective.ShouldBeNull();
            resolution.Disabled.ShouldBeFalse();
            resolution.Present.ShouldBeEmpty();
        }

        [Fact]
        public void Normal_BuiltInOnly()
        {
            var resolution = Resolver(Source(ProfileTier.User), Source(ProfileTier.Mod), Source(ProfileTier.BuiltIn).With(Livery))
                .Resolve(Livery, ResolutionMode.Normal);

            resolution.Mode.ShouldBe(ResolutionMode.Normal);
            resolution.Winner?.Tier.ShouldBe(ProfileTier.BuiltIn);
            resolution.Effective?.Tier.ShouldBe(ProfileTier.BuiltIn);
            resolution.Present.Select(e => e.Tier).ShouldBe([ProfileTier.BuiltIn]);
            resolution.Shadowed.ShouldBeEmpty();
        }

        [Fact]
        public void Normal_ModOverridesBuiltIn()
        {
            var resolution = Resolver(Source(ProfileTier.User), Source(ProfileTier.Mod).With(Livery), Source(ProfileTier.BuiltIn).With(Livery))
                .Resolve(Livery, ResolutionMode.Normal);

            resolution.Winner?.Tier.ShouldBe(ProfileTier.Mod);
            resolution.Effective?.Tier.ShouldBe(ProfileTier.Mod);
            resolution.Effective?.Origin.ShouldBe("Some Mod");
            resolution.Shadowed.Select(e => e.Tier).ShouldBe([ProfileTier.BuiltIn]);
        }

        [Fact]
        public void Normal_UserOverridesEverything()
        {
            var resolution = Resolver(Source(ProfileTier.User).With(Livery), Source(ProfileTier.Mod).With(Livery), Source(ProfileTier.BuiltIn).With(Livery))
                .Resolve(Livery, ResolutionMode.Normal);

            resolution.Winner?.Tier.ShouldBe(ProfileTier.User);
            resolution.Effective?.Tier.ShouldBe(ProfileTier.User);
            resolution.Shadowed.Select(e => e.Tier).ShouldBe([ProfileTier.Mod, ProfileTier.BuiltIn]);
        }

        [Fact]
        public void Normal_DisabledUser_StopsTheChain()
        {
            var resolution = Resolver(Source(ProfileTier.User).With(Livery, enabled: false), Source(ProfileTier.Mod).With(Livery), Source(ProfileTier.BuiltIn).With(Livery))
                .Resolve(Livery, ResolutionMode.Normal);

            resolution.Winner?.Tier.ShouldBe(ProfileTier.User);
            resolution.Effective.ShouldBeNull();
            resolution.Disabled.ShouldBeTrue();
            resolution.Shadowed.Select(e => e.Tier).ShouldBe([ProfileTier.Mod, ProfileTier.BuiltIn]);
        }

        [Fact]
        public void Normal_DisabledMod_StopsTheChain()
        {
            var resolution = Resolver(Source(ProfileTier.User), Source(ProfileTier.Mod).With(Livery, enabled: false), Source(ProfileTier.BuiltIn).With(Livery))
                .Resolve(Livery, ResolutionMode.Normal);

            resolution.Winner?.Tier.ShouldBe(ProfileTier.Mod);
            resolution.Effective.ShouldBeNull();
            resolution.Disabled.ShouldBeTrue();
            resolution.Shadowed.Select(e => e.Tier).ShouldBe([ProfileTier.BuiltIn]);
        }

        [Fact]
        public void Authoring_IgnoresTheUserTier()
        {
            var resolution = Resolver(Source(ProfileTier.User).With(Livery), Source(ProfileTier.Mod).With(Livery), Source(ProfileTier.BuiltIn).With(Livery))
                .Resolve(Livery, ResolutionMode.Authoring);

            resolution.Mode.ShouldBe(ResolutionMode.Authoring);
            resolution.Winner?.Tier.ShouldBe(ProfileTier.Mod);
            resolution.Effective?.Tier.ShouldBe(ProfileTier.Mod);
            resolution.Present.Select(e => e.Tier).ShouldBe([ProfileTier.Mod, ProfileTier.BuiltIn]);
        }

        [Fact]
        public void Authoring_DisabledMod_FallsThroughToBuiltIn()
        {
            var resolution = Resolver(Source(ProfileTier.User), Source(ProfileTier.Mod).With(Livery, enabled: false), Source(ProfileTier.BuiltIn).With(Livery))
                .Resolve(Livery, ResolutionMode.Authoring);

            resolution.Winner?.Tier.ShouldBe(ProfileTier.Mod);
            resolution.Effective?.Tier.ShouldBe(ProfileTier.BuiltIn);
            resolution.Disabled.ShouldBeTrue();
            resolution.Shadowed.Select(e => e.Tier).ShouldBe([ProfileTier.BuiltIn]);
        }

        [Fact]
        public void Authoring_BuiltInOnly()
        {
            var resolution = Resolver(Source(ProfileTier.User), Source(ProfileTier.Mod), Source(ProfileTier.BuiltIn).With(Livery))
                .Resolve(Livery, ResolutionMode.Authoring);

            resolution.Winner?.Tier.ShouldBe(ProfileTier.BuiltIn);
            resolution.Effective?.Tier.ShouldBe(ProfileTier.BuiltIn);
            resolution.Disabled.ShouldBeFalse();
        }
    }
}
