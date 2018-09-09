using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    internal static class Utils
    {
        private static bool IsPackAnimalReachable(Thing p, Pawn prospector)
        {
            PawnPath path = prospector.Map.pathFinder.FindPath(
                prospector.Position, p.Position, prospector, PathEndMode.ClosestTouch
            );
            return path.TotalCost <= 200;
        }

        /**
         * Wether a mine "Order" hasn't been placed on the cell yet
         */
        public static bool HasntBeenDesignatedYet(Designator dm, IntVec3 cell)
        {
            return dm.CanDesignateCell(cell).Accepted;
        }

        /**
         * Wether the Thing is a rock with mineable resources
         */
        public static bool IsResourceRock(ThingDef def)
        {
            return def?.building != null && def.building.isResourceRock;
        }

        /**
         * Wether the pawn can reach the cell (Rock) to the point of being able to touch it,
         * instead of standing on it
         */
        public static bool CanReach(Pawn pawn, IntVec3 cell)
        {
            return pawn.CanReach(cell, PathEndMode.Touch, Danger.None);
        }

        public static bool FindAvailablePackAnimal(Pawn prospector)
        {
            if (DoneMiningRock.MapData.PawnPackAnimalTracker.ContainsKey(prospector.ThingID))
            {
                Pawn existingPackAnimal = DoneMiningRock.MapData.PawnPackAnimalTracker[prospector.ThingID];
                if (IsPackAnimalReachable(existingPackAnimal, prospector)) return true;

                Log.Message("Existing pack animal " + existingPackAnimal + " is too far");
                return false;
            }

            // Find a pack animal that's close by, if it exists
            foreach (Pawn p in prospector.Map.mapPawns.PawnsInFaction(prospector.Faction))
            {
                if (p.playerSettings.Master != prospector) continue;

                if (!IsPackAnimalReachable(p, prospector))
                {
                    Log.Message("Found pack animal " + p + " but it's too far");
                    continue;
                }

                DoneMiningRock.MapData.PawnPackAnimalTracker.Add(prospector.ThingID, p);
                return true;
            }

            return false;
        }

        /**
         * Decide wether there are enough mined ore around to fully pack the mule
         * and return the list of ore to be packed
         */
        public static bool MaybeListOreToPack(out List<PackableOre> oreToPack, Pawn packAnimal)
        {
            oreToPack = new List<PackableOre>();

            if (DoneMiningRock.MapData.MinedOre.Count == 0)
            {
                return false;
            }

            var max =
                MassUtility.CountToPickUpUntilOverEncumbered(packAnimal, DoneMiningRock.MapData.MinedOre.First()) - 1;
            var toPackCount = 0;

            foreach (Thing ore in DoneMiningRock.MapData.MinedOre)
            {
                if (ore.def != DoneMiningRock.MapData.MinedOre.First().def)
                {
                    continue;
                }

                var toPackDiff = max - toPackCount;
                if (ore.stackCount <= toPackDiff)
                {
                    oreToPack.Add(new PackableOre(ore, ore.stackCount));
                    toPackCount += ore.stackCount;
                }
                else if (toPackDiff >= 1)
                {
                    oreToPack.Add(new PackableOre(ore, toPackDiff));
                    toPackCount += toPackDiff;
                }

                if (toPackCount >= max)
                {
                    break;
                }
            }

            return toPackCount >= max;
        }

        /**
         * Store the ore mined so far for later reference
         */
        public static void AddMinedOreAt(Thing thingPosition, Map map)
        {
            Thing t = map.thingGrid.ThingAt(thingPosition.Position, ThingCategory.Item);
            if (t.def == ThingDefOf.Steel ||
                t.def == ThingDefOf.ComponentIndustrial ||
                t.def == ThingDefOf.Gold ||
                t.def == ThingDefOf.Plasteel ||
                t.def == ThingDefOf.Silver ||
                t.def == ThingDefOf.Uranium)
            {
                DoneMiningRock.MapData.MinedOre.Add(t);
            }
        }

        public class PackableOre
        {
            public Thing Ore { get; }
            public int StackCount { get; }

            public PackableOre(Thing ore, int stackCount)
            {
                Ore = ore;
                StackCount = stackCount;
            }
        }
    }
}