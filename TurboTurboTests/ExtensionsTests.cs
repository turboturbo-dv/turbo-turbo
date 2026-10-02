using System;

using Shouldly;

using TurboTurbo;

using Xunit;

namespace TurboTurboTests;

public class ExtensionsTests
{
    [Fact]
    public void FirstOrNull_ReturnsMatchingValue()
    {
        var values = new[] { 1, 2, 3 };

        values.FirstOrNull(v => v == 2).ShouldBe(2);
    }

    [Fact]
    public void FirstOrNull_NoMatch_ReturnsNull()
    {
        var values = new[] { 1, 2, 3 };

        values.FirstOrNull(v => v == 9).ShouldBeNull();
    }

    [Fact]
    public void FirstOrNull_Empty_ReturnsNull()
    {
        Array.Empty<int>().FirstOrNull(_ => true).ShouldBeNull();
    }
}
