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

    // Per-player state. P1 and P2 always exist (created in the constructor) and are mixed
    // into the state hash in a fixed order so first-touch lazy creation can't perturb hashes.
    private readonly Player _neutral;
    private readonly Player _player1;
    private readonly Player _player2;

    // Tile occupancy. _occupancy[tileIdx] == EntityId.None when the tile is free. Stores the
    // EntityId of whatever entity (Unit, Tree, BerryBush, Building) currently occupies the
    // tile; A* and MovementSystem treat any non-None entry as a blocker.
    private readonly EntityId[] _occupancy;

    private readonly GatheringSystem _gathering = new();
    private readonly CombatSystem _combat = new();
    private readonly MovementSystem _movement = new();
    private readonly ProductionSystem _production = new();

    public World(ulong seed)
    {
        Rng = new DeterministicRng(seed);
        Map = Grid.Generate(seed);
        _occupancy = new EntityId[Grid.Width * Grid.Height];
        _neutral = new Player(PlayerId.Neutral);
        _player1 = new Player(PlayerId.Player1);
        _player2 = new Player(PlayerId.Player2);
    }

    // --- Players ---

    public Player GetPlayer(PlayerId id)
    {
        if (id == PlayerId.Player1) return _player1;
        if (id == PlayerId.Player2) return _player2;
        return _neutral;
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

    /// <summary>
    /// Remove an entity from the world. Clears all occupied tiles, decrements the owner's
    /// PopCurrent (for Units), and drops the entity from active iteration. The EntityId is
    /// burned — never re-used. Idempotent: removing an already-removed entity is a no-op.
    /// </summary>
    public void RemoveEntity(EntityId id)
    {
        if (!_entities.TryGetValue(id, out var obj)) return;
        switch (obj)
        {
            case Unit u:
                ClearOccupant(u.CurrentTileIdx, u.Id);
                GetPlayer(u.Owner).PopCurrent = System.Math.Max(0, GetPlayer(u.Owner).PopCurrent - 1);
                break;
            case Tree t:
                ClearOccupant(t.TileY * Grid.Width + t.TileX, t.Id);
                break;
            case BerryBush bb:
                ClearOccupant(bb.TileY * Grid.Width + bb.TileX, bb.Id);
                break;
            case Building b:
                for (int dy = 0; dy < b.FootprintH; dy++)
                    for (int dx = 0; dx < b.FootprintW; dx++)
                        ClearOccupant((b.TileY + dy) * Grid.Width + (b.TileX + dx), b.Id);
                break;
        }
        _entities.Remove(id);
    }

    public IEnumerable<KeyValuePair<EntityId, object>> EntitiesOrderedById() => _entities;

    /// <summary>
    /// The single factory method for creating units. Allocates an EntityId, constructs
    /// the Unit, registers it, and stamps initial tile occupancy.
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
        GetPlayer(owner).PopCurrent += 1;
        return unit;
    }

    /// <summary>
    /// Spawn a tree on the given tile. The tile becomes occupied (impassable for pathing) for
    /// as long as the tree has wood remaining.
    /// </summary>
    public Tree CreateTree(int tileX, int tileY, int woodMax = 100)
    {
        var id = AllocateEntityId();
        var tree = new Tree(id, tileX, tileY, Fixed64.FromInt(woodMax));
        RegisterEntity(id, tree);
        SetOccupant(tileY * Grid.Width + tileX, id);
        return tree;
    }

    /// <summary>Spawn a berry bush on the given tile.</summary>
    public BerryBush CreateBerryBush(int tileX, int tileY, int foodMax = 125)
    {
        var id = AllocateEntityId();
        var bush = new BerryBush(id, tileX, tileY, Fixed64.FromInt(foodMax));
        RegisterEntity(id, bush);
        SetOccupant(tileY * Grid.Width + tileX, id);
        return bush;
    }

    /// <summary>
    /// Spawn a building with a rectangular footprint. All footprint tiles store the building's
    /// EntityId in the occupancy table; pathing will treat the area as a solid wall.
    /// </summary>
    public Building CreateBuilding(BuildingTypeId type, PlayerId owner,
                                   int tileX, int tileY,
                                   int footprintW, int footprintH,
                                   int hpMax)
    {
        var id = AllocateEntityId();
        var b = new Building(id, type, owner, tileX, tileY, footprintW, footprintH, Fixed64.FromInt(hpMax));
        RegisterEntity(id, b);
        for (int dy = 0; dy < footprintH; dy++)
            for (int dx = 0; dx < footprintW; dx++)
                SetOccupant((tileY + dy) * Grid.Width + (tileX + dx), id);
        return b;
    }

    /// <summary>Enumerate every Unit currently in the world, in EntityId order.</summary>
    public IEnumerable<Unit> UnitsOrderedById()
    {
        foreach (var kvp in _entities)
            if (kvp.Value is Unit u) yield return u;
    }

    public IEnumerable<Tree> TreesOrderedById()
    {
        foreach (var kvp in _entities)
            if (kvp.Value is Tree t) yield return t;
    }

    public IEnumerable<BerryBush> BushesOrderedById()
    {
        foreach (var kvp in _entities)
            if (kvp.Value is BerryBush b) yield return b;
    }

    public IEnumerable<Building> BuildingsOrderedById()
    {
        foreach (var kvp in _entities)
            if (kvp.Value is Building b) yield return b;
    }

    // --- Tile occupancy ---

    public EntityId GetOccupant(int tileIdx) => _occupancy[tileIdx];

    public ReadOnlySpan<EntityId> OccupancyView => _occupancy;

    public void SetOccupant(int tileIdx, EntityId id) => _occupancy[tileIdx] = id;

    /// <summary>Clear occupancy at the given tile, but only if the current occupant matches.</summary>
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
        // Order: behavior decisions first (they may set new pending paths or clear paths),
        // then low-level movement, then production (which mints new entities). Reversing the
        // first two means MovementSystem consumes a path before BehaviorSystem decides to
        // abandon it; doing production before movement means newborn units would be visible
        // to MovementSystem on tick zero of their life, with no path or behavior — fine, but
        // we keep production last so newborns get a full tick of state before they move.
        _gathering.Tick(this);
        _combat.Tick(this);
        _movement.Tick(this);
        _production.Tick(this);
        CurrentTick++;
    }

    /// <summary>
    /// Compute a 64-bit hash of the entire deterministic state. Used for OOS detection
    /// across clients and for the determinism test in CI.
    /// </summary>
    public ulong ComputeStateHash()
    {
        var h = new Fnv1a64();
        h.Mix(CurrentTick);
        h.Mix(_nextEntityId);
        Rng.HashInto(h);
        Map.HashInto(h);

        // Players in fixed order (Neutral, P1, P2). Always present; never lazy-created.
        _neutral.HashInto(h);
        _player1.HashInto(h);
        _player2.HashInto(h);

        foreach (var kvp in _entities)
        {
            h.Mix(kvp.Key.Value);
            if (kvp.Value is IHashable hashable)
                hashable.HashInto(h);
        }

        return h.Result;
    }
}
