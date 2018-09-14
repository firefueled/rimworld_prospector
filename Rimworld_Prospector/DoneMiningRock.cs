using System;
using System.ComponentModel;
using System.Threading;
using Harmony;
using HugsLib;
using HugsLib.Utils;
using Rimworld_Prospector.Jobs;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    // A mining operation has ended
    // ReSharper disable once ClassNeverInstantiated.Global
    [HarmonyPatch(typeof(Mineable), "DestroyMined")]
    internal class DoneMiningRock : ModBase
    {
        private static Pawn packMule;
        private static Pawn prospector;
        public static MapData MapData;
        public static ModLogger Log;
        private static Building dumpSpot;
        private const int MaxGiveJobWait = 30000;

        public override string ModIdentifier => "com.firefueled.rimworld_prospector";

        public override void WorldLoaded()
        {
            Log = new ModLogger("Prospector");
        }

        // ReSharper disable once InconsistentNaming
        private static void Postfix(Thing __instance, Pawn pawn)
        {
            prospector = pawn;
            MapData = prospector.Map.GetComponent<MapData>();
//            prospector.jobs.debugLog = true;
            
            DeisgnateCellsAround();
            Utils.AddMinedOreAt(__instance, prospector.Map);

            if (!Utils.FindAvailablePackAnimal(prospector)) return;
            
            packMule = MapData.PawnPackAnimalTracker[prospector.ThingID];

            // Do nothing if the only available pack mule is hauling stuff
            if (packMule.CurJob.def == JobDriver_SendPackAnimalHome.DefOf) return;
            
            // Can't pack the animal without having a spot to dump stuff onto
            dumpSpot = Utils.FindClosestDumpSpot(packMule) as Building;
            if (dumpSpot == null)
            {
                // TODO pop a learning helper
                return;
            }

            StoreOreInPackMule();
        }

        /**
         * Designate rock ore cells around the player for mining
         */
        private static void DeisgnateCellsAround()
        {
            // A cell grid around the mining pawn covering an area two cells away from it
            var cellsAround = GenAdj.CellsOccupiedBy(prospector.Position, prospector.Rotation, new IntVec2(5, 5));
            var dm = new Designator_Mine();
            
            foreach (IntVec3 cell in cellsAround)
            {
                // Find out what Thing is on the cell
                Thing thing = prospector.Map.thingGrid.ThingAt(cell, ThingCategory.Building);
                if (
                    thing != null && 
                    Utils.IsResourceRock(thing.def) && 
                    Utils.HasntBeenDesignatedYet(dm, cell) && 
                    Utils.CanReach(prospector, cell)
                )
                {
                    // "Order" the cell to be mined
                    dm.DesignateSingleCell(cell);
                }
            }
        }

        /**
         * Fully pack the mule with mined ore and send it on it's way to the dumping spot
         */
        private static void StoreOreInPackMule()
        {
            if (!Utils.MaybeListOreToPack(out var oreToPack, packMule)) return;
            Log.Message("oreToPack " + oreToPack.Count);

            Job giveJob = null;
            foreach (Utils.PackableOre pOre in oreToPack)
            {
                giveJob = new Job(JobDriver_GiveToPackAnimalDone.DefOf, pOre.Ore, packMule)
                {
                    count = pOre.StackCount
                };
                prospector.jobs.jobQueue.EnqueueLast(giveJob);
            }
            
            var sendPackAnimalWorker = new BackgroundWorker();
            sendPackAnimalWorker.DoWork += WaitAndSendPackAnimal;
            
            // only wait for the last job
            Log.Message("prostector " + prospector + " job: " + giveJob);
            MapData.GiveJobDoneTracker.Add(prospector.ThingID + giveJob?.loadID, false);
            sendPackAnimalWorker.RunWorkerAsync(giveJob);
        } 

        /**
         * Wait for the last GiveToPackAnimal Job to end before issuing the dump Job to the mule
         */
        private static void WaitAndSendPackAnimal(object sender, DoWorkEventArgs e)
        {
            DateTime starTime = DateTime.Now;
            var job = (Job) e.Argument;
            var trackerKey = prospector.ThingID + job.loadID;
            var isGiveJobDone = MapData.GiveJobDoneTracker[trackerKey];
            
            Log.Message("isGiveJobDone " + isGiveJobDone);
            while (!isGiveJobDone && (DateTime.Now - starTime).Milliseconds < MaxGiveJobWait)
            {
                Thread.Sleep(250);
                isGiveJobDone = MapData.GiveJobDoneTracker[trackerKey];
                Log.Message("isGiveJobDone " + isGiveJobDone);
            }

            MapData.GiveJobDoneTracker.Remove(trackerKey);
            
            if (!isGiveJobDone) return;
            Log.Message("jobdonemaybe");
            
            var packJob = new Job(JobDriver_SendPackAnimalHome.DefOf, dumpSpot);
            packMule.jobs.jobQueue.EnqueueFirst(packJob);
        }
    }
}