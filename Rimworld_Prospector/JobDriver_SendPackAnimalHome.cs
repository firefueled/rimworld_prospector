using System;
using System.Collections.Generic;
using System.Diagnostics;
using Verse;
using Verse.AI;
using RimWorld;

namespace Rimworld_Prospector
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Global
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

            var dropCell = new IntVec3(70, 0, 84);
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

        public override bool TryMakePreToilReservations()
        {
            Log.Message("TryMakePreToilReservations");
            return true;
        }

        public static readonly JobDef DefOf = DefDatabase<JobDef>.GetNamed("Prospector_JobDriver_SendPackAnimalHome");
    }
}
