using Godot;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;

namespace WarOfKings.Presentation;

/// <summary>
/// Minimal M2 stand-in: instantiates the simulation, spawns a handful of units,
/// drives the 20 Hz tick from Godot's _Process, and draws each unit as a colored
/// circle on the canvas with a small HUD showing the current tick and state hash.
///
/// The simulation is canonical; this script just visualizes it. No game state
/// originates here — it only reads.
/// </summary>
public partial class Main : Node2D
{
    private const double TickDurationSeconds = 0.05;     // 20 Hz
    private const float WorldToScreenScale = 32f;        // 1 sim unit = 32 px

    private World _world = null!;
    private double _tickAccumulator;

    public override void _Ready()
    {
        _world = new World(0xC0FFEEUL);
        for (int i = 0; i < 6; i++)
        {
            var owner = (i % 2 == 0) ? PlayerId.Player1 : PlayerId.Player2;
            var pos = FixedVector2.FromInts(i - 2, ((i * 3) % 5) - 2);
            _world.CreateUnit(owner, pos);
        }
    }

    public override void _Process(double delta)
    {
        _tickAccumulator += delta;
        var emptyCommands = System.Array.Empty<Command>();
        while (_tickAccumulator >= TickDurationSeconds)
        {
            _world.Step(emptyCommands);
            _tickAccumulator -= TickDurationSeconds;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = GetViewportRect().Size / 2;
        var font = ThemeDB.FallbackFont;

        foreach (var u in _world.UnitsOrderedById())
        {
            var local = new Vector2(u.Position.X.ToFloatForRender(), u.Position.Y.ToFloatForRender());
            var screen = center + local * WorldToScreenScale;
            var color = u.Owner.Value == PlayerId.Player1.Value ? new Color(0.55f, 0.78f, 0.95f) : new Color(0.95f, 0.45f, 0.45f);
            DrawCircle(screen, 14, color);
            DrawArc(screen, 14, 0, Mathf.Tau, 32, new Color(0, 0, 0, 0.6f), 1.5f, true);
            DrawString(font, screen + new Vector2(-8, 5), u.Id.Value.ToString(), HorizontalAlignment.Left, -1, 12, new Color(0, 0, 0));
        }

        var hud = $"tick {_world.CurrentTick}    hash 0x{_world.ComputeStateHash():X16}    units {CountUnits()}";
        DrawString(font, new Vector2(16, 28), hud, HorizontalAlignment.Left, -1, 16, new Color(0.9f, 0.9f, 0.9f));
        DrawString(font, new Vector2(16, 52), "War of Kings - M0 preview (static units, no input yet)", HorizontalAlignment.Left, -1, 13, new Color(0.7f, 0.7f, 0.7f));
    }

    private int CountUnits()
    {
        int n = 0;
        foreach (var _ in _world.UnitsOrderedById()) n++;
        return n;
    }
}
