using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector.Jobs
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Global
    /**
     * Sends a pack animal to a pre-determined spot and have it dump everything it's carrying there
     */
    public class JobDriver_SendPackAnimalHome : JobDriver
    {

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetLocA, job, 1, -1, null, errorOnFailed);
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);

            yield return Toils_Goto.GotoCell(TargetLocA, PathEndMode.OnCell);
            yield return Toils_General.Do(() =>
            {
                pawn.inventory.DropAllNearPawn(TargetLocA);
                EndJobWith(JobCondition.Succeeded);
            });
        }

        public override string GetReport()
        {
            Log.Message("Getting Report");
            return "Dumping mined stuff donto the dump site";
        }

        public static readonly JobDef DefOf = DefDatabase<JobDef>.GetNamed("Prospector_JobDriver_SendPackAnimalHome");
    }
}