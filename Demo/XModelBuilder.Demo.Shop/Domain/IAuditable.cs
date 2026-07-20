namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>
/// A cross-cutting concern shared by many entities: a creation timestamp. Implemented across
/// unrelated aggregate roots (<see cref="Customer"/>, <see cref="Product"/>, <see cref="Category"/>),
/// it is exactly the kind of "true of EVERY object" default that belongs in XModelBuilder's
/// cross-cutting layer rather than in each individual builder - see
/// <c>Support/EntityDefaults.cs</c> and README chapter 5.
/// </summary>
public interface IAuditable
{
    /// <summary>When the entity was created (UTC). Stamped once, deterministically, by the cross-cutting layer.</summary>
    DateTime CreatedAt { get; set; }
}
