namespace WarOfKings.Simulation.Core;

/// <summary>
/// Implemented by entities and other state objects that contribute to the world state hash.
/// Add a HashInto call for every field that affects deterministic gameplay.
/// Forgetting a field will show up as a divergent hash on the next determinism CI run.
/// </summary>
public interface IHashable
{
    void HashInto(Fnv1a64 hash);
}
