using System;

namespace WarOfKings.Simulation.Core;

/// <summary>
/// Strongly-typed entity identifier. Monotonically increasing, assigned by the factory.
/// Use this everywhere instead of raw integers or object references when storing or
/// passing around entity references in the simulation.
/// </summary>
public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
{
    public readonly long Value;
    public EntityId(long value) { Value = value; }

    public static readonly EntityId None = new(0);

    public bool IsNone => Value == 0;

    public bool Equals(EntityId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is EntityId id && Equals(id);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
    public override string ToString() => $"E{Value}";

    public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
    public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
    public static bool operator <(EntityId a, EntityId b) => a.Value < b.Value;
    public static bool operator >(EntityId a, EntityId b) => a.Value > b.Value;
}

/// <summary>
/// Player identifier. Up to 8 players supported, though v1 ships with 2.
/// </summary>
public readonly struct PlayerId : IEquatable<PlayerId>, IComparable<PlayerId>
{
    public readonly byte Value;
    public PlayerId(byte value) { Value = value; }

    public static readonly PlayerId Neutral = new(0);
    public static readonly PlayerId Player1 = new(1);
    public static readonly PlayerId Player2 = new(2);

    public bool Equals(PlayerId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is PlayerId p && Equals(p);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(PlayerId other) => Value.CompareTo(other.Value);
    public override string ToString() => $"P{Value}";

    public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
    public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
}
