using System.Collections.Generic;
using HugsLib.Utils;
using Verse;

namespace Rimworld_Prospector
{
    public class WorldDataStore : UtilityWorldObject
    {
        public GiveJobDoneTracker GiveJobDoneTracker;

        public override void PostAdd() {
            base.PostAdd();
            GiveJobDoneTracker = new GiveJobDoneTracker();
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref GiveJobDoneTracker, "GiveJobDoneTracker");
        }
    }
}