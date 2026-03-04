using HCI.Kafka.Domain.Models;

namespace HCI.Kafka.Domain.Factories;

/// <summary>
/// Generates realistic Swiss pharmaceutical sample data for training and testing.
/// All product names, prices, and codes are fictional but structurally accurate.
/// </summary>
public static class SampleDataFactory
{
    private static readonly Random Rng = new(42); // Seeded for reproducibility

    private static readonly string[] Manufacturers =
    {
        "Novartis Pharma AG", "Roche AG", "Pfizer AG", "Sandoz AG",
        "Mepha Pharma AG", "Bayer (Schweiz) AG", "AstraZeneca AG",
        "Galderma AG", "Vifor Pharma AG", "Takeda Pharma AG",
        "Janssen-Cilag AG", "Sanofi-Aventis (Suisse) SA"
    };

    private static readonly string[] ActiveSubstances =
    {
        "Paracetamol", "Ibuprofen", "Amoxicillin", "Atorvastatin",
        "Metformin", "Ramipril", "Omeprazol", "Salbutamol",
        "Sertralin", "Amlodipin", "Metoprolol", "Losartan",
        "Simvastatin", "Pantoprazol", "Escitalopram"
    };

    // WHO ATC codes matching active substances above
    private static readonly string[] AtcCodes =
    {
        "N02BE01", "M01AE01", "J01CA04", "C10AA05",
        "A10BA02", "C09AA05", "A02BC01", "R03AC02",
        "N06AB06", "C08CA01", "C07AB02", "C09CA01",
        "C10AA01", "A02BC02", "N06AB10"
    };

    private static readonly string[] DosageForms =
    {
        "Filmtablette", "Kapsel, hart", "Lösung zur Injektion",
        "Sirup", "Augentropfen", "Pflaster", "Zäpfchen",
        "Pulver zur Herstellung einer Injektionslösung",
        "Retardtablette", "Granulat"
    };

    private static readonly string[] DataSources =
    {
        "Swissmedic", "Manufacturer", "SL", "HCI_Internal"
    };

    private static readonly string[] PackageSizes =
    {
        "10 Tabletten", "20 Tabletten", "30 Tabletten", "50 Tabletten",
        "100 Tabletten", "1 Ampulle 5 ml", "5 Ampullen 10 ml",
        "1 Flasche 200 ml", "28 Kapseln", "98 Kapseln"
    };

    /// <summary>
    /// Generates a single random MedicinalProduct with realistic Swiss pharma data.
    /// </summary>
    public static MedicinalProduct RandomProduct()
    {
        var substanceIdx = Rng.Next(ActiveSubstances.Length);
        var strengthValue = Rng.Next(1, 1000);
        var strengthUnit = Rng.Next(3) switch { 0 => "mg", 1 => "mcg", _ => "g" };
        var exFactoryPrice = Math.Round((decimal)(Rng.NextDouble() * 200 + 5), 2);

        return new MedicinalProduct
        {
            Gtin = GenerateGtin(),
            ArticleNumber = $"HCI-{Rng.Next(100000, 999999)}",
            AuthorizationNumber = Rng.Next(10000, 99999).ToString(),
            ProductName = $"{ActiveSubstances[substanceIdx]} {Rng.Next(1, 5) * 100} " +
                          $"{Manufacturers[Rng.Next(Manufacturers.Length)].Split(' ')[0]}",
            ActiveSubstance = ActiveSubstances[substanceIdx],
            AtcCode = AtcCodes[substanceIdx],
            DosageForm = DosageForms[Rng.Next(DosageForms.Length)],
            Strength = $"{strengthValue} {strengthUnit}",
            Manufacturer = Manufacturers[Rng.Next(Manufacturers.Length)],
            MarketingStatus = WeightedMarketingStatus(),
            ExFactoryPrice = exFactoryPrice,
            PublicPrice = Math.Round(exFactoryPrice * (decimal)(1.28 + Rng.NextDouble() * 0.15), 2),
            LastUpdatedUtc = DateTime.UtcNow.AddSeconds(-Rng.Next(0, 3600)),
            DataSource = DataSources[Rng.Next(DataSources.Length)],
            PackageSizeUnit = PackageSizes[Rng.Next(PackageSizes.Length)],
            NarcoticsCategory = Rng.Next(20) == 0 ? "A" : null // ~5% are narcotics
        };
    }

    /// <summary>
    /// Generates a batch of MedicinalProduct events.
    /// </summary>
    public static IEnumerable<MedicinalProduct> GenerateBatch(int count)
    {
        for (var i = 0; i < count; i++)
            yield return RandomProduct();
    }

