using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

using Brushes     = System.Windows.Media.Brushes;
using SystemFonts = System.Windows.SystemFonts;

namespace SevenDaysManager.Controls;

/// <summary>
/// A text element with REAL letter-spacing (tracking), which WPF does not provide.
/// (dotnet/wpf#293 is still open; CharacterSpacing only exists in UWP/WinUI.)
///
/// Why this exists instead of the old TextEx attached property:
///
///   TextEx faked tracking by injecting Unicode space characters (U+2009 etc.) between glyphs.
///   That cannot work in a MONOSPACE font — every glyph, including a "thin" space, occupies
///   exactly one identical cell. So the inter-letter gap and the word gap rendered at the same
///   width and multi-word labels collapsed: "GAME SETTINGS" read as "GAMESETTINGS".
///
///   The only way to get true tracking is to lay out the glyphs ourselves and add the extra
///   advance between them. That is what this control does: it builds a GlyphRun and offsets
///   each glyph's advance width by (LetterSpacing em * FontSize), with word spaces additionally
///   widened by WordSpacingFactor so word boundaries stay legible.
///
/// Usage (literal or bound — both work, because Text is a normal DP with no side effects):
///     &lt;c:TrackedText Text="GAME SETTINGS" LetterSpacing="0.16"/&gt;
///     &lt;c:TrackedText Text="{Binding SelectedServer.Name}" LetterSpacing="0.16"/&gt;
/// </summary>
public class TrackedText : FrameworkElement
{
    // ── Text ──────────────────────────────────────────────────────────────────
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(TrackedText),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure |
                                              FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // ── LetterSpacing (em) ────────────────────────────────────────────────────
    public static readonly DependencyProperty LetterSpacingProperty =
        DependencyProperty.Register(nameof(LetterSpacing), typeof(double), typeof(TrackedText),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure |
                                              FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Tracking in em — 0.16 == 0.16em, matching the CSS values in the design spec.</summary>
    public double LetterSpacing
    {
        get => (double)GetValue(LetterSpacingProperty);
        set => SetValue(LetterSpacingProperty, value);
    }

    // ── WordSpacingFactor ─────────────────────────────────────────────────────
    public static readonly DependencyProperty WordSpacingFactorProperty =
        DependencyProperty.Register(nameof(WordSpacingFactor), typeof(double), typeof(TrackedText),
            new FrameworkPropertyMetadata(2.2, FrameworkPropertyMetadataOptions.AffectsMeasure |
                                               FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Extra multiplier applied to the space glyph's advance, so word gaps stay clearly wider
    /// than letter gaps. Without this, wide tracking makes multi-word labels read as one word.
    /// </summary>
    public double WordSpacingFactor
    {
        get => (double)GetValue(WordSpacingFactorProperty);
        set => SetValue(WordSpacingFactorProperty, value);
    }

    // ── Font / colour (mirror TextBlock's inherited properties) ───────────────
    public static readonly DependencyProperty FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner(typeof(TrackedText),
            new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.Inherits));

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public static readonly DependencyProperty FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner(typeof(TrackedText),
            new FrameworkPropertyMetadata(12d,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.Inherits));

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly DependencyProperty FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner(typeof(TrackedText),
            new FrameworkPropertyMetadata(FontWeights.Normal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.Inherits));

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public static readonly DependencyProperty FontStyleProperty =
        TextElement.FontStyleProperty.AddOwner(typeof(TrackedText),
            new FrameworkPropertyMetadata(FontStyles.Normal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.Inherits));

    public FontStyle FontStyle
    {
        get => (FontStyle)GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public static readonly DependencyProperty ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner(typeof(TrackedText),
            new FrameworkPropertyMetadata(Brushes.Black,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    // ── Layout / render ───────────────────────────────────────────────────────

    private GlyphRun? _run;
    private Size _runSize;
    private string? _builtFor;   // the text the cached run was built from

    protected override Size MeasureOverride(Size availableSize)
    {
        BuildGlyphRun();
        return _runSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Rebuild if the cached run doesn't match the current Text. Relying on MeasureOverride
        // alone is not enough: a binding can deliver Text after measure has already run (e.g. a
        // header supplied by a ControlTemplate), and AffectsRender re-renders WITHOUT re-measuring
        // — which would draw a stale run.
        if (_run is null || !string.Equals(_builtFor, Text, StringComparison.Ordinal))
            BuildGlyphRun();

        if (_run is null) return;
        dc.DrawGlyphRun(Foreground, _run);
    }

    private void BuildGlyphRun()
    {
        _run = null;
        _runSize = new Size(0, 0);

        var text = Text;
        _builtFor = text;

        if (string.IsNullOrEmpty(text)) return;

        var size = FontSize;
        if (double.IsNaN(size) || double.IsInfinity(size) || size <= 0) return;

        // FontFamily here is often a FALLBACK CHAIN ("Cascadia Mono, Consolas, ..."). WPF
        // resolves the chain for TextBlock, but GetGlyphTypeface needs a single real face, so
        // this can fail. Walk the chain and take the first family that actually resolves.
        var glyphTypeface = ResolveGlyphTypeface();
        if (glyphTypeface is null) return; // draw nothing rather than crash

        var track = LetterSpacing * size;           // em -> device-independent px
        var dpi   = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var indices  = new ushort[text.Length];
        var advances = new double[text.Length];

        double total = 0;
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(ch, out var gi) &&
                !glyphTypeface.CharacterToGlyphMap.TryGetValue(' ', out gi))
                gi = 0;   // .notdef — always present at index 0

            indices[i] = gi;

            var advance = glyphTypeface.AdvanceWidths.TryGetValue(gi, out var aw) ? aw * size : 0;

            // A space carries the word gap; widen it so word breaks survive wide tracking.
            if (ch == ' ')
                advance += track * WordSpacingFactor;
            else if (i < text.Length - 1)
                advance += track;   // no trailing track after the final glyph

            advances[i] = advance;
            total += advance;
        }

        var baseline = glyphTypeface.Baseline * size;
        var height   = glyphTypeface.Height   * size;

        try
        {
            _run = new GlyphRun(
                glyphTypeface,
                bidiLevel: 0,
                isSideways: false,
                renderingEmSize: size,
                pixelsPerDip: (float)dpi,
                glyphIndices: indices,
                baselineOrigin: new Point(0, baseline),
                advanceWidths: advances,
                glyphOffsets: null,
                characters: text.ToCharArray(),
                deviceFontName: null,
                clusterMap: null,
                caretStops: null,
                language: XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag));

            _runSize = new Size(total, height);
        }
        catch
        {
            // A malformed run must not take the whole app down during layout.
            _run = null;
            _runSize = new Size(0, 0);
        }
    }

    private GlyphTypeface? ResolveGlyphTypeface()
    {
        var family = FontFamily;
        if (family is null) return null;

        // Try the family as given first (handles a single real family name).
        if (new Typeface(family, FontStyle, FontWeight, FontStretches.Normal)
                .TryGetGlyphTypeface(out var gt))
            return gt;

        // Then each name in the fallback chain.
        foreach (var name in family.Source.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = new Typeface(new FontFamily(name.Trim()),
                                         FontStyle, FontWeight, FontStretches.Normal);
            if (candidate.TryGetGlyphTypeface(out gt))
                return gt;
        }

        // Last resort: whatever the system will give us.
        return new Typeface(SystemFonts.MessageFontFamily, FontStyle, FontWeight, FontStretches.Normal)
            .TryGetGlyphTypeface(out gt) ? gt : null;
    }
}
