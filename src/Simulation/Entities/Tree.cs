using WarOfKings.Simulation.Core;

namespace WarOfKings.Simulation.Entities;

/// <summary>
/// A single tree. Occupies one tile, which is impassable while the tree has wood remaining.
/// When wood reaches zero the tree is "depleted" — the world clears its tile occupancy and
/// the tree is removed from active iteration but is NOT garbage-collected (its EntityId is
/// permanent and continues to exist in the entity table so any references stay valid). The
/// brief calls for trees to "deplete and disappear"; visually they shrink at 75/50/25 in the
/// renderer.
/// </summary>
public sealed class Tree : IHashable
{
    public EntityId Id { get; }

    /// <summary>Tile coordinates. Trees do not move.</summary>
    public int TileX { get; }
    public int TileY { get; }

    public Fixed64 WoodRemaining { get; set; }

    /// <summary>Wood at full stock. The standard tree starts with 100 wood.</summary>
    public Fixed64 WoodMax { get; }

    public bool IsDepleted => WoodRemaining.Raw <= 0;

    public Tree(EntityId id, int tileX, int tileY, Fixed64 woodMax)
    {
        Id = id;
        TileX = tileX;
        TileY = tileY;
        WoodMax = woodMax;
        WoodRemaining = woodMax;
    }

    public void HashInto(Fnv1a64 hash)
    {
        hash.Mix((long)TileX);
        hash.Mix((long)TileY);
        hash.Mix(WoodRemaining.Raw);
        hash.Mix(WoodMax.Raw);
    }
}
