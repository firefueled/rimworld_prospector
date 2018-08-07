using HugsLib.Utils;
using Verse;

namespace Rimworld_Prospector.Properties
{
    public class WorldDataStore : UtilityWorldObject
    {
        public bool isGiveJobDone;

        public override void PostAdd() {
            base.PostAdd();
            isGiveJobDone = false;
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref isGiveJobDone, "isGiveJobDone", false);
        }
    }
}