namespace HCI.Kafka.Domain.Models;

/// <summary>
/// A Swissmedic safety alert or recall notification.
/// These events must propagate to all downstream systems (Compendium.ch,
/// PharmaVista, hospital systems) within seconds for patient safety.
/// </summary>
public record DrugAlert
{
    /// <summary>Unique alert identifier (UUID)</summary>
    public required string AlertId { get; init; }

    /// <summary>GTIN of the affected product</summary>
    public required string Gtin { get; init; }

    /// <summary>Type of safety alert</summary>
    public required AlertType AlertType { get; init; }

    /// <summary>Patient safety severity classification</summary>
    public required AlertSeverity Severity { get; init; }

    /// <summary>Short headline summary of the alert</summary>
    public required string Headline { get; init; }

    /// <summary>Full description of the alert and required actions</summary>
    public required string Description { get; init; }

    /// <summary>Affected lot/batch numbers — empty if entire product affected</summary>
    public IReadOnlyList<string> AffectedLots { get; init; } = Array.Empty<string>();

    /// <summary>UTC timestamp when Swissmedic issued the alert</summary>
    public required DateTime IssuedByUtc { get; init; }

    /// <summary>UTC expiry of this alert — null if indefinite</summary>
    public DateTime? ExpiresUtc { get; init; }

    /// <summary>Link to official Swissmedic publication</summary>
    public required string SourceUrl { get; init; }
}

public enum AlertType
{
    Recall,
    SafetyWarning,
    Shortage,
    LabelUpdate
}

public enum AlertSeverity
{
    /// <summary>Immediate hazard that will cause serious adverse health consequences or death</summary>
    ClassI,

    /// <summary>May cause temporary adverse health consequences — remote probability of serious harm</summary>
    ClassII,

    /// <summary>Unlikely to cause adverse health consequences</summary>
    ClassIII,

    /// <summary>Informational advisory — no immediate risk</summary>
    Advisory
}
