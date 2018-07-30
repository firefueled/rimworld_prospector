using Harmony;
using HugsLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    // A mining operation has ended
    [HarmonyPatch(typeof(Mineable), "DestroyMined")]
    class DoneMiningRock : ModBase
    {
        private static bool hasPackMule;
        private static Pawn packMule;
        private static Pawn prospector;
        private static List<LocalTargetInfo> minedOre;

        public override string ModIdentifier => "com.firefueled.rimworld_prospector";

        static void Postfix(Building __instance, Pawn pawn)
        {
            prospector = pawn;
            minedOre = new List<LocalTargetInfo>();

            DeisgnateCellsAround(pawn);

            // Find the pack mule if it exists            
            foreach (var p in pawn.Map.mapPawns.PawnsInFaction(pawn.Faction))
            {
                if (p.playerSettings.master == pawn)
                {
                    hasPackMule = true;
                    packMule = p;
                }
            }

            if (hasPackMule)
            {
                AddMinedOreAt(ref minedOre, __instance.Position);
                Log.Message("packMule: " + packMule.Name);
            }
            
            if (hasPackMule)
            {
                StoreOreInPackMule();
            }
        }

        private static void AddMinedOreAt(ref List<LocalTargetInfo> minedOre, IntVec3 position)
        {
            var thing = prospector.Map.thingGrid.ThingAt(position, ThingCategory.Item);
            if (thing.def == ThingDefOf.Steel ||
                thing.def == ThingDefOf.Component ||
                thing.def == ThingDefOf.Gold ||
                thing.def == ThingDefOf.Plasteel ||
                thing.def == ThingDefOf.Silver ||
                thing.def == ThingDefOf.Uranium)
            {
                minedOre.Add(thing);
            }
        }

        private static void StoreOreInPackMule()
        {
            prospector.jobs.debugLog = true;
            var job = new Job(JobDefOf.GiveToPackAnimal, minedOre.First(), new LocalTargetInfo(packMule))
            {
                count = 35
            };

            prospector.jobs.jobQueue.EnqueueFirst(job);

            if (IsPackMuleFull())
            {
                SendPackMuleHome();
            }
        }

        private static bool IsPackMuleAround()
        {
            return CanReach(prospector, packMule.Position);
        }

        private static void SendPackMuleHome()
        {
            Log.Message("SendPackMuleHome");
            var job1 = new Job(JobDefOf.Goto, minedOre.First(), new LocalTargetInfo(new IntVec3(90, 0, 90)));
            //var job2 = new Job(JobDefOf.DropEquipment, new LocalTargetInfo(packMule));
            packMule.jobs.jobQueue.EnqueueFirst(job1);
            //packMule.jobs.jobQueue.EnqueueLast(job2);
        }

        private static bool IsPackMuleFull()
        {
            return true;
        }

        private static void DeisgnateCellsAround(Pawn pawn)
        {
            // A cell grid around the mining pawn covering an area two cells away from it
            var cellsAround = GenAdj.CellsOccupiedBy(pawn.Position, pawn.Rotation, new IntVec2(5, 5));
            Designator_Mine dm = new Designator_Mine();

            foreach (var cell in cellsAround)
            {
                // Find out what Thing is on the cell
                var thing = pawn.Map.thingGrid.ThingAt(cell, ThingCategory.Building);
                if (
                        thing != null &&
                        IsResourceRock(thing.def) &&
                        HasntBeenDesignatedYet(dm, cell) &&
                        CanReach(pawn, cell)
                    )
                {
                    // "Order" the cell to be mined
                    dm.DesignateSingleCell(cell);
                }
            }
        }

        // Wether an mine "Order" hasn't been placed on the cell yet
        static bool HasntBeenDesignatedYet(Designator_Mine dm, IntVec3 cell)
        {
            return dm.CanDesignateCell(cell).Accepted;
        }

        // Wether the Thing is a rock with mineable resources
        static bool IsResourceRock(ThingDef def)
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
