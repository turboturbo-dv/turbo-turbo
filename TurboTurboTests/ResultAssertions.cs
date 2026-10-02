using System.Diagnostics;

using Shouldly;

using TurboTurbo;

namespace TurboTurboTests;

[ShouldlyMethods]
internal static class ResultAssertions
{
    /// <summary>Asserts success, returning the value for further chaining.</summary>
    public static T ShouldSucceed<T>(this Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    /// <summary>Asserts failure, returning the error for further chaining.</summary>
    public static Error ShouldFail<T>(this Result<T> result)
    {
        result.IsSuccess.ShouldBeFalse();
        return result.Error!.Value;
    }
}
