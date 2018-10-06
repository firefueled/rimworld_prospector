using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using Harmony;
using Rimworld_Prospector;
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
        public static MapData MapData;
        private static Building dumpSpot;
        private const int MaxGiveJobWait = 30000;

        // ReSharper disable once InconsistentNaming
        public static void Postfix(Thing __instance, Pawn pawn)
        {
            prospector = pawn;
            MapData = prospector.Map.GetComponent<MapData>();

            Utils.DeisgnateCellsAround(prospector);
            Utils.AddMinedOreAt(__instance, prospector.Map);

            if (MapData.PawnPackAnimalTracker.ContainsKey(prospector.ThingID))
            {
                packMule = MapData.PawnPackAnimalTracker[prospector.ThingID];
            }
            
            Log.Message("has pack mule? " + packMule);
            if (packMule == null) return;

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

    [HarmonyPatch(typeof(JobDriver_Mine), "MakeNewToils")]
    public class JobOnThing
    {
        private static MapData MapData;
        private static Pawn prospector;
        private static Job ogJob;

        private static void Postfix(JobDriver_Mine __instance, ref IEnumerable<Toil> __result)
        {
            Log.Message("JobDriver_Mine.MakeNewToils");

            Pawn pawn = __instance.pawn;
            MapData = pawn.Map.GetComponent<MapData>();

//            Log.Message("pawn.Name " + pawn.Name);
            Pawn packMule = null;
            if (MapData.PawnPackAnimalTracker.ContainsKey(pawn.ThingID))
            {
                packMule = MapData.PawnPackAnimalTracker[pawn.ThingID];
            }
            else
            {
                if (Utils.FindAvailablePackAnimal(pawn))
                {
                    packMule = MapData.PawnPackAnimalTracker[pawn.ThingID];
                }
            }

            Log.Message("packMule " + packMule);
            if (packMule == null)
                return;
            
            var isAnimalFollowing = false;

            // why, oh why?
            if (MapData.PawnPackAnimalFollowing == null)
                MapData.PawnPackAnimalFollowing = new Dictionary<string, bool>();
            
            Log.Message("MapData.PawnPackAnimalFollowing " + MapData.PawnPackAnimalFollowing);
            if (MapData.PawnPackAnimalFollowing.ContainsKey(pawn.ThingID + packMule.ThingID))
                isAnimalFollowing = MapData.PawnPackAnimalFollowing[pawn.ThingID + packMule.ThingID];

            Log.Message("isAnimalFollowing " + isAnimalFollowing);
            
            // only allow mining if the pack animal has been told to follow
            if (isAnimalFollowing)
                return;

            __instance.job.targetC = packMule.Position;

            // TODO make the pawn follow the animal until reaching it            
            Toil t1 = Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch);
            // TODO make the pawn 'interact' with the animal            
            Toil t2 = Toils_General.Do(() =>
            {
                packMule.playerSettings.followFieldwork = true;
                MapData.PawnPackAnimalFollowing[pawn.ThingID + packMule.ThingID] = true;
            });

            var toils = __result.ToList();
            toils.Insert(0, t2);
            toils.Insert(0, t1);
            __result = toils.AsEnumerable();
        }
    }
}