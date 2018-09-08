using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Harmony;
using HugsLib;
using HugsLib.Utils;
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
            
            DeisgnateCellsAround();
            AddMinedOreAt(__instance);

            if (!FindAvailablePackAnimal()) return;

            // Do nothing if the only available pack mule is hauling stuff
            packMule = MapData.PawnPackAnimalTracker[prospector.ThingID];

            Log.Message("packMule: " + packMule.Name);
            Log.Message("curjob: " + packMule.CurJob);
            if (packMule.CurJob.def == JobDriver_SendPackAnimalHome.DefOf) return;
            
            StoreOreInPackMule();
        }

        private static bool FindAvailablePackAnimal()
        {
            if (MapData.PawnPackAnimalTracker.ContainsKey(prospector.ThingID))
            {
                Pawn existingPackAnimal = MapData.PawnPackAnimalTracker[prospector.ThingID];
                if (IsPackAnimalNear(existingPackAnimal)) return true;

                Log.Message("Existing pack animal " + existingPackAnimal + " is too far");
                return false;
            }

            // Find a pack animal that's close by, if it exists
            foreach (Pawn p in prospector.Map.mapPawns.PawnsInFaction(prospector.Faction))
            {               
                if (p.playerSettings.Master != prospector) continue;

                if (!IsPackAnimalNear(p))
                {
                    Log.Message("Found pack animal " + p + " but it's too far");
                    continue;
                }

                MapData.PawnPackAnimalTracker.Add(prospector.ThingID, p);
                return true;
            }

            return false;
        }

        private static bool IsPackAnimalNear(Thing p)
        {
            PawnPath path = prospector.Map.pathFinder.FindPath(
                prospector.Position, p.Position, prospector, PathEndMode.ClosestTouch
            );
            return path.TotalCost <= 200;
        }

        /**
         * Store the ore mined so far for later reference
         */
        private static void AddMinedOreAt(Thing thing)
        {
            Thing t = prospector.Map.thingGrid.ThingAt(thing.Position, ThingCategory.Item);
            if (t.def == ThingDefOf.Steel ||
                t.def == ThingDefOf.ComponentIndustrial ||
                t.def == ThingDefOf.ComponentSpacer ||
                t.def == ThingDefOf.Gold ||
                t.def == ThingDefOf.Plasteel ||
                t.def == ThingDefOf.Silver ||
                t.def == ThingDefOf.Uranium)
            {
                MapData.MinedOre.Add(t);
            }
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
                    IsResourceRock(thing.def) &&
                    HasntBeenDesignatedYet(dm, cell) &&
                    CanReach(prospector, cell)
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
            if (!MaybeListOreToPack(out var oreToPack)) return;
            Log.Message("oreToPack " + oreToPack);

            Job giveJob = null;
            foreach (PackableOre pOre in oreToPack)
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
            MapData.GiveJobDoneTracker.AddJob(prospector, giveJob);
            sendPackAnimalWorker.RunWorkerAsync(giveJob);
        } 

        /**
         * Decide wether there are enough mined ore around to fully pack the mule
         * and return the list of ore to be packed
         */
        private static bool MaybeListOreToPack(out List<PackableOre> oreToPack)
        {
            oreToPack = new List<PackableOre>();

            if (MapData.MinedOre.Count == 0)
            {
                return false;
            }
            
            var max = MassUtility.CountToPickUpUntilOverEncumbered(packMule, MapData.MinedOre.First()) - 1;
            var toPackCount = 0;
            Log.Message("max " + max);
            
            foreach (Thing ore in MapData.MinedOre)
            {
                if (ore.def != MapData.MinedOre.First().def)
                {
                    continue;
                }

                var toPackDiff = max - toPackCount;
                Log.Message("toPackDiff " + toPackDiff);
                if (ore.stackCount <= toPackDiff)
                {
                    oreToPack.Add(new PackableOre(ore, ore.stackCount));
                    toPackCount += ore.stackCount;
                }
                else if (toPackDiff >= 1)
                {
                    oreToPack.Add(new PackableOre(ore, toPackDiff));
                    toPackCount += toPackDiff;
                }
                
                if (toPackCount >= max)
                {
                    break;
                }
            }
            
            Log.Message("toPackCount >= max " + (toPackCount >= max));
            return toPackCount >= max;
        }

        /**
         * Wait for the last GiveToPackAnimal Job to end before issuing the dump Job to the mule
         */
        private static void WaitAndSendPackAnimal(object sender, DoWorkEventArgs e)
        {
            DateTime starTime = DateTime.Now;
            var job = (Job) e.Argument;
            var isGiveJobDone = MapData.GiveJobDoneTracker.IsDone(prospector, job);
            Log.Message("isGiveJobDone " + isGiveJobDone);
            while (!isGiveJobDone && (DateTime.Now - starTime).Milliseconds < MaxGiveJobWait)
            {
                Thread.Sleep(250);
                isGiveJobDone = MapData.GiveJobDoneTracker.IsDone(prospector, job);
                Log.Message("isGiveJobDone " + isGiveJobDone);
            }

            MapData.GiveJobDoneTracker.RemoveJob(prospector, job);
            
            if (!isGiveJobDone) return;
            Log.Message("jobdonemaybe");

            var packJob = new Job(JobDriver_SendPackAnimalHome.DefOf);
            packMule.jobs.jobQueue.EnqueueFirst(packJob);
        }

        /**
         * Wether a mine "Order" hasn't been placed on the cell yet
         */
        private static bool HasntBeenDesignatedYet(Designator dm, IntVec3 cell)
        {
            return dm.CanDesignateCell(cell).Accepted;
        }

        /**
         * Wether the Thing is a rock with mineable resources
         */
        private static bool IsResourceRock(ThingDef def)
        {
            return def?.building != null && def.building.isResourceRock;
        }

        /**
         * Wether the pawn can reach the cell (Rock) to the point of being able to touch it,
         * instead of standing on it
         */
        private static bool CanReach(Pawn pawn, IntVec3 cell)
        {
            return pawn.CanReach(cell, PathEndMode.Touch, Danger.None);
        }
        
        private class PackableOre
        {
            public Thing Ore { get; }
            public int StackCount { get; }

            public PackableOre(Thing ore, int stackCount)
            {
                Ore = ore;
                StackCount = stackCount;
            }
        }

    }
}