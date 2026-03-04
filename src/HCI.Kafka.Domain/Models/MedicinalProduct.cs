namespace HCI.Kafka.Domain.Models;

/// <summary>
/// Represents a pharmaceutical product as registered in the HCI product master.
/// This models drug data collected from SwissMedic, manufacturer submissions,
/// and the Federal Office of Public Health (BAG) Spezialitätenliste (SL).
/// </summary>
public record MedicinalProduct
{
    /// <summary>14-digit GS1 Global Trade Item Number</summary>
    public required string Gtin { get; init; }

    /// <summary>HCI internal article number (e.g. HCI-123456)</summary>
    public required string ArticleNumber { get; init; }

    /// <summary>Swissmedic authorization number (e.g. 67890)</summary>
    public required string AuthorizationNumber { get; init; }

    /// <summary>Full commercial product name</summary>
    public required string ProductName { get; init; }

    /// <summary>International Nonproprietary Name (INN) of active substance</summary>
    public required string ActiveSubstance { get; init; }

    /// <summary>WHO Anatomical Therapeutic Chemical classification code (e.g. N02BE01)</summary>
    public required string AtcCode { get; init; }

    /// <summary>Pharmaceutical dose form (e.g. Filmtablette, Lösung zur Injektion)</summary>
    public required string DosageForm { get; init; }

    /// <summary>Strength of active substance per unit (e.g. 500 mg)</summary>
    public required string Strength { get; init; }

    /// <summary>Marketing authorization holder / manufacturer</summary>
    public required string Manufacturer { get; init; }

    /// <summary>Current marketing authorization status</summary>
    public required MarketingStatus MarketingStatus { get; init; }

    /// <summary>Ex-factory price in CHF (before VAT and distribution markup)</summary>
    public required decimal ExFactoryPrice { get; init; }

    /// <summary>Public price in CHF as listed in the BAG Spezialitätenliste</summary>
    public required decimal PublicPrice { get; init; }

    /// <summary>UTC timestamp of the last update to this record</summary>
    public required DateTime LastUpdatedUtc { get; init; }

    /// <summary>Originating data source for this event</summary>
    public required string DataSource { get; init; }

    /// <summary>Optional package size description (e.g. 30 Tabletten)</summary>
    public string? PackageSizeUnit { get; init; }

    /// <summary>Betäubungsmittelverzeichnis (BetmVV) category — null for non-narcotics</summary>
    public string? NarcoticsCategory { get; init; }
}
