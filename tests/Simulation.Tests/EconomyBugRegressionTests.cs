using System.Collections.Generic;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;
using Xunit;

namespace WarOfKings.Simulation.Tests;

/// <summary>
/// Regression tests for three economy bugs fixed in the same pass:
///
///   1. ProductionSystem double-counted PopCurrent: World.CreateUnit already increments pop
///      by 1; ProductionSystem must NOT add popCost on top of that.
///
///   2. HandleDepositing cleared u.Carried before calling FindNearbySameKind, so the search
///      always returned false (kind == None) and villagers went idle after every deposit
///      instead of finding a new same-kind resource.
///
///   3. ApplyGatherCommand assigned all N gatherers the same adjacent tile, causing all but
///      the first to collide, exhaust their repath budget, and sit idle at the resource.
/// </summary>
public class EconomyBugRegressionTests
{
    // Build a flat-terrain world (no random map noise in tests).
    private static World FlatWorld(ulong seed = 1UL)
    {
        var w = new World(seed);
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
                w.Map.SetTerrain(x, y, Terrain.Plain);
        return w;
    }

    private static void RunTicks(World world, int ticks, IReadOnlyList<Command>? firstTickCommands = null)
    {
        var empty = new List<Command>();
        for (int i = 0; i < ticks; i++)
            world.Step(i == 0 && firstTickCommands != null ? firstTickCommands : empty);
    }

    // -----------------------------------------------------------------------
    // Bug 1: double pop-increment
    // -----------------------------------------------------------------------

    /// <summary>
    /// Training a unit must increment PopCurrent by exactly 1.
    /// Before the fix, ProductionSystem added popCost after World.CreateUnit already had,
    /// so every trained unit cost 2 pop — breaking the economy with a pop cap of 30.
    /// </summary>
    [Fact]
    public void Training_unit_increments_pop_by_exactly_one()
    {
        var world = FlatWorld();
        world.GetPlayer(PlayerId.Player1).Food = Fixed64.FromInt(1000);
        world.GetPlayer(PlayerId.Player1).PopCap = 10;

        var tc = world.CreateBuilding(BuildingTypeId.TownHall, PlayerId.Player1,
                                      tileX: 5, tileY: 5,
                                      footprintW: 3, footprintH: 3, hpMax: 600);
        int popBefore = world.GetPlayer(PlayerId.Player1).PopCurrent;

        // Queue one villager.
        var train = new TrainCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            ProductionBuilding = tc.Id, UnitTypeId = 1,
        };
        // TrainTicksFor(1) = 500 ticks; run 600 to be safe.
        RunTicks(world, 600, new List<Command> { train });

