using Shouldly;

using TurboTurbo.Profiles;
using TurboTurbo.Profiles.Storage;

using Xunit;

namespace TurboTurboTests
{
    public class ProfileSourceTests
    {
        private static LocoProfile Profile(string id, bool enabled = true) => new()
        {
            LiveryId = id,
            Enabled = enabled,
            Exhausts = [new LocoExhaust { Kind = ExhaustKind.Replacement, Path = "ExhaustSmoke" }],
        };

        [Fact]
        public void UserSource_ReplacePutRemove()
        {
            var source = new UserProfileSource();
            source.Replace(new[] { Profile("a"), Profile("b") });

            source.Get("a").LiveryId.ShouldBe("a");
            source.Put(Profile("c"));
            source.Get("c").ShouldNotBeNull();
            source.Remove("a").ShouldBeTrue();
            source.Get("a").ShouldBeNull();
            source.Remove("a").ShouldBeFalse();
        }

        [Fact]
        public void UserSource_ReplaceNullClears()
        {
            var source = new UserProfileSource();
            source.Put(Profile("a"));

            source.Replace(null);

            source.Get("a").ShouldBeNull();
        }

        [Fact]
        public void ModSource_ExposesOriginAndMod()
        {
            var source = new ModProfileSource();
            source.Put("a", new ProfileLoader.ModProfile(Profile("a"), "mod-1", "Mod One"));
            source.Put("b", new ProfileLoader.ModProfile(Profile("b"), "mod-2", "Mod Two"));

            source.GetProfile("a").LiveryId.ShouldBe("a");
            source.IsFrom("a", "mod-1").ShouldBeTrue();
            source.IsFrom("a", "mod-2").ShouldBeFalse();

            source.RemoveAllFrom("mod-1").ShouldBe(["a"]);
            source.GetProfile("a").ShouldBeNull();
            source.GetProfile("b").ShouldNotBeNull();
        }

        [Fact]
        public void BuiltInSource_RegisterAndGet()
        {
            var source = new BuiltInProfileSource();
            source.Register("a", Profile("a"));

            source.Get("a").LiveryId.ShouldBe("a");
            source.Get("missing").ShouldBeNull();
        }
    }
}
