using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SevenDaysManager.Converters;

/// <summary>
/// Builds a chamfered (corner-cut) clip geometry for a control — the HUD's answer to a rounded
/// corner. Cuts the TOP-LEFT and BOTTOM-RIGHT corners at a fixed 45°, matching the RustPanel
/// primary button.
///
/// This has to be a converter rather than a fixed Path: a Path with Stretch="Fill" scales its
/// geometry to the element, so an "8px" chamfer drawn on a 100x100 path becomes an enormous
/// wedge once the button is 160px wide. Here the cut stays exactly ChamferSize px at any size.
///
/// Bind ActualWidth + ActualHeight through a MultiBinding; pass the cut size as the parameter.
/// </summary>
public class ChamferClipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not double w || values[1] is not double h ||
            double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0)
            return Geometry.Empty;

        var cut = 8.0;
        if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
            cut = p;

        // Never let the chamfer eat the whole control.
        cut = Math.Min(cut, Math.Min(w, h) / 2);

        var figure = new PathFigure { StartPoint = new Point(cut, 0), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment(new Point(w, 0), true));            // top edge
        figure.Segments.Add(new LineSegment(new Point(w, h - cut), true));       // right edge
        figure.Segments.Add(new LineSegment(new Point(w - cut, h), true));       // bottom-right chamfer
        figure.Segments.Add(new LineSegment(new Point(0, h), true));             // bottom edge
        figure.Segments.Add(new LineSegment(new Point(0, cut), true));           // left edge
        // closing segment returns to (cut, 0) — the top-left chamfer

        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
