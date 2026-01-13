using System.Text.RegularExpressions;

namespace Dependame.AutoMerge;

/// <summary>
/// Matches branch names against configured patterns.
/// Supports exact names, comma-separated lists, and glob patterns (e.g., "release/*").
/// </summary>
public class BranchMatcher
{
    private readonly IReadOnlyList<string> _patterns;

    public BranchMatcher(IReadOnlyList<string> patterns)
    {
        _patterns = patterns;
    }

    public bool IsMatch(string branchName)
    {
        if (_patterns.Count == 0)
            return false;

        foreach (var pattern in _patterns)
        {
            if (IsPatternMatch(pattern, branchName))
                return true;
        }

        return false;
    }

    private static bool IsPatternMatch(string pattern, string branchName)
    {
        // Exact match
        if (pattern.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Glob pattern matching (supports * and **)
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*\\*", ".*")      // ** matches anything including /
                .Replace("\\*", "[^/]*")      // * matches anything except /
                + "$";
            return Regex.IsMatch(branchName, regexPattern, RegexOptions.IgnoreCase);
        }

        return false;
    }
}