    /// <summary>
    /// Generates a DrugAlert for a given GTIN with realistic Swissmedic content.
    /// </summary>
    public static DrugAlert RandomAlert(string? gtin = null)
    {
        var alertType = (AlertType)Rng.Next(4);
        var severity = (AlertSeverity)Rng.Next(4);

        return new DrugAlert
        {
            AlertId = Guid.NewGuid().ToString(),
            Gtin = gtin ?? GenerateGtin(),
            AlertType = alertType,
            Severity = severity,
            Headline = GenerateAlertHeadline(alertType, severity),
            Description = GenerateAlertDescription(alertType),
            AffectedLots = GenerateAffectedLots(),
            IssuedByUtc = DateTime.UtcNow,
            ExpiresUtc = alertType == AlertType.Shortage
                ? DateTime.UtcNow.AddDays(Rng.Next(30, 180))
                : null,
            SourceUrl = $"https://www.swissmedic.ch/swissmedic/de/home/humanarzneimittel/market-surveillance/alerts/{Guid.NewGuid():N}.html"
        };
    }

    /// <summary>
    /// Generates a product with invalid data to test DLQ/validation pipelines.
    /// </summary>
    public static MedicinalProduct InvalidProduct(InvalidDataType errorType)
    {
        var valid = RandomProduct();
        return errorType switch
        {
            InvalidDataType.InvalidGtin => valid with { Gtin = "123" }, // Too short
            InvalidDataType.NegativePrice => valid with { ExFactoryPrice = -10.50m, PublicPrice = -15.00m },
            InvalidDataType.MissingActiveSubstance => valid with { ActiveSubstance = string.Empty },
            InvalidDataType.InvalidAtcCode => valid with { AtcCode = "INVALID" },
            InvalidDataType.FutureLastUpdated => valid with { LastUpdatedUtc = DateTime.UtcNow.AddYears(10) },
            _ => valid
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string GenerateGtin()
    {
        // Swiss pharma GTINs typically start with 7612345 (Galenica prefix) or 4045181
        var prefixes = new[] { "7612345", "4045181", "7611851", "7601234" };
        var prefix = prefixes[Rng.Next(prefixes.Length)];
        var body = Rng.Next(1000000, 9999999).ToString();
        return $"{prefix}{body}".Substring(0, 14);
    }

    private static MarketingStatus WeightedMarketingStatus()
    {
        // Realistic distribution: ~85% active, ~8% suspended, ~5% withdrawn, ~2% pending
        return Rng.Next(100) switch
        {
            < 85 => MarketingStatus.Active,
            < 93 => MarketingStatus.Suspended,
            < 98 => MarketingStatus.Withdrawn,
            _ => MarketingStatus.Pending
        };
    }

    private static string GenerateAlertHeadline(AlertType type, AlertSeverity severity) =>
        (type, severity) switch
        {
            (AlertType.Recall, AlertSeverity.ClassI) => "Dringende Sicherheitswarnung: Sofortrückruf erforderlich",
            (AlertType.Recall, _) => "Chargenrückruf wegen Qualitätsmangel",
            (AlertType.SafetyWarning, AlertSeverity.ClassI) => "Wichtige Sicherheitsinformation: Risiko schwerwiegender Nebenwirkungen",
            (AlertType.SafetyWarning, _) => "Aktualisierte Sicherheitsinformationen in der Fachinformation",
            (AlertType.Shortage, _) => "Lieferengpass — eingeschränkte Verfügbarkeit",
            (AlertType.LabelUpdate, _) => "Aktualisierung der Patienteninformation und Fachinformation",
            _ => "Sicherheitsinformation Swissmedic"
        };

    private static string GenerateAlertDescription(AlertType type) =>
        type switch
        {
            AlertType.Recall => "Swissmedic hat einen Rückruf dieser Charge angeordnet. " +
                "Betroffene Packungen sind sofort aus dem Verkehr zu ziehen und an den Lieferanten zurückzusenden. " +
                "Patienten sind zu informieren und auf Alternativpräparate umzustellen.",
            AlertType.SafetyWarning => "Nach Auswertung von Spontanmeldungen wurden neue sicherheitsrelevante Erkenntnisse gewonnen. " +
                "Die Fachinformation wurde entsprechend aktualisiert. " +
                "Verschreibende Ärzte werden gebeten, die aktualisierten Kontraindikationen zu beachten.",
            AlertType.Shortage => "Aufgrund von Produktionsengpässen ist die Verfügbarkeit temporär eingeschränkt. " +
                "Apotheken und Spitäler werden gebeten, Lagerbestände zu rationieren. " +
                "Informationen zur Wiederverfügbarkeit folgen.",
            AlertType.LabelUpdate => "Die Patienten- und Fachinformation wurde gemäss aktualisierter klinischer Evidenz angepasst. " +
                "Neue Warnhinweise und Dosierungsempfehlungen wurden hinzugefügt.",
            _ => "Weitere Informationen folgen."
        };

    private static IReadOnlyList<string> GenerateAffectedLots()
    {
        if (Rng.Next(3) == 0) return Array.Empty<string>(); // Entire product affected
        return Enumerable
            .Range(0, Rng.Next(1, 5))
            .Select(_ => $"CH{Rng.Next(100000, 999999)}")
            .ToList();
    }
}

public enum InvalidDataType
{
    InvalidGtin,
    NegativePrice,
    MissingActiveSubstance,
    InvalidAtcCode,
    FutureLastUpdated
}
