using HRandomPlus.Core;
using HRandomPlus.Desktop;

namespace HRandomPlus.Tests;

public class GuideTests
{
    [Fact]
    public void GuideCoversEveryConfigurationFieldAndScoringWeight()
    {
        string[] keys = GuideContent.Sections.SelectMany(section => section.Entries).Select(entry => entry.Key).ToArray();
        foreach (var property in typeof(HRandomConfig).GetProperties().Where(property => property.Name != "Weights")
                     .Concat(typeof(ScoringWeights).GetProperties()))
            Assert.Equal(1, keys.Count(key => key == property.Name));
        Assert.Equal(1, keys.Count(key => key == "ReferenceBpm"));
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.True(GuideContent.Sections.SelectMany(section => section.Entries)
            .All(entry => !string.IsNullOrWhiteSpace(entry.Title) && !string.IsNullOrWhiteSpace(entry.Description)));
    }

    [Fact]
    public void GuideProvidesTheFourUserSectionsAndExplainsWeightScale()
    {
        Assert.Equal(new[] { "Getting started", "Parameters", "Profiles", "Integration" },
            GuideContent.Sections.Select(section => section.Title));
        Assert.Contains("do not directly represent difficulty", GuideContent.Sections[1].Introduction);
        Assert.Contains("not percentages", GuideContent.Sections[1].Entries.Single(entry => entry.Key == "ScoringWeights").Description);
    }
}
