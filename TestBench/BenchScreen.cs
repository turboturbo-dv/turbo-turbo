using System;
using System.Threading;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace TestBench
{
    /// <summary>Shared console screen for a bench: an editable field list plus its report.</summary>
    public sealed class BenchScreen
    {
        private const string HelpText =
            "[grey]Up/Down  select setting        Left/Right  -9.09% / +10%[/]\n" +
            "[grey]R        recompute             Q / Esc     quit[/]\n" +
            "[grey]*        marks a setting changed from its default[/]";

        public sealed class Field
        {
            public string Label;
            public string Description;
            public float Default;
            public Func<float> Get;
            public Action<float> Set;
        }

        private readonly string _title;
        private readonly Field[] _fields;
        private readonly Func<IRenderable> _report;
        private readonly Action _validate;

        public BenchScreen(string title, Field[] fields, Func<IRenderable> report, Action validate)
        {
            _title = title;
            _fields = fields;
            _report = report;
            _validate = validate;
        }

        public void Run(bool once)
        {
            if (once)
            {
                AnsiConsole.Write(_report());
                return;
            }

            var selected = 0;
            var report = _report();
            var settingsDirty = false;

            // Live repositions the cursor and overwrites the previous frame instead of
            // clearing, which is what keeps the redraw from flashing.
            AnsiConsole.Live(BuildRoot(selected, report)).Start(ctx =>
            {
                ctx.Refresh();

                var quit = false;
                while (!quit)
                {
                    var changed = false;
                    while (Console.KeyAvailable)
                    {
                        switch (Console.ReadKey(true).Key)
                        {
                            case ConsoleKey.UpArrow:
                            case ConsoleKey.W:
                                selected = Math.Max(0, selected - 1);
                                changed = true;
                                break;
                            case ConsoleKey.DownArrow:
                            case ConsoleKey.S:
                                selected = Math.Min(_fields.Length - 1, selected + 1);
                                changed = true;
                                break;
                            case ConsoleKey.RightArrow:
                            case ConsoleKey.D:
                            case ConsoleKey.Add:
                            case ConsoleKey.OemPlus:
                                Adjust(selected, 1f);
                                settingsDirty = true;
                                changed = true;
                                break;
                            case ConsoleKey.LeftArrow:
                            case ConsoleKey.A:
                            case ConsoleKey.Subtract:
                            case ConsoleKey.OemMinus:
                                Adjust(selected, -1f);
                                settingsDirty = true;
                                changed = true;
                                break;
                            case ConsoleKey.R:
                                settingsDirty = true;
                                changed = true;
                                break;
                            case ConsoleKey.Q:
                            case ConsoleKey.Escape:
                                quit = true;
                                break;
                        }
                    }

                    if (changed)
                    {
                        if (settingsDirty)
                        {
                            report = _report();
                            settingsDirty = false;
                        }

                        ctx.UpdateTarget(BuildRoot(selected, report));
                    }

                    Thread.Sleep(30);
                }
            });
        }

        private IRenderable BuildRoot(int selected, IRenderable report)
        {
            var columns = new Grid();
            columns.AddColumn();
            columns.AddColumn(new GridColumn { Width = 46 });
            columns.AddRow(BuildSettings(selected), BuildInfo(selected));

            return new Rows(new IRenderable[]
            {
                new Rule($"[yellow]{_title}[/]").LeftJustified(),
                columns,
                new Markup(HelpText),
                report,
            });
        }

        private IRenderable BuildInfo(int selected)
        {
            var field = _fields[selected];
            IRenderable body = string.IsNullOrEmpty(field.Description)
                ? new Markup("[grey]no description[/]")
                : new Markup(Markup.Escape(field.Description));

            return new Panel(body)
                .Header(Markup.Escape(field.Label))
                .RoundedBorder();
        }

        private Table BuildSettings(int selected)
        {
            var table = new Table().Border(TableBorder.None).HideHeaders();
            table.AddColumn("");
            table.AddColumn("setting");
            table.AddColumn(Render.Num("value"));

            for (var i = 0; i < _fields.Length; i++)
            {
                var changed = Math.Abs(_fields[i].Get() - _fields[i].Default) > Math.Abs(_fields[i].Default) * 1e-3f;
                var marker = i == selected ? "[blue]»[/]" : " ";
                var label = Markup.Escape(_fields[i].Label);
                if (changed) label = $"[yellow]{label}[/]";
                var value = changed ? $"[yellow]{_fields[i].Get():0.###}[/]" : $"{_fields[i].Get():0.###}";
                table.AddRow(marker, label, value);
            }

            return table;
        }

        private void Adjust(int index, float direction)
        {
            var field = _fields[index];
            var current = field.Get();
            field.Set(direction > 0f ? current * 1.1f : current / 1.1f);
            _validate();
        }
    }
}
