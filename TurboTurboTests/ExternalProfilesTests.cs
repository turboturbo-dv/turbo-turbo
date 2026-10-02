using System.Collections.Generic;

using Shouldly;

using TurboTurbo.Profiles;
using TurboTurbo.Profiles.Storage;

using Xunit;

using static TurboTurbo.Profiles.Storage.ProfileLoader;

namespace TurboTurboTests
{
    public class ExternalProfilesTests
    {
        [Fact]
        public void ModProfiles_StoreAndReplace()
        {
            var profile = new LocoProfile
            {
                LiveryId = "test-livery",
                Exhausts = [new LocoExhaust { Kind = ExhaustKind.Replacement, Path = "ExhaustEngineSmoke(Clone)" }],
            };

            ProfileService.Mod.Replace(new Dictionary<string, ModProfile>
            {
                ["test-livery"] = new ModProfile(profile, "mod-a", "Mod A"),
            });

            ProfileService.Mod.GetProfile("test-livery").Exhausts[0].Path.ShouldBe("ExhaustEngineSmoke(Clone)");
            ProfileService.Mod.GetProfile("missing").ShouldBeNull();

            ProfileService.Mod.Replace(new Dictionary<string, ModProfile>());

            ProfileService.Mod.GetProfile("test-livery").ShouldBeNull();
        }
    }
}
