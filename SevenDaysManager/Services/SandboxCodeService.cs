using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

/// <summary>
/// Reads and writes the V3.0 <c>SandboxCode</c> — the single string that now carries every
/// gameplay setting in <c>serverconfig.xml</c>.
///
/// <para><b>Why this exists.</b> V3.0 ("Dead Hot Summer") removed ~29 individual gameplay
/// properties — <c>GameDifficulty</c>, <c>XPMultiplier</c>, <c>ZombieMove</c>,
/// <c>BloodMoonFrequency</c>, <c>LootAbundance</c> and the rest. Writing them now does nothing:
/// the game reads <c>SandboxCode</c> instead. There is no longer a <c>GameDifficulty</c> setting
/// at all — the six difficulty levels are just presets over five damage multipliers.</para>
///
/// <para><b>Format.</b> A version character (<c>'A'</c>) followed by 3-character blocks:
/// two letters for the option's enum id in base-26 (<c>AA</c>=0, <c>BA</c>=26), then one letter
/// for the INDEX of the chosen value within that option's allowed list (<c>A</c>=0).
/// Options sitting at their default are omitted entirely — which is why the stock Adventurer
/// code is only 19 characters.</para>
///
/// <para><b>Verified.</b> Round-tripped against all 16 stock preset codes: 15 reproduce
/// byte-for-byte, and all 16 decode identically. (The 16th, Madmoles Mayhem, is hand-authored
/// with its blocks out of order; the game reads the code as a key/value list, so order does not
/// affect meaning, and ours is the canonical ordering.) See SandboxCodeServiceTests.</para>
/// </summary>
public static class SandboxCodeService
{
    private const char Version = 'A';
    private const char Base    = 'A';

    /// <summary>Encode an option id as two base-26 letters. 0 -> "AA", 26 -> "BA".</summary>
    private static string IdToCode(int id) =>
        $"{(char)(Base + id / 26)}{(char)(Base + id % 26)}";

    private static int CodeToId(char hi, char lo) =>
        (hi - Base) * 26 + (lo - Base);

    /// <summary>
    /// Decode a code into <c>option id -> value index</c>.
    ///
    /// Unknown ids and out-of-range indexes are skipped rather than throwing: a code may come
    /// from a newer game build that knows options we don't, and dropping the whole config
    /// because of one unrecognised block would be worse than ignoring it.
    /// </summary>
    public static Dictionary<int, int> Decode(string? code)
    {
        var result = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(code)) return result;

        code = code.Trim();
        var body = code.Length > 0 && char.IsLetter(code[0]) ? code[1..] : code;

        for (var i = 0; i + 3 <= body.Length; i += 3)
        {
            var id = CodeToId(body[i], body[i + 1]);
            var vi = body[i + 2] - Base;

            var opt = SandboxSettings.ById(id);
            if (opt is null) continue;                       // option we don't know
            if (vi < 0 || vi >= opt.Values.Count) continue;  // index we can't resolve

            result[id] = vi;
        }

        return result;
    }

    /// <summary>
    /// Encode <c>option id -> value index</c> back into a code.
    ///
    /// Blocks are emitted in the game's UI order (not enum-id order) — that's what the stock
    /// presets do. Defaults are dropped, except the six difficulty fields, which the game always
    /// writes even at default.
    /// </summary>
    public static string Encode(IReadOnlyDictionary<int, int> values)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Version);

        var ordered = values.Keys
            .Where(id => SandboxSettings.ById(id) is not null)
            .OrderBy(SandboxSettings.UiIndex);

        foreach (var id in ordered)
        {
            var opt = SandboxSettings.ById(id)!;
            var vi  = values[id];
            if (vi < 0 || vi >= opt.Values.Count) continue;

            if (vi == opt.DefaultIndex && !SandboxSettings.AlwaysWritten.Contains(opt.Name))
                continue;

            sb.Append(IdToCode(id)).Append((char)(Base + vi));
        }

        return sb.ToString();
    }

    /// <summary>Decode to <c>option name -> actual value</c>, for display.</summary>
    public static Dictionary<string, double> DecodeToValues(string? code)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, vi) in Decode(code))
        {
            var opt = SandboxSettings.ById(id)!;
            result[opt.Name] = opt.Values[vi];
        }
        return result;
    }

    /// <summary>
    /// True if the string is a well-formed code: a version letter plus whole 3-char blocks of
    /// letters. Used to reject junk on import BEFORE it can reach serverconfig.xml.
    /// </summary>
    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();

        // Version char + n complete blocks
        if (code.Length < 1 || (code.Length - 1) % 3 != 0) return false;
        if (!code.All(c => c is >= 'A' and <= 'Z')) return false;

        // Every block must name an option we know, with an index it actually allows.
        var body = code[1..];
        for (var i = 0; i + 3 <= body.Length; i += 3)
        {
            var opt = SandboxSettings.ById(CodeToId(body[i], body[i + 1]));
            if (opt is null) return false;
            var vi = body[i + 2] - Base;
            if (vi < 0 || vi >= opt.Values.Count) return false;
        }

        return true;
    }
}
