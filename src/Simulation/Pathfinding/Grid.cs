using System;
using WarOfKings.Simulation.Core;

namespace WarOfKings.Simulation.Pathfinding;

/// <summary>
/// One terrain tile. Movement and pathing implications are derived in <see cref="Grid"/>.
/// Encoded as a byte so the grid serializes/hashes compactly.
/// </summary>
public enum Terrain : byte
{
    Plain    = 0,   // Walkable, no resource
    Forest   = 1,   // Walkable, harvestable wood (M3+)
    Mountain = 2,   // Impassable
    Water    = 3,   // Impassable (no naval in v1)
    Gold     = 4,   // Walkable, harvestable gold (M3+)
}

/// <summary>
/// The game map: a fixed-size 2D grid of terrain tiles. Generated deterministically
/// from the world seed at construction. Mutable in principle (M3 villagers will chop
/// trees and the Forest tile becomes Plain) but immutable across M1.
///
/// Coordinates: (x, y) where x is column, y is row. (0, 0) is top-left.
/// </summary>
public sealed class Grid : IHashable
{
    public const int Width = 200;
    public const int Height = 200;

    private readonly byte[] _tiles;

    public Grid(byte[] tiles)
    {
        if (tiles.Length != Width * Height)
            throw new ArgumentException($"Grid expects {Width * Height} tiles, got {tiles.Length}", nameof(tiles));
        _tiles = tiles;
    }

    /// <summary>
    /// Deterministic procedural generation. Uses a dedicated RNG seeded from the master
    /// seed so map generation can't be perturbed by other subsystems' RNG draws.
    /// Light terrain mix for M1: mostly plain, sparse forest, occasional mountain/gold.
    /// </summary>
    public static Grid Generate(ulong seed)
    {
        var rng = new DeterministicRng(seed ^ 0xA11BA11BA11BA11BUL);
        var tiles = new byte[Width * Height];
        for (int i = 0; i < tiles.Length; i++)
        {
            var roll = (int)(rng.NextUInt() % 100);
            tiles[i] = roll switch
            {
                < 80 => (byte)Terrain.Plain,
                < 92 => (byte)Terrain.Forest,
                < 96 => (byte)Terrain.Mountain,
                < 98 => (byte)Terrain.Water,
                _    => (byte)Terrain.Gold,
            };
        }
        return new Grid(tiles);
    }

    public Terrain GetTerrain(int x, int y)
    {
        if ((uint)x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
        return (Terrain)_tiles[y * Width + x];
    }

    public void SetTerrain(int x, int y, Terrain terrain)
    {
        if ((uint)x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
        _tiles[y * Width + x] = (byte)terrain;
    }

    public bool IsPassable(int x, int y)
    {
        var t = GetTerrain(x, y);
        return t != Terrain.Mountain && t != Terrain.Water;
    }

    /// <summary>
    /// Mix every tile into the state hash in row-major order. Deterministic and exhaustive.
    /// At 40000 bytes per call, this is well under the per-tick budget for state hashing
    /// (FNV-1a is roughly two arithmetic ops per byte). If the grid becomes a hot spot,
    /// switch to incremental hashing: track a running grid hash, update on SetTerrain.
    /// </summary>
    public void HashInto(Fnv1a64 hash)
    {
        for (int i = 0; i < _tiles.Length; i++) hash.MixByte(_tiles[i]);
    }
}
