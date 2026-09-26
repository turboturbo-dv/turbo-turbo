using Shouldly;

using TurboTurbo.Runtime;

using Xunit;

namespace TurboTurboTests
{
    public class ExhaustTargetsTests
    {
        [Fact]
        public void PickDefault_NoNames_ReturnsMinusOne()
        {
            ExhaustTargets.PickDefault([]).ShouldBe(-1);
            ExhaustTargets.PickDefault(null).ShouldBe(-1);
        }

        [Fact]
        public void PickDefault_PrefersExhaustEngineSmoke()
        {
            string[] names = ["Body", "ExhaustEngineSmoke(Clone)", "ExhaustPipe"];

            ExhaustTargets.PickDefault(names).ShouldBe(1);
        }

        [Fact]
        public void PickDefault_FallsBackToExhaust()
        {
            string[] names = ["Body", "SomeExhaustThing"];

            ExhaustTargets.PickDefault(names).ShouldBe(1);
        }

        [Fact]
        public void PickDefault_FallsBackToFirst()
        {
            string[] names = ["Body", "Chimney"];

            ExhaustTargets.PickDefault(names).ShouldBe(0);
        }

        [Fact]
        public void PickDefault_IsCaseInsensitive()
        {
            string[] names = ["body", "exhaustenginesmoke"];

            ExhaustTargets.PickDefault(names).ShouldBe(1);
        }

        [Fact]
        public void PickDefault_FirstMatchWins()
        {
            string[] names = ["ExhaustEngineSmokeA", "ExhaustEngineSmokeB"];

            ExhaustTargets.PickDefault(names).ShouldBe(0);
        }
    }
}
