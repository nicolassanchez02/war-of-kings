using System.Collections.Generic;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;

namespace WarOfKings.Simulation;

/// <summary>
/// The root of the simulation state. Owns all entities, all players, the tick counter,
/// the RNG, and runs the tick loop.
///
/// This class has no Godot dependency and runs headlessly. It is the entire game world,
/// expressed as plain C# state.
/// </summary>
public sealed class World
{
    public long CurrentTick { get; private set; }
    public DeterministicRng Rng { get; }

    // Entity storage. Sorted by ID for deterministic iteration.
    private readonly SortedDictionary<EntityId, object> _entities = new();
    private long _nextEntityId = 1;

    public World(ulong seed)
    {
        Rng = new DeterministicRng(seed);
    }

    /// <summary>
    /// Allocate the next entity ID. The single source of truth for entity creation order.
    /// </summary>
    public EntityId AllocateEntityId() => new(_nextEntityId++);

    /// <summary>
    /// Register an entity with the world. Entity must already have its ID set
    /// (from AllocateEntityId). Throws if the ID is already in use.
    /// </summary>
    public void RegisterEntity(EntityId id, object entity)
    {
        if (id.IsNone) throw new System.ArgumentException("Cannot register EntityId.None");
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
    /// Advance the simulation by one tick. Applies commands for this tick, then runs all systems.
    /// </summary>
    public void Step(IReadOnlyList<Command> commandsForThisTick)
    {
        // 1. Apply commands. Sorted by player then by command sequence for determinism.
        //    Implementation belongs in a CommandProcessor; left as a stub for now.
        //    TODO(, M1): implement command application with deterministic ordering.

        // 2. Run systems in fixed order. Order matters and must not change.
        //    TODO(, M1 onward): add systems as they're implemented.
        //    Expected order: Input -> Behavior transitions -> Pathing -> Movement
        //                    -> Combat -> Gathering -> Production -> Vision -> Death/Cleanup.

        CurrentTick++;
    }

    /// <summary>
    /// Compute a 64-bit hash of the entire deterministic state.
    /// Used for OOS detection across clients and for the determinism test in CI.
    ///
    /// Every entity field that affects gameplay MUST contribute to this hash.
    /// When entities arrive in M1, they will implement IHashable and be folded in here.
    /// </summary>
    public ulong ComputeStateHash()
    {
        var h = new Fnv1a64();
        h.Mix(CurrentTick);
        h.Mix(_nextEntityId);
        Rng.HashInto(h);

        foreach (var kvp in _entities)
        {
            h.Mix(kvp.Key.Value);
            if (kvp.Value is IHashable hashable)
                hashable.HashInto(h);
        }

        return h.Result;
    }
}

