using System.Windows.Input;
using KeyCode = Microsoft.Xna.Framework.Input.Keys;

namespace UiFramework.Input;

// One key (or several spellings of it) running a command while the element it is declared on is in sight. A binding
// that names modifiers takes precedence over one that does not, which is how a key does two things: the arrows walk
// the list, and the arrows with Alt move the entry inside it.
internal sealed class KeyBinding
{
    internal ViewElement Owner { get; init; } = null!;

    // The spellings of the key itself: any one of them being hit counts as the key.
    internal KeyCode[] Keys { get; init; } = [];

    // One entry per modifier named, each carrying that modifier's own spellings: every entry has to be held, and
    // within an entry any spelling will do. Alt is one entry of left and right, not a demand for both at once.
    internal KeyCode[][] Modifiers { get; init; } = [];

    internal ICommand? Command { get; set; }

    internal object? Parameter { get; init; }

    // A key that types (Delete, "+") stands back while a text box has the keyboard; an arrow commands either way.
    internal bool RequiresNoTextInput { get; init; }

    internal int Specificity => Modifiers.Length;

    internal bool Held() => Modifiers.All(group => group.Any(PlayerInput.KeyDown));

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

    private static KeyCode[][] ParseModifiers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return []; }

        List<KeyCode[]> modifiers = [];

        foreach (string name in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (name.ToLowerInvariant())
            {
                case "alt": modifiers.Add([KeyCode.LeftAlt, KeyCode.RightAlt]); break;
                case "shift": modifiers.Add([KeyCode.LeftShift, KeyCode.RightShift]); break;
                case "ctrl" or "control": modifiers.Add([KeyCode.LeftControl, KeyCode.RightControl]); break;
                default: break;
            }
        }

        return [.. modifiers];
    }
}
