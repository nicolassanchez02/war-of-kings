using WarOfKings.Simulation.Core;

namespace WarOfKings.Simulation.Entities;

/// <summary>
/// Per-player simulation state: resource stockpiles, population, current age (placeholder).
/// One <see cref="Player"/> exists per <see cref="PlayerId"/> and is owned by the World.
///
/// All fields are mixed into the world state hash in HashInto. New gameplay-affecting
/// fields MUST be added there or determinism replays will silently drift.
/// </summary>
public sealed class Player : IHashable
{
    public PlayerId Id { get; }

    public Fixed64 Wood { get; set; }
    public Fixed64 Food { get; set; }
    public Fixed64 Gold { get; set; }

    public int PopCurrent { get; set; }
    public int PopCap { get; set; }

    /// <summary>Single-age design: every player is in Settlement Age for v1. Reserved for future use.</summary>
    public byte AgeCode { get; set; } = 0;

    public Player(PlayerId id)
    {
        Id = id;
    }

    public void HashInto(Fnv1a64 hash)
    {
        hash.Mix(Id.Value);
        hash.Mix(Wood.Raw);
        hash.Mix(Food.Raw);
        hash.Mix(Gold.Raw);
        hash.Mix((long)PopCurrent);
        hash.Mix((long)PopCap);
        hash.MixByte(AgeCode);
    }
}
