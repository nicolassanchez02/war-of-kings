using WarOfKings.Simulation.Core;

namespace WarOfKings.Simulation.Entities;

/// <summary>
/// Mobile entity. Minimal M0 shape: identity, owner, position, facing, hp.
/// Movement, behavior state machines, and unit-type data arrive in M1+.
/// </summary>
public sealed class Unit : IHashable
{
    public EntityId Id { get; }
    public PlayerId Owner { get; set; }
    public FixedVector2 Position { get; set; }
    public Fixed64 Facing { get; set; }
    public Fixed64 HpCurrent { get; set; }
    public Fixed64 HpMax { get; set; }

    public Unit(EntityId id)
    {
        Id = id;
    }

    public void HashInto(Fnv1a64 hash)
    {
        hash.Mix(Owner.Value);
        hash.Mix(Position.X.Raw);
        hash.Mix(Position.Y.Raw);
        hash.Mix(Facing.Raw);
        hash.Mix(HpCurrent.Raw);
        hash.Mix(HpMax.Raw);
    }
}
