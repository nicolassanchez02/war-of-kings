using System.Collections.Generic;
using Godot;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;

namespace WarOfKings.Presentation;

/// <summary>
/// M2 stand-in. Owns the simulation, ticks it at 20 Hz, renders terrain + units, handles
/// camera (WASD pan + wheel zoom), and routes left/right click to selection + move commands.
///
/// Render modes: <see cref="RenderMode.Primitives"/> is the only mode wired today (shapes &amp;
/// colors). The sprite mode is reserved for Part 6 (Kenney asset pipeline). F8 toggles the
/// enum but only logs when no sprites are available.
///
/// Visual fidelity disclaimer: this file was written without an interactive Godot session
/// available for review. Behaviors (camera pixel rates, exact zoom levels, tile colors,
/// click-test pixel radius) are best-guess defaults. See `docs/OPEN_QUESTIONS.md` for the
/// list of choices Nick will likely want to revisit.
/// </summary>
public partial class Main : Node2D
{
    private const double TickDurationSeconds = 0.05;    // 20 Hz
    public const float PixelsPerTile = 32f;

    // Camera defaults. Pan rate is per-second at zoom 1.0 and scales inversely with zoom.
    private const float CameraPanPxPerSec = 600f;
    private const float EdgePanMarginPx = 20f;
    private static readonly float[] ZoomLevels = { 0.5f, 0.75f, 1.0f, 1.5f, 2.0f };

    private enum RenderMode { Primitives, Sprites }

    private World _world = null!;
    private double _tickAccumulator;

    // Camera: world-space pixel offset of the viewport's top-left corner, plus a zoom factor.
    private Vector2 _cameraTopLeft = Vector2.Zero;
    private int _zoomLevelIdx = 2; // 1.0x default
    private RenderMode _renderMode = RenderMode.Primitives;

    // Selection + input.
    private readonly HashSet<long> _selectedUnitIds = new();
    private uint _nextSequenceP1 = 1;
    private bool _draggingSelectionBox;
    private Vector2 _dragStartScreen;
    private Vector2 _dragEndScreen;
    private bool _showDebugPanel;

    // Pending commands for the next tick.
    private readonly List<Command> _pendingCommands = new();

    public override void _Ready()
    {
        _world = new World(0xC0FFEEUL);

        // M3 starter scenario: carve a flat play arena, drop a Town Hall for each player,
        // sprinkle trees and a berry patch near each TC, and spawn a handful of villagers.
        for (int y = 8; y < 50; y++)
            for (int x = 8; x < 70; x++)
                _world.Map.SetTerrain(x, y, Terrain.Plain);

        // Starting resources.
        _world.GetPlayer(PlayerId.Player1).Wood = Fixed64.FromInt(200);
        _world.GetPlayer(PlayerId.Player1).Food = Fixed64.FromInt(200);
        _world.GetPlayer(PlayerId.Player1).Gold = Fixed64.FromInt(100);
        _world.GetPlayer(PlayerId.Player1).PopCap = 30;
        _world.GetPlayer(PlayerId.Player2).Wood = Fixed64.FromInt(200);
        _world.GetPlayer(PlayerId.Player2).Food = Fixed64.FromInt(200);
        _world.GetPlayer(PlayerId.Player2).Gold = Fixed64.FromInt(100);
        _world.GetPlayer(PlayerId.Player2).PopCap = 30;

        // Player 1 base: TC at (15, 20), trees southwest, berries northwest.
        _world.CreateBuilding(BuildingTypeId.TownHall, PlayerId.Player1, tileX: 15, tileY: 18, footprintW: 3, footprintH: 3, hpMax: 600);
        SpawnTreeCluster(centerX: 12, centerY: 24, radius: 3, count: 8);
        SpawnBerryPatch(centerX: 12, centerY: 14, count: 5);

        // Player 2 base: TC at (55, 30), mirrored cluster.
        _world.CreateBuilding(BuildingTypeId.TownHall, PlayerId.Player2, tileX: 55, tileY: 30, footprintW: 3, footprintH: 3, hpMax: 600);
        SpawnTreeCluster(centerX: 60, centerY: 36, radius: 3, count: 8);
        SpawnBerryPatch(centerX: 60, centerY: 26, count: 5);

        // Starting villagers.
        for (int i = 0; i < 3; i++)
            _world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(19 + i, 22));
        for (int i = 0; i < 3; i++)
            _world.CreateUnit(PlayerId.Player2, FixedVector2.FromInts(53 - i, 34));

