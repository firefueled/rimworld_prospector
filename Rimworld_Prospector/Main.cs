﻿using Harmony;
using HugsLib;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    // A mining operation has ended
    [HarmonyPatch(typeof(Mineable), "DestroyMined")]
    class DoneMiningRock : ModBase
    {
        private static bool hasPackMule;
        private static Pawn packMule;
        public static List<Thing> minedOre;

        public override string ModIdentifier => "com.firefueled.rimworld_prospector";

        static void Postfix(Pawn pawn)
        {
            DeisgnateCellsAround(pawn);
            if (hasPackMule && IsPackMuleAround())
            {
                StoreOreInPackMule();
            }
        }

        private static void StoreOreInPackMule()
        {
            if (IsPackMuleFull())
            {
                SendPackMuleHome();
            }
        }

        private static bool IsPackMuleAround()
        {
            throw new NotImplementedException();
        }

        private static void SendPackMuleHome()
        {
            throw new NotImplementedException();
        }

        private static bool IsPackMuleFull()
        {
            throw new NotImplementedException();
        }

        private static void DeisgnateCellsAround(Pawn pawn)
        {
            // A cell grid around the mining pawn covering an area two cells away from it
            var cellsAround = GenAdj.CellsOccupiedBy(pawn.Position, pawn.Rotation, new IntVec2(5, 5));
            Designator_Mine dm = new Designator_Mine();

            foreach (var cell in cellsAround)
            {
                // Find out what Thing, of the Bulding, category is on the cell
                var thing = pawn.Map.thingGrid.ThingAt(cell, ThingCategory.Building);
                if (
                        thing != null &&
                        IsOre(thing.def) &&
                        HasntBeenDesignatedYet(dm, cell) &&
                        CanReach(pawn, cell)
                    )
                {
                    // "Order" the cell to be mined
                    dm.DesignateSingleCell(cell);
                }
            }
        }

        // Wether an mine "Order" hasn't been placed on the cell yet
        static bool HasntBeenDesignatedYet(Designator_Mine dm, IntVec3 cell)
        {
            return dm.CanDesignateCell(cell).Accepted;
        }

        // Wether the Thing is a rock with mineable resources
        static bool IsOre(ThingDef def)
        {
            return def != null && def.building != null && def.building.isResourceRock;
        }

        // Wether the pawn can reach the cell (Rock) to the point of being able to touch it,
        // instead of standing on it
        static bool CanReach(Pawn pawn, IntVec3 cell)
        {
            return pawn.CanReach(cell, PathEndMode.Touch, Danger.None);
        }
    }
}
