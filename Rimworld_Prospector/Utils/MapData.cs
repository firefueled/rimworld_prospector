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
            GiveJobDoneTracker = new Dictionary<string, bool>();
            PawnPackAnimalTracker = new Dictionary<string, Pawn>();
            PawnPackAnimalFollowing = new Dictionary<string, bool>();
            Designations = new List<IntVec3>();
        }

        /**
         * The helper for tracking Jobs' doneness
         */
        public Dictionary<string, bool> GiveJobDoneTracker;

        /**
         * The list of mined ore 
         */
        public List<Thing> MinedOre;

        /**
         * The list of mine designations
         * Defines when the prospection is done, when eq 0 
         */
        public List<IntVec3> Designations;

        /**
         * Pack animal
         */
        public Dictionary<string, Pawn> PawnPackAnimalTracker;

        /**
         * Tracks the pack animals told to follow a prospector
         */
        public Dictionary<string, bool> PawnPackAnimalFollowing;

        private List<string> ppKeys;
        private List<Pawn> ppVals;

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Collections.Look(ref GiveJobDoneTracker, "GiveJobDoneTracker", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref MinedOre, "MinedOre", LookMode.Reference);
            Scribe_Collections.Look(ref PawnPackAnimalTracker, "PawnPackAnimalTracker", LookMode.Value, LookMode.Reference, ref ppKeys, ref ppVals);
            Scribe_Collections.Look(ref PawnPackAnimalFollowing, "PawnPackAnimalFollowing", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref Designations, "Designations", LookMode.Value);
        }
    }
}