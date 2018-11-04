using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using Harmony;
using Rimworld_Prospector.Jobs;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    // A mining operation has ended
    // ReSharper disable once ClassNeverInstantiated.Global
    [StaticConstructorOnStartup]
    internal static class MiningPatch
    {
        static MiningPatch()
        {
            HarmonyInstance harmony = HarmonyInstance.Create("com.firefueled.rimworld_prospector");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    // ReSharper disable once InconsistentNaming
    [HarmonyPatch(typeof(Mineable), "DestroyMined")]
    internal static class DoneMiningRock
    {
        private static Pawn packMule;
        private static Pawn prospector;
        private static MapData MapData;
        private static Building dumpSpot;
        
        /**
         * Will cause the prospector to pack the mule one last time before leaving the site
         */
        private static bool isProspectionSiteDone;
        private const int MaxGiveJobWait = 30000;

        // ReSharper disable once InconsistentNaming
        public static void Postfix(Thing __instance, Pawn pawn)
        {
            prospector = pawn;
            MapData = prospector.Map.GetComponent<MapData>();

            // TODO figure out why this is needed
            if (MapData.Designations == null)
                MapData.Designations = new List<IntVec3>();
            
            MapData.Designations.Remove(__instance.Position);

            Utils.DesignateCellsAround(prospector);
            Utils.AddMinedOreAt(__instance, prospector.Map);
            
            isProspectionSiteDone = MapData.Designations.Count == 0;

            if (MapData.PawnPackAnimalTracker.ContainsKey(prospector.ThingID))
                packMule = MapData.PawnPackAnimalTracker[prospector.ThingID];

            if (!Utils.HasAvailablePackMule(packMule))
                return;
            
            Log.Message("has pack mule? " + packMule);

            // Can't pack the animal without having a spot to dump stuff onto
            dumpSpot = Utils.FindClosestDumpSpot(packMule);
            
            // TODO pop a learning helper
            if (dumpSpot == null)
                return;

            StoreOreInPackMule();
        }

        /**
         * Fully pack the mule with mined ore and send it on it's way to the dumping spot
         */
        private static void StoreOreInPackMule()
        {
            if (isProspectionSiteDone)
                Log.Message("Prospection site is done");

            var maybeListOreToPack = Utils.MaybeListOreToPack(out var oreToPack, packMule, isProspectionSiteDone);
            
            if (!maybeListOreToPack) return;
            
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
            Log.Message("Prospector " + prospector + " is packing the mule");
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

            while (!isGiveJobDone && (DateTime.Now - starTime).Milliseconds < MaxGiveJobWait)
            {
                Thread.Sleep(250);
                isGiveJobDone = MapData.GiveJobDoneTracker[trackerKey];
            }

            MapData.GiveJobDoneTracker.Remove(trackerKey);

            if (!isGiveJobDone) return;

            Log.Message("Prospector " + prospector + " is done packing the mule");
            var packJob = new Job(JobDriver_SendPackAnimalHome.DefOf, dumpSpot);
            packMule.jobs.jobQueue.EnqueueFirst(packJob);
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Miner), "JobOnThing")]
    public class JobOnThing
    {
        private static Pawn PackMule;
        private static MapData MapData;

        private static void Postfix(Pawn pawn, Thing t, bool forced, ref Job __result)
        {
            MapData = pawn.Map.GetComponent<MapData>();

            if (MapData.PawnPackAnimalTracker.ContainsKey(pawn.ThingID))
            {
                PackMule = MapData.PawnPackAnimalTracker[pawn.ThingID];
            }
            else
            {
                if (Utils.FindAvailablePackAnimal(pawn))
                {
                    PackMule = MapData.PawnPackAnimalTracker[pawn.ThingID];
                }
            }

            if (PackMule == null)
                return;
            
            var isAnimalFollowing = false;

            // why, oh why?
            if (MapData.PawnPackAnimalFollowing == null)
                MapData.PawnPackAnimalFollowing = new Dictionary<string, bool>();
            
            if (MapData.PawnPackAnimalFollowing.ContainsKey(pawn.ThingID + PackMule.ThingID))
                isAnimalFollowing = MapData.PawnPackAnimalFollowing[pawn.ThingID + PackMule.ThingID];

            // only allow mining if the pack animal has been told to follow
            if (isAnimalFollowing)
                return;

            Log.Message("Make animal follow");
            __result = new Job(JobDriver_MakePackAnimalFollow.DefOf, pawn, PackMule);
        }
    }
    
    [HarmonyPatch(typeof(WorkGiver_Miner), "HasJobOnThing")]
    public class HasJobOnThing
    {
        private static bool Prefix(Pawn pawn, Thing t, bool forced, ref bool __result)
        {
            __result = false;
            if (pawn.Map.designationManager.DesignationAt(t.Position, DesignationDefOf.Mine) == null) return false;
            
            var mayBeAccessible = false;
            for (var j = 0; j < 8; j++)
            {
                IntVec3 c = t.Position + GenAdj.AdjacentCells[j];
                if (!c.InBounds(pawn.Map) || !c.Walkable(pawn.Map)) continue;
                mayBeAccessible = true;
                break;
            }

            if (!mayBeAccessible) return false;
            __result = true;
            return false;
        }
    }
}