using WarOfKings.Simulation.Core;

namespace WarOfKings.Simulation.Commands;

/// <summary>
/// Base type for all player intent.
///
/// Commands are the ONLY way to mutate world state from outside the simulation.
/// They are produced by the input layer (human input, AI, network) and consumed by World.Step.
///
/// Commands are tagged with the tick they should execute on (current tick + lockstep delay
/// for networked play). The World only applies commands whose ExecuteAtTick matches the
/// tick currently being processed.
/// </summary>
public abstract class Command
{
    /// <summary>The tick at which this command should be applied. Set by the input layer.</summary>
    public long ExecuteAtTick { get; init; }

    /// <summary>The player issuing this command.</summary>
    public PlayerId Player { get; init; }

    /// <summary>
    /// Monotonic per-player sequence number. Used as a tie-breaker for deterministic ordering
    /// when multiple commands from the same player target the same tick.
    /// </summary>
    public uint Sequence { get; init; }
}

/// <summary>Move the listed units to the given world position.</summary>
public sealed class MoveCommand : Command
{
    public required EntityId[] Units { get; init; }
    public required FixedVector2 Target { get; init; }
}

/// <summary>Order the given units to attack the target entity (move into range if needed).</summary>
public sealed class AttackCommand : Command
{
    public required EntityId[] Attackers { get; init; }
    public required EntityId Target { get; init; }
}

/// <summary>Order the given villager(s) to gather from the target resource node.</summary>
public sealed class GatherCommand : Command
{
    public required EntityId[] Gatherers { get; init; }
    public required EntityId ResourceNode { get; init; }
}

/// <summary>Order a villager to construct a building at the given location.</summary>
public sealed class BuildCommand : Command
{
    public required EntityId Builder { get; init; }
    public required int BuildingTypeId { get; init; }
    public required FixedVector2 Position { get; init; }
}

/// <summary>Order a production building to train a unit.</summary>
public sealed class TrainCommand : Command
{
    public required EntityId ProductionBuilding { get; init; }
    public required int UnitTypeId { get; init; }
}
