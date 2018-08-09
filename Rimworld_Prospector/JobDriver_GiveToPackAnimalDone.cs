using System;
using System.Collections.Generic;
using System.Diagnostics;
using HugsLib.Utils;
using Verse;
using Verse.AI;
using RimWorld;

namespace Rimworld_Prospector
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Global
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
                DoneMiningRock.dataStore.GiveJobDoneTracker.SetDone(pawn, job);
            });
        }
    }
}
