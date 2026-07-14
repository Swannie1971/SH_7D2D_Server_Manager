using System.Globalization;
using System.Windows.Data;

namespace SevenDaysManager.Converters;

/// <summary>
/// Renders a ComboBox item as text: uses its <c>Label</c> property if it has one, otherwise the
/// item itself.
///
/// Why this exists: <c>Hud.ComboBox</c> supplies a custom <c>ControlTemplate</c>, and WPF only
/// populates the (read-only) <c>SelectionBoxItemTemplate</c> for its own default template. That
/// means <c>DisplayMemberPath</c> is silently ignored and the selection box falls back to
/// <c>ToString()</c> — printing e.g. "LabeledValue { Value = 2, Label = Insane }". So the style
/// drives the selection box from <c>ItemTemplate</c> instead, and this converter lets that one
/// template serve both shapes we bind:
///
///   • option records — <c>record LabeledValue(int Value, string Label)</c>  -> "Insane"
///   • plain string lists (e.g. world names)                                  -> "RWG"
///
/// A XAML DataTrigger cannot do this: binding <c>Label</c> against a string raises a binding
/// error rather than yielding null, so the trigger never fires and the item renders blank.
/// (Verified by test.)
/// </summary>
public class ComboItemLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return null;

        var label = value.GetType().GetProperty("Label")?.GetValue(value);
        return label ?? value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
