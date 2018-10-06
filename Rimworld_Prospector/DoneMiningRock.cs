using System.Reflection;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    // A mining operation has ended
    [HarmonyPatch(typeof(Mineable), "DestroyMined")]
    [StaticConstructorOnStartup]
    internal static class DoneMiningRock
    {
        static DoneMiningRock()
        {
            HarmonyInstance harmony = HarmonyInstance.Create("com.firefueled.rimworld_prospector");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        
        static void Postfix(Pawn pawn)
        {
            // A cell grid around the mining pawn covering an area two cells away from it
            var cellsAround = GenAdj.CellsOccupiedBy(pawn.Position, pawn.Rotation, new IntVec2(5, 5));
            var dm = new Designator_Mine();

            foreach (IntVec3 cell in cellsAround)
            {
                // Find out what Thing, of the Bulding, category is on the cell
                Thing thing = pawn.Map.thingGrid.ThingAt(cell, ThingCategory.Building);
                if (
                        thing != null &&
                        IsOre(thing.def) &&
                        HasntBeenDesignatedYet(dm, cell) &&
                        CanReach(pawn, cell)
                    )
                {
                    // "Order" the cell to be mined
                    dm.DesignateSingleCell(cell);
                }
            }
        }

        // Wether an mine "Order" hassn't been placed on the cell yet
        private static bool HasntBeenDesignatedYet(Designator_Mine dm, IntVec3 cell)
        {
            return dm.CanDesignateCell(cell).Accepted;
        }

        // Wether the Thing is a rock with mineable resources
        private static bool IsOre(ThingDef def)
        {
            return def?.building != null && def.building.isResourceRock;
        }

        // Wether the pawn can reach the cell (Rock) to the point of being able to touch it,
        // instead of standing on it
        private static bool CanReach(Pawn pawn, IntVec3 cell)
        {
            return pawn.CanReach(cell, PathEndMode.Touch, Danger.None);
        }
    }
}
