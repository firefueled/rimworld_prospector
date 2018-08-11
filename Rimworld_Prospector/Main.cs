﻿using System;
using Harmony;
using HugsLib;
using RimWorld;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using HugsLib.Utils;
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
        public static WorldDataStore dataStore;
        private const int MAX_GIVE_JOB_WAIT = 10000;

        public override string ModIdentifier => "com.firefueled.rimworld_prospector";

        public override void WorldLoaded()
        {
            dataStore = UtilityWorldObjectManager.GetUtilityWorldObject<WorldDataStore>();
        }

        private static void Postfix(Thing __instance, Pawn pawn)
        {
            prospector = pawn;

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
                AddMinedOreAt(__instance);
                Log.Message("packMule: " + packMule.Name);
                StoreOreInPackMule();
            }
        }

        /**
         * Store the ore mined so far for later reference
         */
        private static void AddMinedOreAt(Thing thing)
        {
            Thing t = prospector.Map.thingGrid.ThingAt(thing.Position, ThingCategory.Item);
            if (t.def == ThingDefOf.Steel ||
                t.def == ThingDefOf.Component ||
                t.def == ThingDefOf.Gold ||
                t.def == ThingDefOf.Plasteel ||
                t.def == ThingDefOf.Silver ||
                t.def == ThingDefOf.Uranium)
            {
//                Log.Message("thingmined: " + t);
                dataStore.MinedOre.Add(t);
            }
        }

        /**
         * Designate rock ore cells around the player for mining
         */
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

        /**
         * Fully pack the mule with mined ore and send it on it's way to the dumping spot
         */
        private static void StoreOreInPackMule()
        {
            if (!HasEnoughOreToPack(out var oreToPack)) return;

//            Log.Message("oreToPack " + oreToPack.Count);
            Job giveJob = null;
            foreach (Thing ore in oreToPack)
            {
//                Log.Message("ore " + ore);
//                Log.Message("ore.stack " + ore.stackCount);
                giveJob = new Job(JobDriver_GiveToPackAnimalDone.DefOf, ore, packMule)
                {
                    count = 35
                };
                prospector.jobs.jobQueue.EnqueueLast(giveJob);
            }
            
            var sendPackAnimalWorker = new BackgroundWorker();
            sendPackAnimalWorker.DoWork += WaitAndSendPackAnimal;
            
            // only wait for the last job
            dataStore.GiveJobDoneTracker.AddJob(prospector, giveJob);
            sendPackAnimalWorker.RunWorkerAsync(giveJob);
        } 

        /**
         * Decide wether there are enough mined ore around to fully pack the mule
         * and return the list of ore to be packed
         */
        private static bool HasEnoughOreToPack(out List<Thing> oreToPack)
        {
            oreToPack = new List<Thing>();
//            Log.Message("dataStore.minedOre.Count " + dataStore.MinedOre.Count);

            if (dataStore.MinedOre.Count == 0)
            {
                return false;
            }
            
            var max = MassUtility.CountToPickUpUntilOverEncumbered(packMule, dataStore.MinedOre.First());
//            Log.Message("max: " + max);
            var toPackCount = 0;
            var oreChecked = 0;
            
            foreach (Thing ore in dataStore.MinedOre)
            {
                oreChecked++;
//                Log.Message("ore> " + ore);
                if (ore.def != dataStore.MinedOre.First().def)
                {
                    continue;
                }

                var toPackDiff = max - toPackCount;
                if (ore.stackCount <= toPackDiff)
                {
                    oreToPack.Add(ore);
                    toPackCount += ore.stackCount;
                }
                
                if (toPackCount == max)
                {
                    break;
                }
            }
            
//            Log.Message("toPackCount " + toPackCount);
//            Log.Message("oreChecked " + oreChecked);

            return toPackCount == max || oreChecked == dataStore.MinedOre.Count - 1;
        }

        /**
         * Wait for the last GiveToPackAnimal Job to end before issuing the dump Job to the mule
         */
        private static void WaitAndSendPackAnimal(object sender, DoWorkEventArgs e)
        {
            DateTime starTime = DateTime.Now;
            var job = (Job) e.Argument;
            var isGiveJobDone = dataStore.GiveJobDoneTracker.IsDone(prospector, job);
            Log.Message("isGiveJobDone " + isGiveJobDone);
            while (!isGiveJobDone && (DateTime.Now - starTime).Milliseconds < MAX_GIVE_JOB_WAIT)
            {
                Thread.Sleep(250);
                isGiveJobDone = dataStore.GiveJobDoneTracker.IsDone(prospector, job);
                Log.Message("isGiveJobDone " + isGiveJobDone);
            }

            dataStore.GiveJobDoneTracker.RemoveJob(prospector, job);
            
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
    }
}
