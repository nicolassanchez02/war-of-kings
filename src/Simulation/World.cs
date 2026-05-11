using System;
using System.Collections.Generic;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;
using WarOfKings.Simulation.Systems;

namespace WarOfKings.Simulation;

/// <summary>
/// The root of the simulation state. Owns all entities, all players, the tick counter,
/// the RNG, the map, the tile-occupancy table, and runs the tick loop.
///
/// This class has no Godot dependency and runs headlessly. It is the entire game world,
/// expressed as plain C# state.
/// </summary>
public sealed class World
{
    public long CurrentTick { get; private set; }
    public DeterministicRng Rng { get; }
    public Grid Map { get; }

    // Entity storage. Sorted by ID for deterministic iteration.
    private readonly SortedDictionary<EntityId, object> _entities = new();
    private long _nextEntityId = 1;

    // Tile occupancy. _occupancy[tileIdx] == EntityId.None when the tile is free.
    // Mutated by MovementSystem on tile-boundary crossings; consulted by A* (transient
    // blocker callback) and by MovementSystem (next-tile collision check). The occupancy
    // table is NOT independently hashed: it is fully derived from each Unit's
    // CurrentTileIdx, which is mixed into the hash via Unit.HashInto.
    private readonly EntityId[] _occupancy;

    private readonly MovementSystem _movement = new();

    public World(ulong seed)
    {
        Rng = new DeterministicRng(seed);
        Map = Grid.Generate(seed);
        _occupancy = new EntityId[Grid.Width * Grid.Height];
        // Default EntityId is value-default (Value=0 -> IsNone == true), so no explicit init needed.
    }

    /// <summary>Allocate the next entity ID. The single source of truth for entity creation order.</summary>
    public EntityId AllocateEntityId() => new(_nextEntityId++);

    /// <summary>Register an entity with the world. Throws if the ID is already in use.</summary>
    public void RegisterEntity(EntityId id, object entity)
    {
        if (id.IsNone) throw new ArgumentException("Cannot register EntityId.None");
        _entities.Add(id, entity);
    }

    public bool TryGetEntity(EntityId id, out object? entity)
    {
        if (_entities.TryGetValue(id, out var e)) { entity = e; return true; }
        entity = null;
        return false;
    }

    public IEnumerable<KeyValuePair<EntityId, object>> EntitiesOrderedById() => _entities;

    /// <summary>
    /// The single factory method for creating units. Allocates an EntityId, constructs
    /// the Unit, registers it, and stamps initial tile occupancy. Callers must never
    /// `new Unit(...)` directly — that would bypass ID allocation and break deterministic
    /// iteration.
    /// </summary>
    public Unit CreateUnit(PlayerId owner, FixedVector2 position)
    {
        var id = AllocateEntityId();
        int tileIdx = TileIdxOf(position);
        var unit = new Unit(id)
        {
            Owner = owner,
            Position = position,
            Facing = Fixed64.Zero,
            HpCurrent = Fixed64.FromInt(40),
            HpMax = Fixed64.FromInt(40),
            CurrentTileIdx = tileIdx,
        };
        RegisterEntity(id, unit);
        SetOccupant(tileIdx, id);
        return unit;
    }

    /// <summary>Enumerate every Unit currently in the world, in EntityId order.</summary>
    public IEnumerable<Unit> UnitsOrderedById()
    {
        foreach (var kvp in _entities)
        {
            if (kvp.Value is Unit u) yield return u;
        }
    }

    // --- Tile occupancy ---

    public EntityId GetOccupant(int tileIdx) => _occupancy[tileIdx];

    /// <summary>Read-only view of the occupancy table for pathfinder consumption.</summary>
    public ReadOnlySpan<EntityId> OccupancyView => _occupancy;

    public void SetOccupant(int tileIdx, EntityId id) => _occupancy[tileIdx] = id;

    /// <summary>Clear occupancy at the given tile, but only if the current occupant matches.
    /// Defensive: prevents one unit from accidentally evicting another's occupancy entry.</summary>
    public void ClearOccupant(int tileIdx, EntityId id)
    {
        if (_occupancy[tileIdx] == id) _occupancy[tileIdx] = EntityId.None;
    }

    private static int TileIdxOf(FixedVector2 position)
    {
        int x = position.X.ToInt();
        int y = position.Y.ToInt();
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x >= Grid.Width) x = Grid.Width - 1;
        if (y >= Grid.Height) y = Grid.Height - 1;
        return y * Grid.Width + x;
    }

    /// <summary>
    /// Advance the simulation by one tick. Order:
    ///   1. Apply commands for this tick (deterministic order).
    ///   2. Run systems in fixed order (M1: movement only).
    ///   3. Increment CurrentTick.
    /// </summary>
    public void Step(IReadOnlyList<Command> commandsForThisTick)
    {
        CommandProcessor.Apply(this, commandsForThisTick);

        // System pipeline. Order must not change without auditing determinism implications.
        // M1: Movement (consumes pending paths, advances positions, manages occupancy).
        // M3+: Behavior transitions -> Pathing -> Movement -> Combat -> Gathering -> Production -> Vision -> Death/Cleanup.
        _movement.Tick(this);

        CurrentTick++;
    }

    /// <summary>
    /// Compute a 64-bit hash of the entire deterministic state. Used for OOS detection
    /// across clients and for the determinism test in CI. Every entity field that affects
    /// gameplay MUST contribute to this hash (via Unit.HashInto and friends).
    /// </summary>
    public ulong ComputeStateHash()
    {
        var h = new Fnv1a64();
        h.Mix(CurrentTick);
        h.Mix(_nextEntityId);
        Rng.HashInto(h);
        Map.HashInto(h);

        foreach (var kvp in _entities)
        {
            h.Mix(kvp.Key.Value);
            if (kvp.Value is IHashable hashable)
                hashable.HashInto(h);
        }

        return h.Result;
    }
}
