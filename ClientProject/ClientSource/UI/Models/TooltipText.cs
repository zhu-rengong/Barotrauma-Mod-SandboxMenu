using System.Globalization;

namespace SandboxMenu.UI.Models;

internal static class TooltipText
{
    internal const string ValuePlaceholder = "[value]";

    internal static LocalizedString Field(string labelKey, LocalizedString value)
        => TextManager.GetWithVariable(labelKey, ValuePlaceholder, value);

    internal static LocalizedString FieldText(string labelKey, string value)
        => TextManager.GetWithVariable(labelKey, ValuePlaceholder, value);

    internal static LocalizedString Colored(string key, Color color)
        => Colorize(TextManager.Get(key), color);

    internal static LocalizedString Colorize(LocalizedString text, Color color)
        => "‖color:" + color.ToStringHex() + "‖" + text + "‖color:end‖";

    internal static LocalizedString Dim(LocalizedString text)
        => "‖color:" + UiMetrics.TextDim.ToStringHex() + "‖" + text + "‖color:end‖";

    internal static LocalizedString Bright(LocalizedString text)
        => "‖color:" + ((Color)GUIStyle.TextColorBright).ToStringHex() + "‖" + text + "‖color:end‖";

    internal static string Number(float value, string format = "0.#")
        => value.ToString(format, CultureInfo.InvariantCulture);
}
