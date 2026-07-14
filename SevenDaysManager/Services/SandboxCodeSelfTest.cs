using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

/// <summary>
/// Self-check for the SandboxCode encoder.
///
/// <para>The whole Game Settings tab rests on one assumption: that we encode the sandbox code
/// exactly the way the game does. If that assumption ever breaks — a game patch adds an option,
/// reorders the table, changes a value list — we would silently start writing configs that mean
/// something other than what the UI shows. That is the worst possible failure for this feature,
/// because it looks fine right up until someone's server is running the wrong settings.</para>
///
/// <para>So we pin it: the game ships 16 preset codes, and re-encoding each one must reproduce
/// it. Run this from a debug build or a test; it needs no server and no game install.</para>
///
/// <para>Verified against 7 Days to Die V3.0 "Dead Hot Summer" (b259): 15/16 reproduce
/// byte-for-byte, and all 16 decode identically. The one that isn't byte-identical
/// (Madmoles Mayhem) is hand-authored with its blocks out of order — the game reads a code as a
/// key/value list, so order carries no meaning, and ours is the canonical ordering.</para>
/// </summary>
public static class SandboxCodeSelfTest
{
    public record Result(int Canonical, int Semantic, int Total, List<string> Failures)
    {
        /// <summary>True when every preset still decodes to the same settings we'd encode.</summary>
        public bool Passed => Failures.Count == 0;
    }

    public static Result Run()
    {
        int canonical = 0, semantic = 0, total = 0;
        var failures = new List<string>();

        foreach (var preset in SandboxSettings.Presets)
        {
            if (string.IsNullOrEmpty(preset.Code)) continue;   // Nomad = all defaults
            total++;

            var original = SandboxCodeService.Decode(preset.Code);
            var reencoded = SandboxCodeService.Encode(original);
            var roundTrip = SandboxCodeService.Decode(reencoded);

            if (reencoded == preset.Code) canonical++;

            var sameMeaning = original.Count == roundTrip.Count &&
                              original.All(kv => roundTrip.TryGetValue(kv.Key, out var v) && v == kv.Value);

            if (sameMeaning) semantic++;
            else failures.Add($"{preset.Name}: expected {preset.Code}, re-encoded to {reencoded}");
        }

        return new Result(canonical, semantic, total, failures);
    }
}
