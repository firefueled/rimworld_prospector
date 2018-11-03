using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector.Jobs
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Global
    /**
     * Sends a pack animal to a pre-determined spot and have it dump everything it's carrying there
     */
    public class JobDriver_MakePackAnimalFollow : JobDriver
    {
        
        private static TargetIndex PackAnimalTarget => TargetIndex.B;
        
        private Pawn Prospector => (Pawn) job.GetTarget(TargetIndex.A).Thing;
        
        private Pawn PackAnimal => (Pawn) job.GetTarget(PackAnimalTarget).Thing;
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetLocA, job, 1, -1, null, errorOnFailed);
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            var mapData = Prospector.Map.GetComponent<MapData>();

            this.FailOnDespawnedNullOrForbidden(PackAnimalTarget);
            this.FailOnDowned(PackAnimalTarget);
            this.FailOnNotCasualInterruptible(PackAnimalTarget);
            
            yield return Toils_Goto.GotoThing(PackAnimalTarget, PathEndMode.Touch);
            yield return Toils_Interpersonal.SetLastInteractTime(PackAnimalTarget);
            yield return Toils_Interpersonal.WaitToBeAbleToInteract(Prospector);
            yield return Toils_Interpersonal.GotoInteractablePosition(PackAnimalTarget);
            var toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.GetActor();
                var recipient = (Pawn)(Thing)actor.CurJob.GetTarget(PackAnimalTarget);
                actor.interactions.TryInteractWith(recipient, InteractionDefOf.AnimalChat);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 90;
            yield return toil;
            yield return Toils_General.Do(() =>
            {
                PackAnimal.playerSettings.followFieldwork = true;
                mapData.PawnPackAnimalFollowing[Prospector.ThingID + PackAnimal.ThingID] = true;
            });
            // TODO feed the animal?
        }

        public override string GetReport()
        {
            return "Making animal follow prospector";
        }

        public static readonly JobDef DefOf = DefDatabase<JobDef>.GetNamed("Prospector_JobDriver_MakePackAnimalFollow");
    }
}