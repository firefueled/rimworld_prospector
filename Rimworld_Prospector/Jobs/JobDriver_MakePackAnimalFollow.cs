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
        
        private Pawn PackAnimal => (Pawn) job.GetTarget(PackAnimalTarget).Thing;
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetLocA, job, 1, -1, null, errorOnFailed);
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(PackAnimalTarget);
            this.FailOnDowned(PackAnimalTarget);
            this.FailOnNotCasualInterruptible(PackAnimalTarget);

            yield return Toils_Goto.GotoThing(PackAnimalTarget, PathEndMode.Touch);
            yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
            yield return Toils_Interpersonal.GotoInteractablePosition(PackAnimalTarget);

            var toil = new Toil
            {
                initAction = delegate
                {
                    pawn.interactions.TryInteractWith(PackAnimal, InteractionDefOf.AnimalChat);
                },
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = 180
            };
            yield return toil;

            yield return Toils_General.Do(() =>
            {
                PackAnimal.playerSettings.followFieldwork = true;
            });
            // TODO feed the animal?
        }

        public override string GetReport()
        {
            return "Making animal follow prospector.";
        }
        
        public static readonly JobDef DefOf = DefDatabase<JobDef>.GetNamed("Prospector_JobDriver_MakePackAnimalFollow");
    }
}