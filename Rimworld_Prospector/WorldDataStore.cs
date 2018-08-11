using System.Collections.Generic;
using HugsLib.Utils;
using Verse;

namespace Rimworld_Prospector
{
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

        public override void PostAdd() {
            base.PostAdd();
            GiveJobDoneTracker = new GiveJobDoneTracker();
            MinedOre = new List<Thing>();
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref GiveJobDoneTracker, "GiveJobDoneTracker");
            Scribe_Values.Look(ref MinedOre, "MinedOre");
        }
    }
}