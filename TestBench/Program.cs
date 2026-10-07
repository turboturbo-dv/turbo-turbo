using TestBench;

var once = Array.Exists(args, a => a == "--once");
var mode = args.Length > 0 && !args[0].StartsWith("--") ? args[0].ToLowerInvariant() : "thermal";

switch (mode)
{
    case "thermal":
        ThermalMode.Run(once);
        return 0;
    default:
        Console.WriteLine($"Unknown mode '{mode}'. Available modes: thermal");
        return 1;
}
