using DaS.NoDiscardAnalyzer.Utilities;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace DaS.NoDiscardAnalyzer.Tests.Utilities;

public sealed class EnumerableExtensionTests
{
    [Fact]
    public void FilterNullFiltersRefTypes()
    {
        IEnumerable<string?> source = new[] {"Hello", null, "World"};
        IEnumerable<string> result = source.FilterNull();
        result.Should().BeEquivalentTo("Hello", "World");
    }

    [Fact]
    public void FilterNullFiltersEmptyRefTypes()
    {
        IEnumerable<string?> source = Array.Empty<string?>();
        IEnumerable<string> result = source.FilterNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterNullFiltersValueTypes()
    {
        IEnumerable<int?> source = new int?[] { 0xdead, null, 0xcafe };
        IEnumerable<int> result = source.FilterNull();
        result.Should().BeEquivalentTo(new[] {0xdead, 0xcafe });
    }

    [Fact]
    public void FilterNullFiltersEmptyValueTypes()
    {
        IEnumerable<int?> source = Array.Empty<int?>();
        IEnumerable<int> result = source.FilterNull();
        result.Should().BeEmpty();
    }
}