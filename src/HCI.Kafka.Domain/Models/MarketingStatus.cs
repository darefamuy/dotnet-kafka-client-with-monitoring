namespace HCI.Kafka.Domain.Models;

/// <summary>
/// Marketing authorization status for a pharmaceutical product.
/// Follows Swissmedic status codes.
/// </summary>
public enum MarketingStatus
{
    /// <summary>Product is actively marketed and available</summary>
    Active,

    /// <summary>Marketing temporarily suspended (e.g. shortage, investigation)</summary>
    Suspended,

    /// <summary>Marketing authorization permanently withdrawn</summary>
    Withdrawn,

    /// <summary>Authorization pending — not yet on market</summary>
    Pending
}
