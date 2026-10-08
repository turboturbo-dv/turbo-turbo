using Spectre.Console;

namespace TestBench
{
    /// <summary>Small helpers shared by the Spectre.Console report renderers.</summary>
    internal static class Render
    {
        public static TableColumn Num(string header) => new TableColumn(header).RightAligned();

        public static TableColumn Name(string header) => new TableColumn(header);

        public static string Duration(float seconds) =>
            seconds < 0f ? "[dim]n/a[/]" : BenchCommon.Fmt(seconds);

        public static string Fraction(float value) =>
            value < 0.999f ? $"[yellow]{value:0.000}[/]" : $"{value:0.000}";
    }
}
