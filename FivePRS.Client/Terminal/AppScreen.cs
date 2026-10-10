using System.Collections.Generic;
using System.Linq;

namespace FivePRS.Client.Terminal
{
    public sealed class AppScreen
    {
        public string? Title { get; set; }

        public List<AppBlock> Blocks { get; set; } = new();

        public AppScreen Add(AppBlock block)
        {
            Blocks.Add(block);
            return this;
        }
    }

    public sealed class AppBlock
    {
        public string Type { get; set; } = "text";

        public string? Title { get; set; }

        public string? Text { get; set; }

        public bool Muted { get; set; }

        public string? Layout { get; set; }

        public string? Action { get; set; }

        public string? Submit { get; set; }

        public List<AppItem> Items { get; set; } = new();

        public List<AppField> Fields { get; set; } = new();

        public static AppBlock Section(string title) => new() { Type = "section", Title = title };

        public static AppBlock Paragraph(string text, bool muted = false) => new() { Type = "text", Text = text, Muted = muted };

        public static AppBlock Buttons(params AppItem[] buttons) => new() { Type = "buttons", Items = buttons.ToList() };

        public static AppBlock List(IEnumerable<AppItem> items, bool grid = false) =>
            new() { Type = "list", Layout = grid ? "grid" : "rows", Items = items.ToList() };

        public static AppBlock Form(string action, string submit, params AppField[] fields) =>
            new() { Type = "form", Action = action, Submit = submit, Fields = fields.ToList() };
    }

    public sealed class AppItem
    {
        public string Title { get; set; } = string.Empty;

        public string? Detail { get; set; }

        public string? Image { get; set; }

        public string? Action { get; set; }

        public Dictionary<string, object> Data { get; set; } = new();

        public bool Active { get; set; }

        public bool Disabled { get; set; }

        public string? Style { get; set; }
    }

    public sealed class AppField
    {
        public string Name { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string? Placeholder { get; set; }

        public string? Value { get; set; }

        public string Type { get; set; } = "text";
    }
}
