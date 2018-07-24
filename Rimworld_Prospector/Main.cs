using Harmony;
using RimWorld;
using System.Reflection;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    [StaticConstructorOnStartup]
    class Main
    {
        // this static constructor runs to create a HarmonyInstance and install a patch.
        static Main()
        {
            var harmony = HarmonyInstance.Create("com.github.firefueled.rimworld_prospector");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        // A mining operation has ended
        [HarmonyPatch(typeof(Mineable), "DestroyMined")]
        class PatchDestroyMined
        {
            static void Postfix(Pawn pawn)
            {
                // A cell grid around the mining pawn covering an area two cells away from it
                var cellsAround = GenAdj.CellsOccupiedBy(pawn.Position, pawn.Rotation, new IntVec2(5, 5));
                Designator_Mine dm = new Designator_Mine();

                foreach (var cell in cellsAround)
                {
                    // Find out what Thing, of the Bulding, category is on the cell
                    var thing = pawn.Map.thingGrid.ThingAt(cell, ThingCategory.Building);
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
            static bool HasntBeenDesignatedYet(Designator_Mine dm, IntVec3 cell)
            {
                return dm.CanDesignateCell(cell).Accepted;
            }

            // Wether the Thing is a rock with mineable resources
            static bool IsOre(ThingDef def)
            {
                return def != null && def.building != null && def.building.isResourceRock;
            }

            // Wether the pawn can reach the cell (Rock) to the point of being able to touch it,
            // instead of standing on it
            static bool CanReach(Pawn pawn, IntVec3 cell)
            {
                return pawn.CanReach(cell, PathEndMode.Touch, Danger.None);
            }
        }
    }
}
