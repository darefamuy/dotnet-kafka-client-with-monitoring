using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using HCI.Kafka.Domain.Factories;
using HCI.Kafka.Domain.Models;
using HCI.Kafka.Infrastructure.Serialization;
using Xunit;

namespace HCI.Kafka.Tests.Infrastructure;

/// <summary>
/// Tests for the JSON serializer/deserializer.
/// Validates round-trip fidelity for MedicinalProduct messages.
/// </summary>
public sealed class JsonSerializerTests
{
    private readonly JsonSerializer<MedicinalProduct> _sut = new();

    [Fact]
    public void Serialize_ThenDeserialize_ShouldReturnEquivalentProduct()
    {
        var original = SampleDataFactory.RandomProduct();
        var context = SerializationContext.Empty;

        var bytes = _sut.Serialize(original, context);
        var deserialized = _sut.Deserialize(bytes, isNull: false, context);

        deserialized.Should().NotBeNull();
        deserialized.Gtin.Should().Be(original.Gtin);
        deserialized.ProductName.Should().Be(original.ProductName);
        deserialized.ActiveSubstance.Should().Be(original.ActiveSubstance);
        deserialized.AtcCode.Should().Be(original.AtcCode);
        deserialized.ExFactoryPrice.Should().Be(original.ExFactoryPrice);
        deserialized.PublicPrice.Should().Be(original.PublicPrice);
        deserialized.MarketingStatus.Should().Be(original.MarketingStatus);
        deserialized.Manufacturer.Should().Be(original.Manufacturer);
    }

    [Fact]
    public void Serialize_NullProduct_ShouldReturnEmptyBytes()
    {
        var bytes = _sut.Serialize(null!, SerializationContext.Empty);
        bytes.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_EmptyBytes_ShouldThrow()
    {
        var action = () => _sut.Deserialize(
            ReadOnlySpan<byte>.Empty,
            isNull: false,
            SerializationContext.Empty);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Serialize_ShouldProduceValidUtf8Json()
    {
        var product = SampleDataFactory.RandomProduct();
        var bytes = _sut.Serialize(product, SerializationContext.Empty);

        var json = Encoding.UTF8.GetString(bytes);
        json.Should().StartWith("{");
        json.Should().EndWith("}");
        json.Should().Contain("gtin");
        json.Should().Contain("productName");
    }

    [Fact]
    public void Serialize_ShouldPreserveNullableFields()
    {
        var product = SampleDataFactory.RandomProduct() with
        {
            PackageSizeUnit = null,
            NarcoticsCategory = null
        };

        var bytes = _sut.Serialize(product, SerializationContext.Empty);
        var deserialized = _sut.Deserialize(bytes, isNull: false, SerializationContext.Empty);

        deserialized.PackageSizeUnit.Should().BeNull();
        deserialized.NarcoticsCategory.Should().BeNull();
    }

    [Fact]
    public void SerializeDeserialize_BatchOf100_ShouldAllRoundTrip()
    {
        var products = SampleDataFactory.GenerateBatch(100).ToList();
        var context = SerializationContext.Empty;

        foreach (var product in products)
        {
            var bytes = _sut.Serialize(product, context);
            var deserialized = _sut.Deserialize(bytes, isNull: false, context);
            deserialized.Gtin.Should().Be(product.Gtin);
            deserialized.AtcCode.Should().Be(product.AtcCode);
        }
    }
}
