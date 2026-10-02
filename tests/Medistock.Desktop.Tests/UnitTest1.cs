using System;
using Medistock.Desktop.ViewModels;
using Xunit;

namespace Medistock.Desktop.Tests;

public class SearchHighlightViewModelTests
{
    [Fact]
    public void BuildSegments_WhenQueryMatches_ProducesMatchAndNonMatchSegments()
    {
        var segments = ProductSearchItemViewModel.BuildSegments("Paracetamol 500mg", "para");

        Assert.NotNull(segments);
        Assert.True(segments.Count >= 2);

        var matchSegment = segments[0];
        Assert.True(matchSegment.IsMatch);
        Assert.Equal("#0D6EFD", matchSegment.ForegroundHex);
        Assert.Equal("#350D6EFD", matchSegment.BackgroundHex);
        Assert.Equal("Bold", matchSegment.FontWeight);

        var nonMatchSegment = segments[1];
        Assert.False(nonMatchSegment.IsMatch);
        Assert.Equal("TEXT_PRIMARY", nonMatchSegment.ForegroundHex);
        Assert.Equal("#00000000", nonMatchSegment.BackgroundHex);
        Assert.Equal("SemiBold", nonMatchSegment.FontWeight);
    }

    [Fact]
    public void BuildSegments_WhenSecondary_SetsTextSecondaryForNonMatch()
    {
        var segments = ProductSearchItemViewModel.BuildSegments("Acetaminophen Salt", "salt", isSecondary: true);

        Assert.NotNull(segments);
        var nonMatch = segments.Find(s => !s.IsMatch);
        Assert.NotNull(nonMatch);
        Assert.Equal("TEXT_SECONDARY", nonMatch.ForegroundHex);
    }
}
