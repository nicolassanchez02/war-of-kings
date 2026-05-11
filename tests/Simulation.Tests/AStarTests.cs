using System.Collections.Generic;
using WarOfKings.Simulation.Pathfinding;
using Xunit;

namespace WarOfKings.Simulation.Tests;

public class AStarTests
{
    // --- Helpers ---

    private static Grid PlainGrid()
    {
        var tiles = new byte[Grid.Width * Grid.Height];
        // All plain by default (0); leave as-is.
        return new Grid(tiles);
    }

    private static int Idx(int x, int y) => y * Grid.Width + x;

    // --- Core correctness ---

    [Fact]
    public void Start_equals_goal_returns_single_tile_path()
    {
        var grid = PlainGrid();
        var astar = new AStar();
        var path = new List<int>();

        Assert.True(astar.FindPath(grid, Idx(10, 10), Idx(10, 10), path));
        Assert.Single(path);
        Assert.Equal(Idx(10, 10), path[0]);
    }

    [Fact]
    public void Straight_line_path_on_empty_grid()
    {
        var grid = PlainGrid();
        var astar = new AStar();
        var path = new List<int>();

        Assert.True(astar.FindPath(grid, Idx(5, 5), Idx(5, 10), path));
        // Path is start-inclusive, goal-inclusive, length 6 (5..10 inclusive).
        Assert.Equal(6, path.Count);
        Assert.Equal(Idx(5, 5), path[0]);
        Assert.Equal(Idx(5, 10), path[^1]);
        // Y monotonically increases by 1.
        for (int i = 0; i < path.Count; i++)
            Assert.Equal(Idx(5, 5 + i), path[i]);
    }

    [Fact]
    public void Diagonal_path_uses_diagonal_steps()
    {
        var grid = PlainGrid();
        var astar = new AStar();
        var path = new List<int>();

        // From (0, 0) to (5, 5): pure diagonal is 5 diagonal steps -> path length 6.
        Assert.True(astar.FindPath(grid, Idx(0, 0), Idx(5, 5), path));
        Assert.Equal(6, path.Count);
    }

    [Fact]
    public void Blocked_goal_returns_false()
    {
        var tiles = new byte[Grid.Width * Grid.Height];
        var grid = new Grid(tiles);
        grid.SetTerrain(10, 10, Terrain.Mountain);

        var astar = new AStar();
        var path = new List<int>();

        Assert.False(astar.FindPath(grid, Idx(0, 0), Idx(10, 10), path));
        Assert.Empty(path);
    }

    [Fact]
    public void Wall_forces_detour()
    {
        var tiles = new byte[Grid.Width * Grid.Height];
        var grid = new Grid(tiles);
        // Vertical wall at x=5, y=4..6 inclusive. Path from (3, 5) to (7, 5) must route around.
        grid.SetTerrain(5, 4, Terrain.Mountain);
        grid.SetTerrain(5, 5, Terrain.Mountain);
        grid.SetTerrain(5, 6, Terrain.Mountain);

        var astar = new AStar();
        var path = new List<int>();

        Assert.True(astar.FindPath(grid, Idx(3, 5), Idx(7, 5), path));
        // Path must not contain any of the wall tiles.
        Assert.DoesNotContain(Idx(5, 4), path);
        Assert.DoesNotContain(Idx(5, 5), path);
        Assert.DoesNotContain(Idx(5, 6), path);
    }

    [Fact]
    public void No_corner_cutting_through_diagonal_block()
    {
        // Wall at (1, 0) makes the diagonal step (0, 0) -> (1, 1) illegal (corner-cutting).
        // Path must route through (0, 1) instead, yielding length 3.
        var tiles = new byte[Grid.Width * Grid.Height];
        var grid = new Grid(tiles);
        grid.SetTerrain(1, 0, Terrain.Mountain);

        var astar = new AStar();
        var path = new List<int>();
        Assert.True(astar.FindPath(grid, Idx(0, 0), Idx(1, 1), path));
        // Cannot be a 2-tile diagonal hop. Must route through (0, 1).
        Assert.Equal(3, path.Count);
        Assert.Equal(Idx(0, 0), path[0]);
        Assert.Equal(Idx(0, 1), path[1]);
        Assert.Equal(Idx(1, 1), path[2]);
    }

    // --- Determinism / tie-breaker ---

    [Fact]
    public void Same_query_twice_yields_identical_path()
    {
        var grid = PlainGrid();
        var astar = new AStar();
        var a = new List<int>();
        var b = new List<int>();

        Assert.True(astar.FindPath(grid, Idx(3, 7), Idx(17, 4), a));
        Assert.True(astar.FindPath(grid, Idx(3, 7), Idx(17, 4), b));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Two_different_pathfinder_instances_yield_identical_paths()
    {
        // The tie-breaker contract (f, h, nodeIdx) must produce identical output regardless of
        // pathfinder identity / instance state.
        var grid = PlainGrid();
        var a1 = new AStar();
        var a2 = new AStar();
        var pA = new List<int>();
        var pB = new List<int>();

        Assert.True(a1.FindPath(grid, Idx(0, 0), Idx(50, 50), pA));
        Assert.True(a2.FindPath(grid, Idx(0, 0), Idx(50, 50), pB));
        Assert.Equal(pA, pB);
    }

    [Fact]
    public void Reusing_same_instance_doesnt_leak_state_across_queries()
    {
        var grid = PlainGrid();
        var astar = new AStar();

        var path1 = new List<int>();
        var path2 = new List<int>();
        var path3 = new List<int>();

        Assert.True(astar.FindPath(grid, Idx(0, 0), Idx(20, 20), path1));
        Assert.True(astar.FindPath(grid, Idx(30, 30), Idx(40, 40), path2));
        Assert.True(astar.FindPath(grid, Idx(0, 0), Idx(20, 20), path3));

        // Repeating the first query after running an unrelated one must yield the same path.
        Assert.Equal(path1, path3);
    }
}