        // Center camera on P1's base.
        var viewport = GetViewportRect().Size;
        _cameraTopLeft = new Vector2(16 * PixelsPerTile - viewport.X / 2, 22 * PixelsPerTile - viewport.Y / 2);

        SetProcess(true);
        SetProcessUnhandledInput(true);
    }

    private void SpawnTreeCluster(int centerX, int centerY, int radius, int count)
    {
        // Deterministic placement via the world RNG so two runs match.
        int placed = 0;
        // Scan outward in a small spiral; place on the first `count` passable tiles.
        for (int r = 0; r <= radius && placed < count; r++)
        {
            for (int dy = -r; dy <= r && placed < count; dy++)
            {
                for (int dx = -r; dx <= r && placed < count; dx++)
                {
                    if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r) continue;
                    int x = centerX + dx, y = centerY + dy;
                    if (!_world.Map.IsPassable(x, y)) continue;
                    if (!_world.GetOccupant(y * Grid.Width + x).IsNone) continue;
                    _world.CreateTree(x, y);
                    placed++;
                }
            }
        }
    }

    private void SpawnBerryPatch(int centerX, int centerY, int count)
    {
        int placed = 0;
        for (int dy = 0; dy < 3 && placed < count; dy++)
        {
            for (int dx = 0; dx < 3 && placed < count; dx++)
            {
                int x = centerX + dx - 1, y = centerY + dy - 1;
                if (!_world.Map.IsPassable(x, y)) continue;
                if (!_world.GetOccupant(y * Grid.Width + x).IsNone) continue;
                _world.CreateBerryBush(x, y);
                placed++;
            }
        }
    }

    public override void _Process(double delta)
    {
        UpdateCamera((float)delta);

        // Pump the sim. Commands collected since the last tick are applied now.
        _tickAccumulator += delta;
        while (_tickAccumulator >= TickDurationSeconds)
        {
            _world.Step(_pendingCommands);
            _pendingCommands.Clear();
            _tickAccumulator -= TickDurationSeconds;
        }
        QueueRedraw();
    }

    // --- Camera ---

    private float CurrentZoom => ZoomLevels[_zoomLevelIdx];

    private void UpdateCamera(float dt)
    {
        var input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))    input.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))  input.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))  input.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1;

        // Edge-pan: cursor near a viewport edge nudges the camera in that direction.
        var viewport = GetViewportRect().Size;
        var mouse = GetViewport().GetMousePosition();
        if (mouse.X < EdgePanMarginPx) input.X -= 1;
        if (mouse.X > viewport.X - EdgePanMarginPx) input.X += 1;
        if (mouse.Y < EdgePanMarginPx) input.Y -= 1;
        if (mouse.Y > viewport.Y - EdgePanMarginPx) input.Y += 1;

        if (input != Vector2.Zero)
        {
            input = input.Normalized();
            _cameraTopLeft += input * (CameraPanPxPerSec / CurrentZoom) * dt;
        }

        // Clamp so we can't pan off the world. Margin = half the viewport on each side.
        float worldWidthPx = Grid.Width * PixelsPerTile;
        float worldHeightPx = Grid.Height * PixelsPerTile;
        _cameraTopLeft.X = Mathf.Clamp(_cameraTopLeft.X, -viewport.X / 2, worldWidthPx - viewport.X / 2);
        _cameraTopLeft.Y = Mathf.Clamp(_cameraTopLeft.Y, -viewport.Y / 2, worldHeightPx - viewport.Y / 2);
    }

    private Vector2 ScreenToWorldPx(Vector2 screen) => _cameraTopLeft + screen / CurrentZoom;
    private Vector2 WorldPxToScreen(Vector2 worldPx) => (worldPx - _cameraTopLeft) * CurrentZoom;
    private Vector2 WorldTileToWorldPx(int x, int y) => new(x * PixelsPerTile, y * PixelsPerTile);
    private (int x, int y) WorldPxToTile(Vector2 worldPx) => ((int)(worldPx.X / PixelsPerTile), (int)(worldPx.Y / PixelsPerTile));

    // --- Input ---

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventKey { Pressed: true, Keycode: Key.F8 }:
                _renderMode = _renderMode == RenderMode.Primitives ? RenderMode.Sprites : RenderMode.Primitives;
                GD.Print($"Render mode -> {_renderMode} (sprites pipeline lands in Part 6)");
                break;
            case InputEventKey { Pressed: true, Keycode: Key.F3 }:
                _showDebugPanel = !_showDebugPanel;
                break;
            case InputEventKey { Pressed: true, Keycode: Key.V }:
                IssueTrainVillager();
                break;
            case InputEventMouseButton mb:
                HandleMouseButton(mb);
                break;
            case InputEventMouseMotion mm when _draggingSelectionBox:
                _dragEndScreen = mm.Position;
                break;
        }
    }

    private void HandleMouseButton(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
        {
            _zoomLevelIdx = Mathf.Min(_zoomLevelIdx + 1, ZoomLevels.Length - 1);
            return;
        }
        if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
        {
            _zoomLevelIdx = Mathf.Max(_zoomLevelIdx - 1, 0);
            return;
        }

        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _draggingSelectionBox = true;
                _dragStartScreen = mb.Position;
                _dragEndScreen = mb.Position;
            }
            else
            {
                _draggingSelectionBox = false;
                CompleteSelection(mb.Position, additive: mb.ShiftPressed);
            }
            return;
        }

        if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            IssueRightClick(mb.Position);
        }
    }

    private void CompleteSelection(Vector2 releaseScreen, bool additive)
    {
        var startWorld = ScreenToWorldPx(_dragStartScreen);
        var endWorld = ScreenToWorldPx(releaseScreen);
        var rect = new Rect2(
            new Vector2(Mathf.Min(startWorld.X, endWorld.X), Mathf.Min(startWorld.Y, endWorld.Y)),
            new Vector2(Mathf.Abs(endWorld.X - startWorld.X), Mathf.Abs(endWorld.Y - startWorld.Y)));

        if (!additive) _selectedUnitIds.Clear();

        // Treat tiny drags as a click: pick the nearest owned unit within a small radius.
        if (rect.Size.X < 4 && rect.Size.Y < 4)
        {
            float bestDistSq = float.MaxValue;
            long? best = null;
            var clickWorld = startWorld;
            foreach (var u in _world.UnitsOrderedById())
            {
                if (u.Owner != PlayerId.Player1) continue;  // single-player owns P1 for now
                var px = UnitWorldPx(u);
                var d = px - clickWorld;
                var dSq = d.X * d.X + d.Y * d.Y;
                if (dSq < bestDistSq && dSq < 25 * 25) { bestDistSq = dSq; best = u.Id.Value; }
            }
            if (best is long id) _selectedUnitIds.Add(id);
        }
        else
        {
            foreach (var u in _world.UnitsOrderedById())
            {
                if (u.Owner != PlayerId.Player1) continue;
                var px = UnitWorldPx(u);
                if (rect.HasPoint(px)) _selectedUnitIds.Add(u.Id.Value);
            }
        }
    }

    private void IssueTrainVillager()
    {
        // Find Player 1's first (lowest-ID) Town Hall and queue a villager there.
        Building? tc = null;
        foreach (var b in _world.BuildingsOrderedById())
        {
            if (b.Owner != PlayerId.Player1) continue;
            if (b.Type != BuildingTypeId.TownHall) continue;
            if (b.IsDestroyed) continue;
            tc = b;
            break;
        }
        if (tc is null) { GD.Print("No P1 Town Hall available to train at."); return; }

        _pendingCommands.Add(new TrainCommand
        {
            ExecuteAtTick = _world.CurrentTick,
            Player = PlayerId.Player1,
            Sequence = _nextSequenceP1++,
            ProductionBuilding = tc.Id,
            UnitTypeId = 1, // villager
        });
    }

    private void IssueRightClick(Vector2 screen)
    {
        if (_selectedUnitIds.Count == 0) return;
        var worldPx = ScreenToWorldPx(screen);
        var (tx, ty) = WorldPxToTile(worldPx);
        tx = Mathf.Clamp(tx, 0, Grid.Width - 1);
        ty = Mathf.Clamp(ty, 0, Grid.Height - 1);

        var ids = new List<EntityId>();
        foreach (var id in _selectedUnitIds) ids.Add(new EntityId(id));
        ids.Sort();

        // What's at the clicked tile? If it's a Tree/BerryBush owned by no-one, issue a
        // GatherCommand. Otherwise, default to MoveCommand.
        int clickedIdx = ty * Grid.Width + tx;
        var occupant = _world.GetOccupant(clickedIdx);
        if (!occupant.IsNone && _world.TryGetEntity(occupant, out var obj))
        {
            if (obj is WarOfKings.Simulation.Entities.Tree || obj is BerryBush)
            {
                _pendingCommands.Add(new GatherCommand
                {
                    ExecuteAtTick = _world.CurrentTick,
                    Player = PlayerId.Player1,
                    Sequence = _nextSequenceP1++,
                    Gatherers = ids.ToArray(),
                    ResourceNode = occupant,
                });
                return;
            }
        }

        _pendingCommands.Add(new MoveCommand
        {
            ExecuteAtTick = _world.CurrentTick,
            Player = PlayerId.Player1,
            Sequence = _nextSequenceP1++,
            Units = ids.ToArray(),
            Target = FixedVector2.FromInts(tx, ty),
        });
    }

    // --- Rendering ---

    public override void _Draw()
    {
        DrawTerrain();
        DrawBuildings();
        DrawResources();
        DrawSelectionBox();
        DrawUnits();
        DrawHud();
    }

    private void DrawBuildings()
    {
        foreach (var b in _world.BuildingsOrderedById())
        {
            if (b.IsDestroyed) continue;
            var topLeftWorld = new Vector2(b.TileX * PixelsPerTile, b.TileY * PixelsPerTile);
            var sizeWorld = new Vector2(b.FootprintW * PixelsPerTile, b.FootprintH * PixelsPerTile);
            var topLeftScreen = WorldPxToScreen(topLeftWorld);
            var sizeScreen = sizeWorld * CurrentZoom;
            var rect = new Rect2(topLeftScreen, sizeScreen);
            var fill = b.Owner == PlayerId.Player1 ? new Color(0.30f, 0.45f, 0.65f) : new Color(0.65f, 0.30f, 0.30f);
            DrawRect(rect, fill, filled: true);
            DrawRect(rect, new Color(0, 0, 0, 0.85f), filled: false, width: 2f);

            // Type label centered on the building.
            var font = ThemeDB.FallbackFont;
            string label = b.Type switch
            {
                BuildingTypeId.TownHall => "TC",
                BuildingTypeId.House => "Hs",
                BuildingTypeId.Barracks => "Bk",
                BuildingTypeId.LumberCamp => "Lc",
                BuildingTypeId.Mill => "Ml",
                _ => "?",
            };
            DrawString(font, topLeftScreen + sizeScreen / 2 - new Vector2(10, -4), label,
                HorizontalAlignment.Left, -1, (int)(14 * CurrentZoom), new Color(1, 1, 1, 0.95f));

            if (b.HpCurrent < b.HpMax)
            {
                float frac = (float)(b.HpCurrent.ToFloatForRender() / b.HpMax.ToFloatForRender());
                var barOrigin = topLeftScreen + new Vector2(0, -6);
                var barSize = new Vector2(sizeScreen.X, 4);
                DrawRect(new Rect2(barOrigin, barSize), new Color(0.2f, 0.05f, 0.05f));
                DrawRect(new Rect2(barOrigin, new Vector2(barSize.X * frac, barSize.Y)), HpColor(frac));
            }
        }
    }

    private void DrawResources()
    {
        // Trees: dark green disk, shrinks with remaining wood (75/50/25 thresholds).
        foreach (var t in _world.TreesOrderedById())
        {
            if (t.IsDepleted) continue;
            var worldPx = new Vector2(t.TileX * PixelsPerTile + PixelsPerTile / 2, t.TileY * PixelsPerTile + PixelsPerTile / 2);
            var screen = WorldPxToScreen(worldPx);
            float frac = (float)(t.WoodRemaining.ToFloatForRender() / t.WoodMax.ToFloatForRender());
            // Visual size shrinks with remaining wood: 1.0 → 0.75 → 0.5 → 0.25 stepped.
            float scale = frac > 0.75f ? 1.0f : frac > 0.5f ? 0.85f : frac > 0.25f ? 0.65f : 0.5f;
            float radius = 12f * scale * CurrentZoom;
            DrawCircle(screen, radius, new Color(0.10f, 0.32f, 0.16f));
            DrawArc(screen, radius, 0, Mathf.Tau, 24, new Color(0, 0, 0, 0.85f), 1.5f, true);
        }

        // Berry bushes: red dot cluster.
        foreach (var bush in _world.BushesOrderedById())
        {
            if (bush.IsDepleted) continue;
            var worldPx = new Vector2(bush.TileX * PixelsPerTile + PixelsPerTile / 2, bush.TileY * PixelsPerTile + PixelsPerTile / 2);
            var screen = WorldPxToScreen(worldPx);
            float radius = 9f * CurrentZoom;
            DrawCircle(screen, radius, new Color(0.45f, 0.15f, 0.20f));
            // Two tiny dots inside to look bush-ish.
            DrawCircle(screen + new Vector2(-3, -2) * CurrentZoom, 2.5f * CurrentZoom, new Color(0.85f, 0.25f, 0.30f));
            DrawCircle(screen + new Vector2(3, 2) * CurrentZoom, 2.5f * CurrentZoom, new Color(0.85f, 0.25f, 0.30f));
        }
    }

    private void DrawTerrain()
    {
        // Compute the tile range visible at the current camera + zoom.
        var viewport = GetViewportRect().Size;
        var topLeftWorld = ScreenToWorldPx(Vector2.Zero);
        var bottomRightWorld = ScreenToWorldPx(viewport);
        int x0 = Mathf.Max(0, (int)(topLeftWorld.X / PixelsPerTile));
        int y0 = Mathf.Max(0, (int)(topLeftWorld.Y / PixelsPerTile));
        int x1 = Mathf.Min(Grid.Width - 1, (int)(bottomRightWorld.X / PixelsPerTile));
        int y1 = Mathf.Min(Grid.Height - 1, (int)(bottomRightWorld.Y / PixelsPerTile));

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                var terrain = _world.Map.GetTerrain(x, y);
                var color = TerrainColor(terrain);
                var worldPx = new Vector2(x * PixelsPerTile, y * PixelsPerTile);
                var screen = WorldPxToScreen(worldPx);
                var size = new Vector2(PixelsPerTile, PixelsPerTile) * CurrentZoom;
                DrawRect(new Rect2(screen, size), color);
            }
        }
    }

    private static Color TerrainColor(Terrain t) => t switch
    {
        Terrain.Plain    => new Color(0.30f, 0.55f, 0.25f),
        Terrain.Forest   => new Color(0.15f, 0.35f, 0.18f),
        Terrain.Mountain => new Color(0.45f, 0.42f, 0.38f),
        Terrain.Water    => new Color(0.18f, 0.40f, 0.62f),
        Terrain.Gold     => new Color(0.85f, 0.75f, 0.30f),
        _ => Colors.Magenta,
    };

    private void DrawUnits()
    {
        foreach (var u in _world.UnitsOrderedById())
        {
            var screen = WorldPxToScreen(UnitWorldPx(u));
            var radius = 14f * CurrentZoom;
            var color = u.Owner == PlayerId.Player1 ? new Color(0.55f, 0.78f, 0.95f) : new Color(0.95f, 0.45f, 0.45f);

            if (_selectedUnitIds.Contains(u.Id.Value))
            {
                // Selection ring beneath the unit.
                DrawArc(screen, radius + 4, 0, Mathf.Tau, 36, new Color(1f, 1f, 0.6f, 0.85f), 2.5f, true);
            }

            DrawCircle(screen, radius, color);
            DrawArc(screen, radius, 0, Mathf.Tau, 32, new Color(0, 0, 0, 0.6f), 1.5f, true);

            // HP bar above unit (always-on for now; M5 will dim when full).
            if (u.HpCurrent < u.HpMax)
            {
                float frac = (float)(u.HpCurrent.ToFloatForRender() / u.HpMax.ToFloatForRender());
                var barOrigin = screen + new Vector2(-radius, -radius - 6);
                var barSize = new Vector2(radius * 2, 4);
                DrawRect(new Rect2(barOrigin, barSize), new Color(0.2f, 0.05f, 0.05f));
                DrawRect(new Rect2(barOrigin, new Vector2(barSize.X * frac, barSize.Y)), HpColor(frac));
            }
        }
    }

    private static Color HpColor(float frac)
        => frac > 0.5f ? new Color(0.3f, 0.85f, 0.3f)
         : frac > 0.25f ? new Color(0.95f, 0.85f, 0.2f)
         : new Color(0.95f, 0.2f, 0.2f);

    private Vector2 UnitWorldPx(WarOfKings.Simulation.Entities.Unit u)
    {
        return new Vector2(
            u.Position.X.ToFloatForRender() * PixelsPerTile + PixelsPerTile / 2,
            u.Position.Y.ToFloatForRender() * PixelsPerTile + PixelsPerTile / 2);
    }

    private void DrawSelectionBox()
    {
        if (!_draggingSelectionBox) return;
        var origin = new Vector2(Mathf.Min(_dragStartScreen.X, _dragEndScreen.X), Mathf.Min(_dragStartScreen.Y, _dragEndScreen.Y));
        var size = new Vector2(Mathf.Abs(_dragEndScreen.X - _dragStartScreen.X), Mathf.Abs(_dragEndScreen.Y - _dragStartScreen.Y));
        var rect = new Rect2(origin, size);
        DrawRect(rect, new Color(0.6f, 0.95f, 0.4f, 0.10f), filled: true);
        DrawRect(rect, new Color(0.6f, 0.95f, 0.4f, 0.85f), filled: false, width: 1.5f);
    }

    private void DrawHud()
    {
        var font = ThemeDB.FallbackFont;
        var topBar = $"tick {_world.CurrentTick}    hash 0x{_world.ComputeStateHash():X16}    FPS {Engine.GetFramesPerSecond():0}    zoom {CurrentZoom:0.00}x";
        DrawString(font, new Vector2(16, 28), topBar, HorizontalAlignment.Left, -1, 16, new Color(0.95f, 0.95f, 0.95f));

        var hints = "WASD pan | wheel zoom | LMB select / drag-box | RMB move/gather | V train villager | F3 debug | F8 sprites toggle";
        DrawString(font, new Vector2(16, 52), hints, HorizontalAlignment.Left, -1, 13, new Color(0.7f, 0.7f, 0.7f));

        var selText = $"selected: {_selectedUnitIds.Count}";
        DrawString(font, new Vector2(16, 76), selText, HorizontalAlignment.Left, -1, 13, new Color(0.85f, 0.85f, 0.6f));

        // Player 1 resource panel (top right).
        var p1 = _world.GetPlayer(PlayerId.Player1);
        var vp = GetViewportRect().Size;
        float resX = vp.X - 280;
        DrawRect(new Rect2(resX - 8, 16, 280, 80), new Color(0, 0, 0, 0.55f));
        DrawString(font, new Vector2(resX, 36), $"Wood:  {(int)p1.Wood.ToFloatForRender()}", HorizontalAlignment.Left, -1, 15, new Color(0.85f, 0.65f, 0.40f));
        DrawString(font, new Vector2(resX, 56), $"Food:  {(int)p1.Food.ToFloatForRender()}", HorizontalAlignment.Left, -1, 15, new Color(0.55f, 0.85f, 0.50f));
        DrawString(font, new Vector2(resX, 76), $"Gold:  {(int)p1.Gold.ToFloatForRender()}", HorizontalAlignment.Left, -1, 15, new Color(0.95f, 0.85f, 0.30f));
        DrawString(font, new Vector2(resX + 140, 36), $"Pop:   {p1.PopCurrent}/{p1.PopCap}", HorizontalAlignment.Left, -1, 15, new Color(0.9f, 0.9f, 0.9f));

        // Training status: show queue + progress for any P1 TC with work in flight.
        float trainY = 110;
        foreach (var b in _world.BuildingsOrderedById())
        {
            if (b.Owner != PlayerId.Player1) continue;
            if (b.ProductionQueue.Count == 0) continue;
            int unitTypeId = b.ProductionQueue[0];
            int total = WarOfKings.Simulation.Systems.ProductionSystem.TrainTicksFor(unitTypeId);
            int prog = b.ProductionProgressTicks;
            float frac = total > 0 ? (float)prog / total : 0f;
            string label = $"Training Villager  {prog}/{total} ticks  ({b.ProductionQueue.Count} in queue)";
            DrawRect(new Rect2(16, trainY - 14, 360, 22), new Color(0, 0, 0, 0.55f));
            DrawString(font, new Vector2(22, trainY), label, HorizontalAlignment.Left, -1, 13, new Color(0.85f, 0.85f, 0.85f));
            DrawRect(new Rect2(380, trainY - 12, 120, 14), new Color(0.15f, 0.15f, 0.15f));
            DrawRect(new Rect2(380, trainY - 12, 120 * frac, 14), new Color(0.55f, 0.85f, 0.50f));
            trainY += 26;
        }

        if (_showDebugPanel)
        {
            var lines = new List<string>
            {
                $"entities (units): {CountUnits()}",
                $"render mode: {_renderMode}",
                $"camera topleft: ({_cameraTopLeft.X:0}, {_cameraTopLeft.Y:0})",
                $"pending cmds: {_pendingCommands.Count}",
            };
            var viewport = GetViewportRect().Size;
            float panelX = viewport.X - 280;
            float panelY = 76;
            DrawRect(new Rect2(panelX - 6, panelY - 18, 280, 18 * lines.Count + 12), new Color(0, 0, 0, 0.65f));
            for (int i = 0; i < lines.Count; i++)
                DrawString(font, new Vector2(panelX, panelY + i * 18), lines[i], HorizontalAlignment.Left, -1, 13, new Color(0.85f, 0.85f, 0.85f));
        }
    }

    private int CountUnits()
    {
        int n = 0;
        foreach (var _ in _world.UnitsOrderedById()) n++;
        return n;
    }
}
