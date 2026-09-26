using System.Collections.Generic;

using Shouldly;

using TurboTurbo.Profiles;

using Xunit;

using static TurboTurbo.Profiles.ProfileRepository;
using static TurboTurbo.Profiles.ProfileLoader;

namespace TurboTurboTests
{
    public class ExternalProfilesTests
    {
        [Fact]
        public void SetSuppliedProfiles_StoresAndReplaces()
        {
            var profile = new LocoProfile
            {
                LiveryId = "test-livery",
                Exhausts = [new LocoExhaust { Kind = ExhaustKind.Replacement, Path = "ExhaustEngineSmoke(Clone)" }],
            };

            SetSuppliedProfiles(new Dictionary<string, ModProfile>
            {
                ["test-livery"] = new ModProfile(profile, "mod-a"),
            });

            TryGetModProfile("test-livery").Exhausts[0].Path.ShouldBe("ExhaustEngineSmoke(Clone)");
            TryGetModProfile("missing").ShouldBeNull();

            SetSuppliedProfiles(new Dictionary<string, ModProfile>());

            TryGetModProfile("test-livery").ShouldBeNull();
        }
    }
}
