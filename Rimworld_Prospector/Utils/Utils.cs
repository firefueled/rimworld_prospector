using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
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
            var mapData = prospector.Map.GetComponent<MapData>();

            if (mapData.PawnPackAnimalTracker.ContainsKey(prospector.ThingID))
                return true;

            // Find a pack animal that's close by, if it exists
            foreach (Pawn p in prospector.Map.mapPawns.SpawnedPawnsInFaction(prospector.Faction))
            {
                if (p.playerSettings.Master != prospector) continue;
                if (!p.RaceProps.packAnimal) continue;

                mapData.PawnPackAnimalTracker.Add(prospector.ThingID, p);
                return true;
            }

            return false;
        }

        /**
         * Decide wether there are enough mined ore around to fully pack the mule
         * and return the list of ore to be packed
         */
        public static bool MaybeListOreToPack(out List<PackableOre> oreToPack, Pawn packAnimal, bool isLeavingSite = false)
        {
            var mapData = packAnimal.Map.GetComponent<MapData>();
            oreToPack = new List<PackableOre>();

            if (mapData.MinedOre.Count == 0)
            {
                return false;
            }

            var max =
                MassUtility.CountToPickUpUntilOverEncumbered(packAnimal, mapData.MinedOre.First()) - 1;
            var toPackCount = 0;
            var toPackValue = 0f;
            
            // If we have different ore to be hauled, do so one type (def) at a time 
            var areOreMixed = false; 

            ThingDef firstOreDef = mapData.MinedOre.First().def;
            
            foreach (Thing ore in mapData.MinedOre)
            {
                // Sets up hauling ore one type (def) at a time
                if (ore.def != firstOreDef)
                {
                    areOreMixed = true;
                    continue;
                }

                var toPackDiff = max - toPackCount;
                if (ore.stackCount <= toPackDiff)
                {
                    oreToPack.Add(new PackableOre(ore, ore.stackCount));
                    toPackCount += ore.stackCount;
                    toPackValue += ore.stackCount * ore.MarketValue;
                }
                else if (toPackDiff >= 1)
                {
                    oreToPack.Add(new PackableOre(ore, toPackDiff));
                    toPackCount += toPackDiff;
                    toPackValue += toPackDiff * ore.MarketValue;
                }

                if (toPackCount >= max)
                {
                    break;
                }
            }
            
            if (isLeavingSite || areOreMixed)
                return toPackCount > 0;

            return toPackCount >= max || toPackValue > 3000;
        }

        /**
         * Store the ore mined so far for later reference
         */
        public static void AddMinedOreAt(Thing thing, Map map)
        {
            var mapData = map.GetComponent<MapData>();
            Thing t = map.thingGrid.ThingAt(thing.Position, ThingCategory.Item);
            if (t.def == ThingDefOf.Steel ||
                t.def == ThingDefOf.ComponentIndustrial ||
                t.def == ThingDefOf.Gold ||
                t.def == ThingDefOf.Plasteel ||
                t.def == ThingDefOf.Silver ||
                t.def == ThingDefOf.Uranium ||
                t.def == DefDatabase<ThingDef>.GetNamed("Jade")) // don't ask
            {
                mapData.MinedOre.Add(t);
            }
        }

        /**
         * Find the closest dump spot to the pack animal, or null
         */
        public static Building FindClosestDumpSpot(Thing pawn)
        {
            var dumpSpots = pawn.Map.listerBuildings.AllBuildingsColonistOfDef(
                DefDatabase<ThingDef>.GetNamed("ProspectionDumpSpot"));

            Building dumpSpot = null;
            var minDist = -1;

            foreach (Building spot in dumpSpots)
            {
                var distanceToSquared = spot.Position.DistanceToSquared(pawn.Position);
                if (dumpSpot == null || distanceToSquared < minDist)
                {
                    dumpSpot = spot;
                    minDist = distanceToSquared;
                }
            }

            return dumpSpot;
        }
        /**
         * Designate rock ore cells around the player for mining
         */
        public static void DeisgnateCellsAround(Pawn prospector)
        {
            var mapData = prospector.Map.GetComponent<MapData>();

            // A cell grid around the mining pawn covering an area two cells away from it
            var cellsAround = GenAdj.CellsOccupiedBy(prospector.Position, prospector.Rotation, new IntVec2(5, 5));
            var dm = new Designator_Mine();
            
            foreach (IntVec3 cell in cellsAround)
            {
                // Find out what Thing is on the cell
                Thing thing = prospector.Map.thingGrid.ThingAt(cell, ThingCategory.Building);
                if (
                    thing != null && 
                    IsResourceRock(thing.def) && 
                    HasntBeenDesignatedYet(dm, cell) && 
                    CanReach(prospector, cell)
                )
                {
                    // Designate the cell to be mined
                    dm.DesignateSingleCell(cell);
                    
                    // Save each designation
                    mapData.Designations.Add(cell);
                }
            }
        }

        public static bool HasAvailablePackMule(Pawn packMule)
        {
            if (packMule == null) return false;
            if (packMule.health.State != PawnHealthState.Mobile) return false;
            if (!packMule.Awake()) return false;
            if (packMule.Drafted) return false;
            if (packMule.InMentalState) return false;
            if (packMule.IsFormingCaravan()) return false;

            return true;
        }
    }
}
