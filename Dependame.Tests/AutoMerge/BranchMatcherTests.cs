using Dependame.AutoMerge;

namespace Dependame.Tests.AutoMerge;

public class BranchMatcherTests
{
    [Theory]
    [InlineData("main", "main", true)]
    [InlineData("main", "Main", true)]
    [InlineData("main", "develop", false)]
    [InlineData("release/*", "release/v1.0", true)]
    [InlineData("release/*", "release/v2.0.1", true)]
    [InlineData("release/*", "release/", true)]
    [InlineData("release/*", "main", false)]
    [InlineData("release/*", "release/foo/bar", false)]
    [InlineData("feature/**", "feature/abc", true)]
    [InlineData("feature/**", "feature/abc/def", true)]
    [InlineData("*", "anything", true)]
    [InlineData("*", "main", true)]
    public void IsMatch_SinglePattern_ReturnsExpectedResult(string pattern, string branch, bool expected)
    {
        var matcher = new BranchMatcher(new[] { pattern });
        Assert.Equal(expected, matcher.IsMatch(branch));
    }

    [Fact]
    public void IsMatch_WithMultiplePatterns_MatchesAny()
    {
        var matcher = new BranchMatcher(new[] { "main", "develop", "release/*" });

        Assert.True(matcher.IsMatch("main"));
        Assert.True(matcher.IsMatch("develop"));
        Assert.True(matcher.IsMatch("release/v1.0"));
        Assert.False(matcher.IsMatch("feature/xyz"));
    }

    [Fact]
    public void IsMatch_WithEmptyPatterns_ReturnsFalse()
    {
        var matcher = new BranchMatcher(Array.Empty<string>());
        Assert.False(matcher.IsMatch("main"));
    }

    [Fact]
    public void IsMatch_CaseInsensitive()
    {
        var matcher = new BranchMatcher(new[] { "Main" });
        Assert.True(matcher.IsMatch("main"));
        Assert.True(matcher.IsMatch("MAIN"));
        Assert.True(matcher.IsMatch("Main"));
    }
}
