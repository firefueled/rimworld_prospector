using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Global
    /**
     * Sends a pack animal to a pre-determined spot and have it dump everything it's carrying there
     */
    public class JobDriver_SendPackAnimalHome : JobDriver
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            Log.Message("Returning Toils");

            Thing makeThing = ThingMaker.MakeThing(ThingDefOf.Steel);
            Log.Message("FreeSpace: " + MassUtility.FreeSpace(pawn));
            Log.Message("EncumbrancePercent: " + MassUtility.EncumbrancePercent(pawn));
            Log.Message("IsOverEncumbered: " + MassUtility.IsOverEncumbered(pawn));
            Log.Message("WillBeOverEncumberedAfterPickingUp: " + MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, makeThing, 1));
            Log.Message("CountToPickUpUntilOverEncumbered: " + MassUtility.CountToPickUpUntilOverEncumbered(pawn, makeThing));

            var isPackMuleFull = MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, makeThing, 1);
            if (!isPackMuleFull)
            {
                EndJobWith(JobCondition.Succeeded);
                yield break;
            }

            var dropCell = new IntVec3(136, 0, 84);
            yield return Toils_Goto.GotoCell(dropCell, PathEndMode.OnCell);
            yield return Toils_General.Do(() =>
            {
                pawn.inventory.DropAllNearPawn(dropCell);
                EndJobWith(JobCondition.Succeeded);
            });
        }

        public override string GetReport()
        {
            Log.Message("Getting Report");
            return "Someone is loading a pack animal and making it go somewhere";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Log.Message("TryMakePreToilReservations");
            return true;
        }

        public static readonly JobDef DefOf = DefDatabase<JobDef>.GetNamed("Prospector_JobDriver_SendPackAnimalHome");
    }
}
