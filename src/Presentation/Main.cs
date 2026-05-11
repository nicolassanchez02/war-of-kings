using System.Collections.Generic;
using Godot;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
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

        // For M2 we want a friendly map so units can actually walk around. Carve a big plain
        // arena at the center of the procedurally-generated terrain.
        for (int y = 8; y < 32; y++)
            for (int x = 8; x < 60; x++)
                _world.Map.SetTerrain(x, y, Terrain.Plain);

        for (int i = 0; i < 6; i++)
        {
            var owner = (i % 2 == 0) ? PlayerId.Player1 : PlayerId.Player2;
            _world.CreateUnit(owner, FixedVector2.FromInts(12 + i * 3, 18));
        }

        // Center camera on the play arena (around tile (30, 20)).
        var viewport = GetViewportRect().Size;
        _cameraTopLeft = new Vector2(30 * PixelsPerTile - viewport.X / 2, 20 * PixelsPerTile - viewport.Y / 2);

        SetProcess(true);
        SetProcessUnhandledInput(true);
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
            IssueMoveCommand(mb.Position);
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

    private void IssueMoveCommand(Vector2 screen)
    {
        if (_selectedUnitIds.Count == 0) return;
        var worldPx = ScreenToWorldPx(screen);
        var (tx, ty) = WorldPxToTile(worldPx);
        tx = Mathf.Clamp(tx, 0, Grid.Width - 1);
        ty = Mathf.Clamp(ty, 0, Grid.Height - 1);

        var ids = new List<EntityId>();
        foreach (var id in _selectedUnitIds) ids.Add(new EntityId(id));
        ids.Sort();

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
        DrawSelectionBox();
        DrawUnits();
        DrawHud();
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

        var hints = "WASD pan | wheel zoom | LMB select / drag-box | RMB move | F3 debug | F8 sprites toggle";
        DrawString(font, new Vector2(16, 52), hints, HorizontalAlignment.Left, -1, 13, new Color(0.7f, 0.7f, 0.7f));

        var selText = $"selected: {_selectedUnitIds.Count}";
        DrawString(font, new Vector2(16, 76), selText, HorizontalAlignment.Left, -1, 13, new Color(0.85f, 0.85f, 0.6f));

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
