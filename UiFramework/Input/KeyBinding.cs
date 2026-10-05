using System.Windows.Input;
using KeyCode = Microsoft.Xna.Framework.Input.Keys;

namespace UiFramework.Input;

// One key (or several spellings of it) running a command while the element it is declared on is in sight. A binding
// that names modifiers takes precedence over one that does not, which is how a key does two things: the arrows walk
// the list, and the arrows with Alt move the entry inside it.
internal sealed class KeyBinding
{
    internal ViewElement Owner { get; init; } = null!;

    internal KeyCode[] Keys { get; init; } = [];

    internal KeyCode[] Modifiers { get; init; } = [];

    internal ICommand? Command { get; set; }

    internal object? Parameter { get; init; }

    // A key that types (Delete, "+") stands back while a text box has the keyboard; an arrow commands either way.
    internal bool RequiresNoTextInput { get; init; }

    internal int Specificity => Modifiers.Length;

    internal bool Held() => Modifiers.Length == 0 || Modifiers.All(PlayerInput.KeyDown);

    internal bool Pressed() => Keys.Any(PlayerInput.KeyHit);

    internal void Fire() => Command?.Execute(Parameter);

    internal static KeyBinding? Read(MarkupNode node, ViewElement owner)
    {
        KeyCode[] keys = ParseKeys(node.Text("Key"));
        if (keys.Length == 0) { return null; }

        return new KeyBinding
        {
            Owner = owner,
            Keys = keys,
            Modifiers = ParseModifiers(node.Text("Modifiers")),
            Parameter = node.Text("Parameter"),
            RequiresNoTextInput = ViewMarkup.ToBool(node.Text("RequiresNoTextInput"), false)
        };
    }

    private static KeyCode[] ParseKeys(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(name => Enum.TryParse(name, ignoreCase: true, out KeyCode _))
                .Select(name => Enum.Parse<KeyCode>(name, ignoreCase: true))];

    private static KeyCode[] ParseModifiers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return []; }

        List<KeyCode> modifiers = [];

        foreach (string name in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (name.ToLowerInvariant())
            {
                case "alt": modifiers.Add(KeyCode.LeftAlt); modifiers.Add(KeyCode.RightAlt); break;
                case "shift": modifiers.Add(KeyCode.LeftShift); modifiers.Add(KeyCode.RightShift); break;
                case "ctrl" or "control": modifiers.Add(KeyCode.LeftControl); modifiers.Add(KeyCode.RightControl); break;
                default: break;
            }
        }

        return [.. modifiers];
    }
}
