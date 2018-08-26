using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
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
            foreach (Toil toil in base.MakeNewToils())
            {
                yield return toil;
            }
            
            yield return Toils_General.Do(() =>
            {
                DoneMiningRock.MapData.MinedOre.Remove(TargetThingA);
                DoneMiningRock.MapData.GiveJobDoneTracker.SetDone(pawn, job);
            });
        }
    }
}
