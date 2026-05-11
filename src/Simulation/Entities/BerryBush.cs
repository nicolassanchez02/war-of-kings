using WarOfKings.Simulation.Core;

namespace WarOfKings.Simulation.Entities;

/// <summary>
/// A berry bush. Occupies one tile, impassable while it has food remaining. Bushes are
/// typically placed in patches of 5-6; the single-bush type is shared across all of them.
/// </summary>
public sealed class BerryBush : IHashable
{
    public EntityId Id { get; }

    public int TileX { get; }
    public int TileY { get; }

    public Fixed64 FoodRemaining { get; set; }
    public Fixed64 FoodMax { get; }

    public bool IsDepleted => FoodRemaining.Raw <= 0;

    public BerryBush(EntityId id, int tileX, int tileY, Fixed64 foodMax)
    {
        Id = id;
        TileX = tileX;
        TileY = tileY;
        FoodMax = foodMax;
        FoodRemaining = foodMax;
    }

    public void HashInto(Fnv1a64 hash)
    {
        hash.Mix((long)TileX);
        hash.Mix((long)TileY);
        hash.Mix(FoodRemaining.Raw);
        hash.Mix(FoodMax.Raw);
    }
}
