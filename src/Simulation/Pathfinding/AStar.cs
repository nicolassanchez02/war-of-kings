using System;
using System.Collections.Generic;

namespace WarOfKings.Simulation.Pathfinding;

/// <summary>
/// Deterministic A* pathfinder on the fixed-size Grid.
///
/// Tie-breaker contract (pinned in session note 2026-05-10-02, do not change):
///   When two open-set nodes have equal f-score, prefer the lower h-score; if still
///   tied, prefer the lower node ID (computed as <c>y * Grid.Width + x</c>). The heap
///   never breaks ties by insertion order or memory address — both vary across runs
///   and would silently desync replays.
///
/// Costs: 10 orthogonal, 14 diagonal (a 4-cost integer approximation of sqrt(2) ~ 1.414).
/// Heuristic: octile, <c>max(dx, dy) * 10 + min(dx, dy) * 4</c>. Admissible: each tile
/// step costs at least 10 cardinally; diagonal steps offer at most 4 worth of "extra"
/// savings beyond the cardinal cost, never more.
///
/// No corner-cutting: a diagonal step from (x, y) to (x+dx, y+dy) requires both
/// (x+dx, y) and (x, y+dy) to be passable. This avoids units gliding through wall
/// corners that should block them.
///
/// Allocation: per-call <c>Array.Clear</c> over four parallel <c>int</c>/<c>bool</c>
/// arrays of size <c>Width * Height</c> (~40 KB total at 200x200). Cheap enough that
/// per-tick pathing for hundreds of units is comfortably within budget; if it shows up
/// in profiles, switch to a touched-cells list and reset only those slots.
/// </summary>
public sealed class AStar
{
    private const int CostOrthogonal = 10;
    private const int CostDiagonal = 14;
    private const int Unvisited = int.MaxValue;

    private readonly int[] _gScore;
    private readonly int[] _cameFrom;
    private readonly bool[] _closed;
    private readonly BinaryHeap _open;

    // Optional path-smoothing pass. Off by default per the brief: enabling it changes
    // hash outcomes, so it must stay off until validated against the determinism test.
    public bool EnableSmoothing { get; set; }

    public AStar()
    {
        int n = Grid.Width * Grid.Height;
        _gScore = new int[n];
        _cameFrom = new int[n];
        _closed = new bool[n];
        _open = new BinaryHeap(capacity: 512);
    }

