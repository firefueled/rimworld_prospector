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
                MapData.Designations = new HashSet<IntVec3>();

            MapData.Designations.Remove(__instance.Position);

            Utils.DeisgnateCellsAround(prospector);
            Utils.AddMinedOreAt(__instance, prospector.Map);

            isProspectionSiteDone = MapData.Designations.Count == 0;
            
            if (MapData.PawnPackAnimalTracker.ContainsKey(prospector.ThingID))
            {
                packMule = MapData.PawnPackAnimalTracker[prospector.ThingID];
            }
            
            Log.Message("has pack mule? " + packMule);
            if (packMule == null) return;

            // Do nothing if the only available pack mule is hauling stuff
            if (packMule.CurJob.def == JobDriver_SendPackAnimalHome.DefOf) return;

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
                Log.Message("Leaving the prospection site");
            
            if (!Utils.MaybeListOreToPack(out var oreToPack, packMule, isProspectionSiteDone)) return;
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

            Log.Message("Making pack mule follow prospector");
            
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
            // why, oh why?
            yield return Toils_General.Do(() => Toils_Interpersonal.WaitToBeAbleToInteract(packMule));
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
            toil.defaultDuration = 270;
            return toil;
        }
    }
}