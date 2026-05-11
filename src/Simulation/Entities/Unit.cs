using System.Collections.Generic;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Pathfinding;

namespace WarOfKings.Simulation.Entities;

/// <summary>Low-level movement status. Orthogonal to <see cref="BehaviorKind"/>.</summary>
public enum UnitState : byte
{
    Idle = 0,
    Moving = 1,
    Waiting = 2,    // next tile blocked; counting toward repath threshold
}

/// <summary>
/// High-level intent layered on top of the movement state. M3 introduces the gathering loop;
/// M4 extends with combat (pursuit + attack); later milestones add building and fleeing.
/// </summary>
public enum BehaviorKind : byte
{
    Generic = 0,            // no behavior layer — direct move commands only
    GoingToResource = 1,    // moving toward TargetEntityId (a Tree/BerryBush)
    Gathering = 2,          // adjacent to TargetEntityId, ticking gather progress
    GoingToDropOff = 3,     // carry full / resource depleted, heading to DropOffId
    Depositing = 4,         // at DropOffId, single-tick transfer to player stockpile
    Pursuing = 5,           // moving toward TargetEntityId (enemy unit/building)
    Attacking = 6,          // in range, ticking attack cooldown against TargetEntityId
}

/// <summary>What kind of carry a villager currently holds.</summary>
public enum CarryKind : byte
{
    None = 0,
    Wood = 1,
    Food = 2,
    Gold = 3,
}

/// <summary>
/// Mobile entity. M1 shape: identity, owner, position, facing, hp, plus movement state
/// (current path, pending destination, collision counters). Behavior systems and unit-type
/// data arrive in M3+.
///
/// Position is in tile-space: (1.0, 0.0) is one tile east of the origin. <see cref="CurrentTileIdx"/>
/// is the tile the unit's center is currently in (computed when the unit crosses tile boundaries),
/// and is the cell used in the world's occupancy table.
/// </summary>
public sealed class Unit : IHashable
{
    public EntityId Id { get; }
    public PlayerId Owner { get; set; }
    public FixedVector2 Position { get; set; }
    public Fixed64 Facing { get; set; }
    public Fixed64 HpCurrent { get; set; }
    public Fixed64 HpMax { get; set; }

    /// <summary>Per-tick movement speed, in tile units. Set per unit type; default = villager pace.</summary>
    public Fixed64 MoveSpeedPerTick { get; set; } = Fixed64.FromRaw(2949);  // ~0.045 tiles/tick at 20Hz ~ 0.9 tiles/sec

    /// <summary>Tile index (y * Width + x) that this unit currently occupies in the grid.</summary>
    public int CurrentTileIdx { get; set; }

    public UnitState State { get; set; } = UnitState.Idle;

    /// <summary>Destination tile index of the active move order, or -1 if none.</summary>
    public int DestinationTileIdx { get; set; } = -1;

    /// <summary>
    /// New destination requested via MoveCommand this tick but not yet pathed. The MovementSystem
    /// consumes this on its next pass, runs A*, and resets it to -1. Use -1 for "no request".
    /// </summary>
    public int PendingDestinationIdx { get; set; } = -1;

    /// <summary>Sequence of tile indices from start to destination. Path[PathIndex] is the next waypoint.</summary>
    public List<int> Path { get; } = new();
    public int PathIndex { get; set; }

    /// <summary>Ticks the unit has been blocked at the current waypoint. Threshold = 5 -> repath.</summary>
    public int WaitTicks { get; set; }

    /// <summary>Repath count within the current 100-tick window. Threshold = 3 -> Idle / give up.</summary>
    public int RepathsInWindow { get; set; }
    public long RepathWindowStartTick { get; set; }

    // --- Behavior layer (M3 gathering) ---

    public BehaviorKind Behavior { get; set; } = BehaviorKind.Generic;

    /// <summary>Resource/build-site/attack target. EntityId.None when no behavior target is set.</summary>
    public EntityId TargetEntityId { get; set; } = EntityId.None;

    /// <summary>Active drop-off building during the carry phase. EntityId.None otherwise.</summary>
    public EntityId DropOffId { get; set; } = EntityId.None;

    public CarryKind Carried { get; set; } = CarryKind.None;
    public Fixed64 CarryAmount { get; set; }
    public Fixed64 CarryCapacity { get; set; } = Fixed64.FromInt(10);

    /// <summary>Tick counter for the gathering tempo. 20 ticks (= 1s at 20Hz) yields +1 carry.</summary>
    public int GatherProgressTicks { get; set; }

    // --- Combat ---

    /// <summary>1 = villager (default), 2 = militia.</summary>
    public int UnitTypeId { get; set; } = 1;

    /// <summary>Ticks since last attack swing landed; resets on swing. Set to AttackCooldown - 1
    /// when AttackCommand fires so the first swing connects after a single-tick wind-up.</summary>
    public int AttackCooldownTicks { get; set; }

    public Unit(EntityId id)
    {
        Id = id;
    }

    public bool HasPath => Path.Count > 0 && PathIndex < Path.Count;

    public void ClearPath()
    {
        Path.Clear();
        PathIndex = 0;
    }

    public void HashInto(Fnv1a64 hash)
    {
        hash.Mix(Owner.Value);
        hash.Mix(Position.X.Raw);
        hash.Mix(Position.Y.Raw);
        hash.Mix(Facing.Raw);
        hash.Mix(HpCurrent.Raw);
        hash.Mix(HpMax.Raw);
        hash.Mix(MoveSpeedPerTick.Raw);
        hash.Mix((long)CurrentTileIdx);
        hash.MixByte((byte)State);
        hash.Mix((long)DestinationTileIdx);
        hash.Mix((long)PendingDestinationIdx);
        hash.Mix((long)PathIndex);
        hash.Mix((long)Path.Count);
        for (int i = 0; i < Path.Count; i++) hash.Mix((long)Path[i]);
        hash.Mix((long)WaitTicks);
        hash.Mix((long)RepathsInWindow);
        hash.Mix(RepathWindowStartTick);
        hash.MixByte((byte)Behavior);
        hash.Mix(TargetEntityId.Value);
        hash.Mix(DropOffId.Value);
        hash.MixByte((byte)Carried);
        hash.Mix(CarryAmount.Raw);
        hash.Mix(CarryCapacity.Raw);
        hash.Mix((long)GatherProgressTicks);
        hash.Mix((long)UnitTypeId);
        hash.Mix((long)AttackCooldownTicks);
    }
}
