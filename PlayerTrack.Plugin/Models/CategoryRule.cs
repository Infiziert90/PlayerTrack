using System;

namespace PlayerTrack.Models;

/// <summary>
/// A single keyword-to-category mapping rule evaluated against
/// a player's adventurer plate bio/comment field.
/// </summary>
[Serializable]
public sealed class CategoryRule
{
    /// <summary>The keyword or phrase to search for in the plate bio.</summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// When true, the search is case-sensitive.
    /// When false (default), the match is case-insensitive.
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// When true, the keyword must appear as a whole word (surrounded by
    /// whitespace or punctuation). When false (default), a substring match
    /// is sufficient.
    /// </summary>
    public bool WholeWord { get; set; } = false;

    /// <summary>
    /// The numeric category ID as defined in PlayerTrack.
    /// This value is set from the category picker in the rule editor.
    /// </summary>
    public uint CategoryId { get; set; } = 0;

    /// <summary>
    /// Human-readable display name of the category, cached for the UI.
    /// This is NOT authoritative -- the canonical name is fetched live from
    /// PlayerTrack's IPC on each UI refresh.
    /// </summary>
    public string CategoryDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// When false, this rule is skipped during evaluation without being deleted.
    /// Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Returns a short textual summary suitable for log output.
    /// </summary>
    public override string ToString() =>
        $"Rule[{(Enabled ? "ON" : "OFF")} | keyword=\"{Keyword}\" " +
        $"case={CaseSensitive} whole={WholeWord} => categoryId={CategoryId}]";
}
