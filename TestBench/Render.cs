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

        /// <summary>Highlights an ignition margin below the spread, where some fuel misses the ignition window.</summary>
        public static string Margin(float margin, float spread) =>
            margin < spread ? $"[yellow]{margin:0.00}[/]" : $"{margin:0.00}";

        public static string Unburned(float relative) =>
            relative > 0.05f ? $"[yellow]{relative:0.00}[/]" : $"{relative:0.00}";
    }
}