    /// <summary>
    /// Find a path from <paramref name="startIdx"/> to <paramref name="goalIdx"/>.
    /// Returns true if a path was found; the path (start-inclusive, goal-inclusive)
    /// is written into <paramref name="path"/> and ordered start -> goal.
    /// Returns false if start or goal is impassable, or no path exists; <paramref name="path"/>
    /// is then empty.
    ///
    /// <paramref name="isBlocked"/> is consulted in addition to <see cref="Grid.IsPassable"/>
    /// to support transient blockers (other units, scaffolding). Static map terrain is
    /// checked via the grid; transient blockers via the callback. The start tile is
    /// always traversable regardless of <paramref name="isBlocked"/>; that lets units
    /// path out of a tile they currently occupy.
    /// </summary>
    public bool FindPath(Grid grid, int startIdx, int goalIdx, List<int> path, Func<int, bool>? isBlocked = null)
    {
        path.Clear();

        if (startIdx == goalIdx)
        {
            path.Add(startIdx);
            return true;
        }

        int sx = startIdx % Grid.Width;
        int sy = startIdx / Grid.Width;
        int gx = goalIdx % Grid.Width;
        int gy = goalIdx / Grid.Width;

        // Goal must be passable terrain and not blocked. Start is always allowed.
        if (!grid.IsPassable(gx, gy)) return false;
        if (isBlocked != null && isBlocked(goalIdx)) return false;

        Array.Fill(_gScore, Unvisited);
        Array.Clear(_cameFrom, 0, _cameFrom.Length);
        Array.Clear(_closed, 0, _closed.Length);
        _open.Clear();

        _gScore[startIdx] = 0;
        int h0 = OctileHeuristic(sx, sy, gx, gy);
        _open.Push(new HeapNode(startIdx, h0, h0));

        while (_open.Count > 0)
        {
            var current = _open.Pop();
            int curIdx = current.NodeIdx;

            if (_closed[curIdx]) continue; // stale entry
            _closed[curIdx] = true;

            if (curIdx == goalIdx)
            {
                ReconstructPath(startIdx, goalIdx, path);
                if (EnableSmoothing) SmoothPath(grid, path, isBlocked);
                return true;
            }

            int cx = curIdx % Grid.Width;
            int cy = curIdx / Grid.Width;

            // 8-neighbor exploration in a deterministic, fixed order. The order doesn't
            // affect correctness (A* is order-independent for correctness) but locking
            // it removes one source of churn when reading hash diffs during debugging.
            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = cy + dy;
                if ((uint)ny >= Grid.Height) continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx;
                    if ((uint)nx >= Grid.Width) continue;

                    int nIdx = ny * Grid.Width + nx;
                    if (_closed[nIdx]) continue;

                    if (!grid.IsPassable(nx, ny)) continue;
                    if (isBlocked != null && nIdx != startIdx && isBlocked(nIdx)) continue;

                    bool diag = (dx != 0 && dy != 0);
                    if (diag)
                    {
                        // No corner-cutting.
                        if (!grid.IsPassable(cx + dx, cy)) continue;
                        if (!grid.IsPassable(cx, cy + dy)) continue;
                    }

                    int stepCost = diag ? CostDiagonal : CostOrthogonal;
                    int tentativeG = _gScore[curIdx] + stepCost;
                    if (tentativeG >= _gScore[nIdx]) continue;

                    _gScore[nIdx] = tentativeG;
                    _cameFrom[nIdx] = curIdx;

                    int hN = OctileHeuristic(nx, ny, gx, gy);
                    int fN = tentativeG + hN;
                    _open.Push(new HeapNode(nIdx, fN, hN));
                }
            }
        }

        return false;
    }

    private void ReconstructPath(int startIdx, int goalIdx, List<int> path)
    {
        // Walk back via cameFrom, then reverse into the output list.
        int cur = goalIdx;
        path.Add(cur);
        while (cur != startIdx)
        {
            cur = _cameFrom[cur];
            path.Add(cur);
        }
        path.Reverse();
    }

    /// <summary>
    /// String-pulling smoother: if a straight line from waypoint i to waypoint i+2
    /// crosses only passable tiles, drop i+1. Single pass; preserves start and goal.
    /// Off by default behind <see cref="EnableSmoothing"/>; changing this flag changes
    /// state hashes, so it stays off until validated.
    /// </summary>
    private static void SmoothPath(Grid grid, List<int> path, Func<int, bool>? isBlocked)
    {
        if (path.Count < 3) return;

        int writeIdx = 1;
        for (int i = 1; i < path.Count - 1; i++)
        {
            int prev = path[writeIdx - 1];
            int next = path[i + 1];
            if (LineOfSightPassable(grid, prev, next, isBlocked))
                continue; // drop path[i]
            path[writeIdx++] = path[i];
        }
        path[writeIdx++] = path[^1];
        if (writeIdx < path.Count) path.RemoveRange(writeIdx, path.Count - writeIdx);
    }

    private static bool LineOfSightPassable(Grid grid, int aIdx, int bIdx, Func<int, bool>? isBlocked)
    {
        // Bresenham-style integer line walk. Each visited tile must be passable.
        int ax = aIdx % Grid.Width, ay = aIdx / Grid.Width;
        int bx = bIdx % Grid.Width, by = bIdx / Grid.Width;
        int dx = Math.Abs(bx - ax), dy = Math.Abs(by - ay);
        int sx = ax < bx ? 1 : -1;
        int sy = ay < by ? 1 : -1;
        int err = dx - dy;
        int x = ax, y = ay;
        while (true)
        {
            if (!grid.IsPassable(x, y)) return false;
            int idx = y * Grid.Width + x;
            if (isBlocked != null && idx != aIdx && isBlocked(idx)) return false;
            if (x == bx && y == by) return true;
            int e2 = err * 2;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 <  dx) { err += dx; y += sy; }
        }
    }

    private static int OctileHeuristic(int ax, int ay, int bx, int by)
    {
        int dx = Math.Abs(ax - bx);
        int dy = Math.Abs(ay - by);
        return Math.Max(dx, dy) * CostOrthogonal + Math.Min(dx, dy) * (CostDiagonal - CostOrthogonal);
    }

    // --- Heap ---

    private readonly struct HeapNode
    {
        public readonly int NodeIdx;
        public readonly int F;
        public readonly int H;
        public HeapNode(int nodeIdx, int f, int h) { NodeIdx = nodeIdx; F = f; H = h; }

        /// <summary>
        /// Comparison: lower F, then lower H, then lower NodeIdx. NodeIdx as final tie-breaker
        /// is what makes A* output identical across runs. Never use the heap's internal
        /// insertion order — it's an implementation detail that varies with array growth.
        /// </summary>
        public int CompareTo(HeapNode other)
        {
            if (F != other.F) return F < other.F ? -1 : 1;
            if (H != other.H) return H < other.H ? -1 : 1;
            if (NodeIdx != other.NodeIdx) return NodeIdx < other.NodeIdx ? -1 : 1;
            return 0;
        }
    }

    private sealed class BinaryHeap
    {
        private HeapNode[] _items;
        public int Count { get; private set; }

        public BinaryHeap(int capacity) { _items = new HeapNode[capacity]; }

        public void Clear() { Count = 0; }

        public void Push(HeapNode item)
        {
            if (Count == _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            int i = Count++;
            _items[i] = item;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_items[parent].CompareTo(_items[i]) <= 0) break;
                (_items[parent], _items[i]) = (_items[i], _items[parent]);
                i = parent;
            }
        }

        public HeapNode Pop()
        {
            var top = _items[0];
            int last = --Count;
            if (last > 0)
            {
                _items[0] = _items[last];
                int i = 0;
                while (true)
                {
                    int left = (i << 1) + 1;
                    if (left >= last) break;
                    int right = left + 1;
                    int smaller = (right < last && _items[right].CompareTo(_items[left]) < 0) ? right : left;
                    if (_items[i].CompareTo(_items[smaller]) <= 0) break;
                    (_items[i], _items[smaller]) = (_items[smaller], _items[i]);
                    i = smaller;
                }
            }
            return top;
        }
    }
}
