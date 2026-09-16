using Shouldly;

using TurboTurbo.Modeling;

using UnityEngine;

using Xunit;

namespace TurboTurboTests
{
    public class ColorHexTests
    {
        [Theory]
        [InlineData("9E9678FF", 158, 150, 120, 255)]
        [InlineData("#9E9678FF", 158, 150, 120, 255)]
        [InlineData("FF0000", 255, 0, 0, 255)]
        public void Parse_ValidHex(string hex, int r, int g, int b, int a)
        {
            ColorHex.Parse(hex).ShouldBe(new Color(r / 255f, g / 255f, b / 255f, a / 255f));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("nope")]
        [InlineData("12345")]
        [InlineData("1234567")]
        public void Parse_Invalid_ReturnsMagenta(string hex)
        {
            // magenta is a good error colour
            ColorHex.Parse(hex).ShouldBe(Color.magenta);
        }

        [Theory]
        [InlineData("9E9678FF")]
        [InlineData("00000000")]
        [InlineData("FFFFFFFF")]
        [InlineData("7085D9FF")]
        public void Format_RoundTripsParsedHex(string hex)
        {
            ColorHex.Format(ColorHex.Parse(hex)).ShouldBe(hex);
        }

        [Fact]
        public void Format_ClampsAndRounds()
        {
            ColorHex.Format(new Color(2f, -1f, 0.5f, 1f)).ShouldBe("FF0080FF");
        }
    }
}
