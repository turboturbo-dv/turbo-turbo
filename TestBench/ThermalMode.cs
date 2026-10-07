using System;
using System.Text;
using System.Threading;

using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    public static class ThermalMode
    {
        private sealed class Field
        {
            public string Label;
            public float Default;
            public Func<float> Get;
            public Action<float> Set;
        }

        public static void Run(bool once)
        {
            var settings = new CombustionModel.Settings();
            var bench = new ThermalBench(settings);
            var ambientK = PhysicsConstants.ReferenceAmbientK;

            var fields = new[]
            {
                new Field { Label = "Rated exhaust T [K]", Default = settings.RatedExhaustTempK, Get = () => settings.RatedExhaustTempK, Set = v => settings.RatedExhaustTempK = v },
                new Field { Label = "Cylinder tau [s]", Default = settings.TauCylinder, Get = () => settings.TauCylinder, Set = v => settings.TauCylinder = v },
                new Field { Label = "Block tau [s]", Default = settings.TauEngine, Get = () => settings.TauEngine, Set = v => settings.TauEngine = v },
                new Field { Label = "Cooldown open tau [s]", Default = settings.TauCooldownOpen, Get = () => settings.TauCooldownOpen, Set = v => settings.TauCooldownOpen = v },
                new Field { Label = "Cooldown closed tau [s]", Default = settings.TauCooldownClosed, Get = () => settings.TauCooldownClosed, Set = v => settings.TauCooldownClosed = v },
                new Field { Label = "Cylinder gain [K]", Default = settings.CylinderGainK, Get = () => settings.CylinderGainK, Set = v => settings.CylinderGainK = v },
                new Field { Label = "Cold wall floor [K]", Default = settings.ColdWallFloorK, Get = () => settings.ColdWallFloorK, Set = v => settings.ColdWallFloorK = v },
                new Field { Label = "Warm wall target [K]", Default = settings.WarmWallTargetK, Get = () => settings.WarmWallTargetK, Set = v => settings.WarmWallTargetK = v },
                new Field { Label = "Min burn at cold", Default = settings.MinBurnFractionAtCold, Get = () => settings.MinBurnFractionAtCold, Set = v => settings.MinBurnFractionAtCold = v },
            };

            if (once)
            {
                Console.Write(BuildView(settings, fields, -1, BuildReports(bench, ambientK), showHelp: false));
                return;
            }

            var selected = 0;
            string reports = null;
            var settingsDirty = true;
            var viewDirty = true;

            Console.CursorVisible = false;
            Console.Clear();

            var quit = false;
            while (!quit)
            {
                while (Console.KeyAvailable)
                {
                    switch (Console.ReadKey(true).Key)
                    {
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.W:
                            selected = Math.Max(0, selected - 1);
                            viewDirty = true;
                            break;
                        case ConsoleKey.DownArrow:
                        case ConsoleKey.S:
                            selected = Math.Min(fields.Length - 1, selected + 1);
                            viewDirty = true;
                            break;
                        case ConsoleKey.RightArrow:
                        case ConsoleKey.D:
                        case ConsoleKey.Add:
                        case ConsoleKey.OemPlus:
                            Adjust(settings, fields[selected], 1f);
                            settingsDirty = true;
                            viewDirty = true;
                            break;
                        case ConsoleKey.LeftArrow:
                        case ConsoleKey.A:
                        case ConsoleKey.Subtract:
                        case ConsoleKey.OemMinus:
                            Adjust(settings, fields[selected], -1f);
                            settingsDirty = true;
                            viewDirty = true;
                            break;
                        case ConsoleKey.R:
                            settingsDirty = true;
                            viewDirty = true;
                            break;
                        case ConsoleKey.Q:
                        case ConsoleKey.Escape:
                            quit = true;
                            break;
                    }
                }

                if (viewDirty)
                {
                    if (settingsDirty || reports == null)
                    {
                        reports = BuildReports(bench, ambientK);
                        settingsDirty = false;
                    }

                    Console.Clear();
                    Console.Write(BuildView(settings, fields, selected, reports, showHelp: true));
                    viewDirty = false;
                }

                Thread.Sleep(30);
            }

            Console.CursorVisible = true;
            Console.Clear();
        }

        private static void Adjust(CombustionModel.Settings settings, Field field, float direction)
        {
            var current = field.Get();
            field.Set(direction > 0f ? current * 1.1f : current / 1.1f);
            settings.Validate();
        }

        private static string BuildReports(ThermalBench bench, float ambientK)
        {
            var sb = new StringBuilder();
            sb.Append(bench.OperatingPointsReport(ambientK));
            sb.AppendLine();
            sb.Append(bench.ThrottleStepReport(ambientK));
            sb.AppendLine();
            sb.Append(bench.AmbientSensitivityReport());
            sb.AppendLine();
            sb.Append(bench.CooldownReport(ambientK));
            return sb.ToString();
        }

        private static string BuildView(CombustionModel.Settings settings, Field[] fields, int selected,
            string reports, bool showHelp)
        {
            var sb = new StringBuilder();
            sb.AppendLine("TurboTurbo thermal TestBench");
            sb.AppendLine("============================");
            if (showHelp)
            {
                sb.AppendLine("Up/Down  select setting        Left/Right  -9.09% / +10%");
                sb.AppendLine("R        recompute             Q / Esc     quit");
                sb.AppendLine("*        marks a setting changed from its default");
            }

            sb.AppendLine();
            sb.AppendLine("Settings");
            for (var i = 0; i < fields.Length; i++)
            {
                var changed = Math.Abs(fields[i].Get() - fields[i].Default) > Math.Abs(fields[i].Default) * 1e-3f;
                sb.AppendLine($"{(changed ? "*" : " ")}{(i == selected ? ">" : " ")} {fields[i].Label,-24} {fields[i].Get(),10:0.###}");
            }

            sb.AppendLine();
            sb.Append(reports);
            return sb.ToString();
        }
    }
}
