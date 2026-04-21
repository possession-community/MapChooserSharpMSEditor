using System;
using System.Collections.Generic;
using System.Globalization;

namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// Tokenizes the search box input into whitespace-separated terms. Quoted substrings
/// survive as single tokens so users can search for names containing spaces. Typed
/// predicates live on <see cref="ViewModels.SearchViewModel"/> as explicit properties
/// (the Advanced form), keeping this class focused on just text splitting.
/// </summary>
public static class SearchQuery
{
    public static List<string> Tokenize(string? raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        var sb = new System.Text.StringBuilder();
        var inQuote = false;
        foreach (var c in raw)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
                continue;
            }
            if (!inQuote && char.IsWhiteSpace(c))
            {
                if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) result.Add(sb.ToString());
        return result;
    }
}

public enum NumericOp { Any, Eq, Gt, Ge, Lt, Le }
public enum TriState { Any, Yes, No }

/// <summary>
/// Numeric-op helpers shared between the Advanced form and the matcher. "Any" means the
/// filter is disabled and should be skipped entirely — the UI lets the user pick this to
/// blank out a numeric filter without clearing the value.
/// </summary>
public static class SearchNumericOps
{
    public static bool Compare(long actual, NumericOp op, long target) => op switch
    {
        NumericOp.Any => true,
        NumericOp.Eq => actual == target,
        NumericOp.Gt => actual > target,
        NumericOp.Ge => actual >= target,
        NumericOp.Lt => actual < target,
        NumericOp.Le => actual <= target,
        _ => false,
    };

    /// <summary>Parses the user's numeric input. Empty or unparseable yields null, which
    /// the matcher treats as "no filter".</summary>
    public static long? TryParseLong(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
