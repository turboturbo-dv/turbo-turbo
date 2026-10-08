using TestBench;

var once = Array.Exists(args, a => a == "--once");
var mode = args.Length > 0 && !args[0].StartsWith("--") ? args[0].ToLowerInvariant() : "engine";

switch (mode)
{
    case "engine":
    case "thermal":
        EngineMode.Run(once);
        return 0;
    case "stack":
        StackMode.Run(once);
        return 0;
    case "sim":
        SimMode.Run(args);
        return 0;
    default:
        Console.WriteLine($"Unknown mode '{mode}'. Available modes: engine, stack, sim");
        return 1;
}
