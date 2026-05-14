using System.Collections.Generic;
using WarOfKings.Simulation.Core;

namespace WarOfKings.Simulation.Entities;

/// <summary>
/// What kind of building this is. Used by gameplay code that needs to special-case behaviour
/// (e.g., "drop-off accepts wood", "training building can train villagers"). The data side
/// will move to `assets/data/buildings.json` once M3 economy needs more than this minimal
/// roster.
/// </summary>
public enum BuildingTypeId : byte
{
    None = 0,
    TownHall = 1,
    House = 2,
    Barracks = 3,
    LumberCamp = 4,
    Mill = 5,
}

/// <summary>
/// Static buildings. Occupy a rectangular footprint of tiles, all impassable while the
/// building has HP. The world's occupancy table stores the building's EntityId across every
/// footprint tile; A* and MovementSystem block on that.
///
/// Construction state (HP ramping up while villagers build) and destruction (HP &lt;= 0) arrive
/// alongside M3 villagers and M4 combat. For now, buildings start fully constructed at HpMax.
/// </summary>
public sealed class Building : IHashable
{
    public EntityId Id { get; }
    public BuildingTypeId Type { get; }
    public PlayerId Owner { get; }

    /// <summary>Top-left tile of the footprint.</summary>
    public int TileX { get; }
    public int TileY { get; }
    public int FootprintW { get; }
    public int FootprintH { get; }

    public Fixed64 HpCurrent { get; set; }
    public Fixed64 HpMax { get; }

    public bool IsDestroyed => HpCurrent.Raw <= 0;

    /// <summary>
    /// Pending training queue: each entry is a UnitTypeId. The building trains entries in
    /// FIFO order. M3 supports a single concurrent training slot (the head of the queue) with
    /// up to 5 queued units behind it.
    /// </summary>
    public List<int> ProductionQueue { get; } = new();
    public int ProductionProgressTicks { get; set; }
    public const int MaxQueueDepth = 5;

    public Building(EntityId id, BuildingTypeId type, PlayerId owner,
                    int tileX, int tileY, int footprintW, int footprintH,
                    Fixed64 hpMax)
    {
        Id = id;
        Type = type;
        Owner = owner;
        TileX = tileX;
        TileY = tileY;
        FootprintW = footprintW;
        FootprintH = footprintH;
        HpMax = hpMax;
        HpCurrent = hpMax;
    }

    public void HashInto(Fnv1a64 hash)
    {
        hash.MixByte((byte)Type);
        hash.Mix(Owner.Value);
        hash.Mix((long)TileX);
        hash.Mix((long)TileY);
        hash.Mix((long)FootprintW);
        hash.Mix((long)FootprintH);
        hash.Mix(HpCurrent.Raw);
        hash.Mix(HpMax.Raw);
        hash.Mix((long)ProductionQueue.Count);
        for (int i = 0; i < ProductionQueue.Count; i++) hash.Mix((long)ProductionQueue[i]);
        hash.Mix((long)ProductionProgressTicks);
    }
}
