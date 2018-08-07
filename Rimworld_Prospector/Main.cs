using System;
using Harmony;
using HugsLib;
using RimWorld;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using HugsLib.Utils;
using Rimworld_Prospector.Properties;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    // A mining operation has ended
    [HarmonyPatch(typeof(Mineable), "DestroyMined")]
    internal class DoneMiningRock : ModBase
    {
        private static bool hasPackMule;
        private static Pawn packMule;
        private static Pawn prospector;
        private static List<Thing> minedOre;
        public static WorldDataStore uwom;
        private const int MAX_GIVE_JOB_WAIT = 10000;

        public override string ModIdentifier => "com.firefueled.rimworld_prospector";

        public override void WorldLoaded()
        {
            uwom = UtilityWorldObjectManager.GetUtilityWorldObject<WorldDataStore>();
        }

        private static void Postfix(Building __instance, Pawn pawn)
        {
            prospector = pawn;
            minedOre = new List<Thing>();

            DeisgnateCellsAround(pawn);

            // Find the pack mule if it exists            
            foreach (Pawn p in pawn.Map.mapPawns.PawnsInFaction(pawn.Faction))
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
                StoreOreInPackMule();
            }
        }

        private static void AddMinedOreAt(ref List<Thing> minedOre, IntVec3 position)
        {
            Thing thing = prospector.Map.thingGrid.ThingAt(position, ThingCategory.Item);
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

        private static void DeisgnateCellsAround(Pawn pawn)
        {
            // A cell grid around the mining pawn covering an area two cells away from it
            var cellsAround = GenAdj.CellsOccupiedBy(pawn.Position, pawn.Rotation, new IntVec2(5, 5));
            var dm = new Designator_Mine();

            foreach (IntVec3 cell in cellsAround)
            {
                // Find out what Thing is on the cell
                Thing thing = pawn.Map.thingGrid.ThingAt(cell, ThingCategory.Building);
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

        private static void StoreOreInPackMule()
        {
            var giveJob = new Job(JobDriver_GiveToPackAnimalDone.DefOf, minedOre.Last(), packMule)
            {
                count = 35
            };
            prospector.jobs.jobQueue.EnqueueFirst(giveJob);
            
            var sendPackAnimalWorker = new BackgroundWorker();
            sendPackAnimalWorker.DoWork += WaitAndSendPackAnimal;
            sendPackAnimalWorker.RunWorkerAsync(giveJob);
        }

        private static void WaitAndSendPackAnimal(object sender, DoWorkEventArgs e)
        {
            DateTime starTime = DateTime.Now;
            while (!uwom.isGiveJobDone && (DateTime.Now - starTime).Milliseconds < MAX_GIVE_JOB_WAIT)
            {
                Thread.Sleep(250);
            }
            
            Log.Message("job done maybe");
            uwom.isGiveJobDone = false;
            var packJob = new Job(JobDriver_SendPackAnimalHome.DefOf);
            packMule.jobs.jobQueue.EnqueueFirst(packJob);
        }

        // Wether an mine "Order" hasn't been placed on the cell yet
        private static bool HasntBeenDesignatedYet(Designator dm, IntVec3 cell)
        {
            return dm.CanDesignateCell(cell).Accepted;
        }

        // Wether the Thing is a rock with mineable resources
        private static bool IsResourceRock(ThingDef def)
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
