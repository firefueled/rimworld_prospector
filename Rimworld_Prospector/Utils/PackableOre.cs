using Verse;

namespace Rimworld_Prospector
{
    public class PackableOre
    {
        internal Thing Ore { get; }
        internal int StackCount { get; }

        public PackableOre(Thing ore, int stackCount)
        {
            Ore = ore;
            StackCount = stackCount;
        }
    }
}