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

    [HarmonyPatch(typeof(JobDriver_Mine), "MakeNewToils")]
    public class MineJobToils
    {
        private static MapData MapData;
        private static Pawn prospector;
        private static Pawn packMule;

        private static void Postfix(JobDriver_Mine __instance, ref IEnumerable<Toil> __result)
        {
            prospector = __instance.pawn;
            MapData = prospector.Map.GetComponent<MapData>();

            
            if (MapData.PawnPackAnimalTracker.ContainsKey(prospector.ThingID))
            {
                packMule = MapData.PawnPackAnimalTracker[prospector.ThingID];
            }
            else
            {
                if (Utils.FindAvailablePackAnimal(prospector))
                {
                    packMule = MapData.PawnPackAnimalTracker[prospector.ThingID];
                }
            }

            if (packMule == null)
                return;
            
            var isAnimalFollowing = false;

            // why, oh why?
            if (MapData.PawnPackAnimalFollowing == null)
                MapData.PawnPackAnimalFollowing = new Dictionary<string, bool>();
            
            if (MapData.PawnPackAnimalFollowing.ContainsKey(prospector.ThingID + packMule.ThingID))
                isAnimalFollowing = MapData.PawnPackAnimalFollowing[prospector.ThingID + packMule.ThingID];

            // only allow mining if the pack animal has been told to follow
            if (isAnimalFollowing)
                return;

            Log.Message("Making pack mule follow Prospector " + prospector);
            
            // interact with the animal and set follow field work before executing job
            __instance.job.targetC = packMule;
            __instance.FailOnDespawnedNullOrForbidden(TargetIndex.C);
            __instance.FailOnDowned(TargetIndex.C);
            __instance.FailOnNotCasualInterruptible(TargetIndex.C);

            var followToils = SetFollowToils().ToList();
            var toils = __result.ToList();

            for (var i = followToils.Count - 1; i >= 0; i--)
                toils.Insert(0, followToils[i]);

            __result = toils.AsEnumerable();
        }

        private static IEnumerable<Toil> SetFollowToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch);
            yield return Toils_Interpersonal.SetLastInteractTime(TargetIndex.C);
            yield return Toils_Interpersonal.WaitToBeAbleToInteract(prospector);
            yield return Toils_Interpersonal.GotoInteractablePosition(TargetIndex.C);
            yield return TalkToAnimalToil(TargetIndex.C);
            yield return Toils_General.Do(() =>
            {
                packMule.playerSettings.followFieldwork = true;
                MapData.PawnPackAnimalFollowing[prospector.ThingID + packMule.ThingID] = true;
            });
            // TODO feed the animal?
        }

        private static Toil TalkToAnimalToil(TargetIndex index)
        {
            var toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.GetActor();
                var recipient = (Pawn)(Thing)actor.CurJob.GetTarget(index);
                actor.interactions.TryInteractWith(recipient, InteractionDefOf.AnimalChat);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 90;
            return toil;
        }
    }
}