        int popAfter = world.GetPlayer(PlayerId.Player1).PopCurrent;
        Assert.Equal(popBefore + 1, popAfter);
    }

    /// <summary>
    /// With a PopCap of 2 and 1 existing unit, training a second unit should fill the cap
    /// and leave PopCurrent == PopCap. Before the fix it would land at PopCurrent == 3
    /// (cap 2) and a second training attempt would be blocked incorrectly (or the cap
    /// would overflow on repeated trains).
    /// </summary>
    [Fact]
    public void Training_two_units_fills_pop_cap_exactly()
    {
        var world = FlatWorld();
        world.GetPlayer(PlayerId.Player1).Food = Fixed64.FromInt(1000);
        world.GetPlayer(PlayerId.Player1).PopCap = 2;

        var tc = world.CreateBuilding(BuildingTypeId.TownHall, PlayerId.Player1,
                                      tileX: 5, tileY: 5,
                                      footprintW: 3, footprintH: 3, hpMax: 600);

        // Train villager 1.
        var t1 = new TrainCommand { ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1, ProductionBuilding = tc.Id, UnitTypeId = 1 };
        RunTicks(world, 600, new List<Command> { t1 });
        Assert.Equal(1, world.GetPlayer(PlayerId.Player1).PopCurrent);

        // Train villager 2.
        var t2 = new TrainCommand { ExecuteAtTick = world.CurrentTick, Player = PlayerId.Player1, Sequence = 2, ProductionBuilding = tc.Id, UnitTypeId = 1 };
        RunTicks(world, 600, new List<Command> { t2 });
        Assert.Equal(2, world.GetPlayer(PlayerId.Player1).PopCurrent);
    }

    // -----------------------------------------------------------------------
    // Bug 2: FindNearbySameKind called after Carried cleared
    // -----------------------------------------------------------------------

    /// <summary>
    /// After depositing a full load, if the original resource is depleted but a nearby
    /// same-kind resource exists, the villager must retarget it rather than going idle.
    /// Before the fix, u.Carried was cleared before the same-kind search, so the search
    /// always found nothing (kind == None) and the villager went idle.
    /// </summary>
    [Fact]
    public void Villager_retargets_nearby_tree_when_original_depletes()
    {
        var world = FlatWorld();
        world.GetPlayer(PlayerId.Player1).PopCap = 30;

        // TC to receive wood.
        world.CreateBuilding(BuildingTypeId.TownHall, PlayerId.Player1,
                             tileX: 10, tileY: 10, footprintW: 3, footprintH: 3, hpMax: 600);

        // A tiny tree (1 wood) — villager will deplete it on the first chop.
        // A second, nearby tree with plenty of wood — must become the retarget.
        var tinyTree  = world.CreateTree(20, 15, woodMax: 1);
        var nearbyTree = world.CreateTree(21, 15, woodMax: 100);

        var villager = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(18, 15));

        var gather = new GatherCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Gatherers = new[] { villager.Id },
            ResourceNode = tinyTree.Id,
        };

        // Run long enough for: walk to tree (short), chop 1 tick, deposit, walk back.
        // With a 1-wood tree the deposit must happen and then the villager should be heading
        // back to gather (either still alive tiny tree or the nearby tree). We validate that
        // the player actually received wood (i.e., a deposit happened) and that the villager
        // is not idle at the end (it found the nearby tree).
        RunTicks(world, 2000, new List<Command> { gather });

        Assert.True(world.GetPlayer(PlayerId.Player1).Wood.ToInt() > 0,
            "Villager never deposited wood — gathering loop broken.");

        // Villager should be in the gathering loop (GoingToResource, Gathering, GoingToDropOff,
        // or Depositing) — NOT Generic/Idle — because the nearby tree still has wood.
        Assert.NotEqual(BehaviorKind.Generic, villager.Behavior);
    }

    // -----------------------------------------------------------------------
    // Bug 3: all gatherers sent to the same adjacent tile
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sending 4 villagers to gather the same tree should spread them to different adjacent
    /// tiles. Before the fix, all four were assigned the same single adjacent tile index;
    /// three exhausted their repath window and went idle, leaving only one gathering.
    /// We verify this by placing the tree close to the TC so each round-trip is fast and
    /// 4 gatherers can collectively deposit more wood than 1 in the same tick budget.
    /// </summary>
    [Fact]
    public void Multiple_gatherers_deposit_more_wood_than_one_alone()
    {
        // Single-villager baseline: tree at (20,10), TC at (5,5), villager starts at (10,10).
        int woodSingle = RunGatherBenchmark(numVillagers: 1);
        // Four-villager run with the same layout.
        int woodFour = RunGatherBenchmark(numVillagers: 4);

        // Four villagers must collectively deliver strictly more wood than one.
        // If 3 out of 4 are idle (pre-fix behavior), woodFour would equal woodSingle.
        Assert.True(woodFour > woodSingle,
            $"Expected 4 gatherers ({woodFour}) to beat 1 ({woodSingle}). " +
            "If equal, most gatherers are idling at the same tile (pre-fix regression).");
    }

    /// <summary>
    /// Helper: runs a gather benchmark with <paramref name="numVillagers"/> starting near
    /// (10,10), tree at (20,10), TC at (5,5). Returns wood deposited in 3000 ticks.
    /// Short distance means multiple round-trips complete within the budget.
    /// </summary>
    private static int RunGatherBenchmark(int numVillagers)
    {
        var world = FlatWorld();
        world.GetPlayer(PlayerId.Player1).PopCap = 30;
        // TC at (5,5), footprint 3x3 — drop-off is close so trips are short.
        world.CreateBuilding(BuildingTypeId.TownHall, PlayerId.Player1, 5, 5, 3, 3, 600);
        // Tree at (20,10) — ~12 tiles from TC, easy walk.
        var tree = world.CreateTree(20, 10, woodMax: 5000);

        var villagerIds = new EntityId[numVillagers];
        for (int i = 0; i < numVillagers; i++)
            villagerIds[i] = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(10 + i, 10)).Id;

        var gather = new GatherCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Gatherers = villagerIds,
            ResourceNode = tree.Id,
        };
        var empty = new List<Command>();
        for (int i = 0; i < 3000; i++)
            world.Step(i == 0 ? new List<Command> { gather } : empty);

        return world.GetPlayer(PlayerId.Player1).Wood.ToInt();
    }
}
