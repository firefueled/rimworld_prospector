using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector.Jobs
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Global
    /**
     * Does the same as the original and then tells a tracker that the Job has ended
     */
    public class JobDriver_GiveToPackAnimalDone : JobDriver_GiveToPackAnimal
    {
        public static readonly JobDef DefOf = DefDatabase<JobDef>.GetNamed("Prospector_JobDriver_GiveToPackAnimalDone");

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return GiveToCarrierAsMuchAsPossibleToil();

            yield return Toils_General.Do(() =>
            {
                DoneMiningRock.MapData.MinedOre.Remove(TargetThingA);
                DoneMiningRock.MapData.GiveJobDoneTracker[pawn.ThingID + job.loadID] = true;
            });
        }

        private Toil GiveToCarrierAsMuchAsPossibleToil()
        {
            return new Toil
            {
                initAction = delegate
                {
                    if (TargetThingA == null)
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    }
                    else
                    {
                        var count = Math.Min(
                            MassUtility.CountToPickUpUntilOverEncumbered((Pawn) TargetThingB, TargetThingA),
                            TargetThingA.stackCount);
                        pawn.carryTracker.innerContainer.TryTransferToContainer(TargetThingA,
                            ((Pawn) TargetThingB).inventory.innerContainer, count);
                    }
                }
            };
        }
    }
}