using System;
using System.Collections.Generic;
using System.Diagnostics;
using Verse;
using Verse.AI;
using RimWorld;

namespace Rimworld_Prospector
{
    public class JobDriver_SendPackAnimalHome : JobDriver
    {
        public override void ExposeData()
        {
            base.ExposeData();
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

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Log.Message("Returning Toils");
            IntVec3 dropCell = new IntVec3(70, 0, 84);

            yield return Toils_Goto.GotoCell(dropCell, PathEndMode.OnCell);
            yield return Toils_General.Do(() =>
            {
                Log.Message("After Go to");
                this.pawn.inventory.innerContainer.TryDropAll(dropCell, base.Map, ThingPlaceMode.Near);
            });
            yield break;
        }

        public static JobDef DefOf = DefDatabase<JobDef>.GetNamed("Prospector_JobDriver_SendPackAnimalHome");
    }
}
