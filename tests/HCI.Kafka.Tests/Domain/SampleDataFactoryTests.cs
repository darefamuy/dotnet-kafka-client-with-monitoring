using FluentAssertions;
using HCI.Kafka.Domain.Factories;
using HCI.Kafka.Domain.Models;
using Xunit;

namespace HCI.Kafka.Tests.Domain;

/// <summary>
/// Tests for the SampleDataFactory.
/// These validate that generated test data meets the structural constraints
/// expected by Confluent Schema Registry data contract rules.
/// </summary>
public sealed class SampleDataFactoryTests
{
    // ── MedicinalProduct generation ───────────────────────────────────────────

    [Fact]
    public void RandomProduct_ShouldGenerateValidGtin()
    {
        var product = SampleDataFactory.RandomProduct();

        product.Gtin.Should().NotBeNullOrEmpty();
        product.Gtin.Should().HaveLength(14, "GTIN must be exactly 14 digits");
        product.Gtin.Should().MatchRegex(@"^\d{14}$", "GTIN must contain only digits");
    }

    [Fact]
    public void RandomProduct_ShouldHavePositivePrices()
    {
        var product = SampleDataFactory.RandomProduct();

        product.ExFactoryPrice.Should().BePositive("ex-factory price must be > 0");
        product.PublicPrice.Should().BePositive("public price must be > 0");
        product.PublicPrice.Should().BeGreaterThan(product.ExFactoryPrice,
            "public price includes distribution margin and should exceed ex-factory price");
    }

    [Fact]
    public void RandomProduct_ShouldHaveValidAtcCode()
    {
        var product = SampleDataFactory.RandomProduct();

        product.AtcCode.Should().NotBeNullOrEmpty();
        product.AtcCode.Should().MatchRegex(@"^[A-Z]\d{2}[A-Z]{2}\d{2}$",
            "ATC code must follow WHO format (e.g. N02BE01)");
    }

    [Fact]
    public void RandomProduct_ShouldHaveRequiredFields()
    {
        var product = SampleDataFactory.RandomProduct();

        product.Gtin.Should().NotBeNullOrEmpty();
        product.ArticleNumber.Should().NotBeNullOrEmpty();
        product.AuthorizationNumber.Should().NotBeNullOrEmpty();
        product.ProductName.Should().NotBeNullOrEmpty();
        product.ActiveSubstance.Should().NotBeNullOrEmpty();
        product.AtcCode.Should().NotBeNullOrEmpty();
        product.DosageForm.Should().NotBeNullOrEmpty();
        product.Strength.Should().NotBeNullOrEmpty();
        product.Manufacturer.Should().NotBeNullOrEmpty();
        product.DataSource.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RandomProduct_ShouldHaveRecentLastUpdatedTimestamp()
    {
        var product = SampleDataFactory.RandomProduct();
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);

        product.LastUpdatedUtc.Should().BeAfter(oneHourAgo,
            "LastUpdatedUtc should be within the last hour for freshness");
        product.LastUpdatedUtc.Should().BeBefore(DateTime.UtcNow.AddSeconds(1),
            "LastUpdatedUtc should not be in the future");
    }

    [Fact]
    public void GenerateBatch_ShouldReturnCorrectCount()
    {
        const int batchSize = 100;
        var batch = SampleDataFactory.GenerateBatch(batchSize).ToList();

        batch.Should().HaveCount(batchSize);
    }

    [Fact]
    public void GenerateBatch_ShouldProduceUniqueArticleNumbers()
    {
        var batch = SampleDataFactory.GenerateBatch(500).ToList();
        var uniqueArticleNumbers = batch.Select(p => p.ArticleNumber).Distinct().Count();

        // Article numbers should be highly diverse (allow some collisions with 500 items)
        uniqueArticleNumbers.Should().BeGreaterThan(400,
            "article numbers should be sufficiently diverse");
    }

    [Fact]
    public void GenerateBatch_ShouldHaveMixOfMarketingStatuses()
    {
        var batch = SampleDataFactory.GenerateBatch(500).ToList();

        batch.Should().Contain(p => p.MarketingStatus == MarketingStatus.Active);
        batch.Should().Contain(p => p.MarketingStatus == MarketingStatus.Suspended);
        batch.Should().Contain(p => p.MarketingStatus == MarketingStatus.Withdrawn);
    }

    // ── Invalid product generation (for DLQ testing) ─────────────────────────

    [Theory]
    [InlineData(InvalidDataType.InvalidGtin)]
    [InlineData(InvalidDataType.NegativePrice)]
    [InlineData(InvalidDataType.MissingActiveSubstance)]
    [InlineData(InvalidDataType.InvalidAtcCode)]
    [InlineData(InvalidDataType.FutureLastUpdated)]
    public void InvalidProduct_ShouldContainExpectedViolation(InvalidDataType errorType)
    {
        var product = SampleDataFactory.InvalidProduct(errorType);

        switch (errorType)
        {
            case InvalidDataType.InvalidGtin:
                product.Gtin.Length.Should().NotBe(14, "invalid GTIN should not be 14 chars");
                break;
            case InvalidDataType.NegativePrice:
                product.ExFactoryPrice.Should().BeNegative("invalid price should be negative");
                break;
            case InvalidDataType.MissingActiveSubstance:
                product.ActiveSubstance.Should().BeEmpty("missing active substance should be empty");
                break;
            case InvalidDataType.InvalidAtcCode:
                product.AtcCode.Should().Be("INVALID");
                break;
            case InvalidDataType.FutureLastUpdated:
                product.LastUpdatedUtc.Should().BeAfter(DateTime.UtcNow.AddYears(5));
                break;
        }
    }

    // ── DrugAlert generation ──────────────────────────────────────────────────

    [Fact]
    public void RandomAlert_ShouldGenerateValidAlert()
    {
        var alert = SampleDataFactory.RandomAlert();

        alert.AlertId.Should().NotBeNullOrEmpty();
        alert.AlertId.Should().MatchRegex(
            @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            "AlertId should be a valid UUID");
        alert.Gtin.Should().HaveLength(14);
        alert.Headline.Should().NotBeNullOrEmpty();
        alert.Description.Should().NotBeNullOrEmpty();
        alert.SourceUrl.Should().StartWith("https://www.swissmedic.ch/");
    }

    [Fact]
    public void RandomAlert_WithSpecificGtin_ShouldUseProvidedGtin()
    {
        const string targetGtin = "76123456789012";
        var alert = SampleDataFactory.RandomAlert(gtin: targetGtin);

        alert.Gtin.Should().Be(targetGtin);
    }
}
