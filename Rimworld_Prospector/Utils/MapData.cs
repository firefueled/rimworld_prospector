using System.Collections.Generic;
using Verse;

namespace Rimworld_Prospector
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class MapData : MapComponent
    {
        public MapData (Map map) : base(map)
        {
            MinedOre = new List<Thing>();
            GiveJobDoneTracker = new GiveJobDoneTracker();
            PawnPackAnimalTracker = new Dictionary<string, Pawn>();
            DesignationTracker = new Dictionary<string, List<IntVec3>>();
        }

        /**
         * The helper for tracking Jobs' doneness
         */
        public GiveJobDoneTracker GiveJobDoneTracker;

        /**
         * The list of mined ore 
         */
        public List<Thing> MinedOre;

        /**
         * Pack animal
         */
        public Dictionary<string, Pawn> PawnPackAnimalTracker;
        public Dictionary<string, List<IntVec3>> DesignationTracker;

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref GiveJobDoneTracker, "GiveJobDoneTracker");
            Scribe_Values.Look(ref MinedOre, "MinedOre");
            Scribe_Values.Look(ref PawnPackAnimalTracker, "PawnPackAnimalTracker");
            Scribe_Values.Look(ref DesignationTracker, "DesignationTracker");
        }
    }
}