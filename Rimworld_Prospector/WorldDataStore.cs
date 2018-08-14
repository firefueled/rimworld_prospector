using System.Collections.Generic;
using HugsLib.Utils;
using Verse;

namespace Rimworld_Prospector
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class WorldDataStore : UtilityWorldObject
    {
        /**
         * The helper for tracking Jobs' doneness
         */
        public GiveJobDoneTracker GiveJobDoneTracker;
        /**
         * The list of mined ore 
         */
        public List<Thing> MinedOre;

        public override void SpawnSetup()
        {
            base.SpawnSetup();
            MinedOre = new List<Thing>();
            GiveJobDoneTracker = new GiveJobDoneTracker();
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref GiveJobDoneTracker, "GiveJobDoneTracker");
            Scribe_Values.Look(ref MinedOre, "MinedOre");
        }
    }
}