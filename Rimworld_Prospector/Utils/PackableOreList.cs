using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Verse;

namespace Rimworld_Prospector
{
    public class PackableOreList : ILoadReferenceable
    {
        public List<PackableOre> List { get; }

        public PackableOreList(List<PackableOre> list)
        {
            List = list;
        }

        public string GetUniqueLoadID()
        {
            // two different pawn can request the same ore stack but with different stack counts
            var comboIds = List.Aggregate("", (id, ore) => id + ore.Ore.ThingID);
            Log.Message("comboIds " + comboIds);
            var uniqueLoadId = string.Join("", new SHA1Managed().ComputeHash(Encoding.UTF8.GetBytes(comboIds)).Select(x => x.ToString("x2")).ToArray());
            Log.Message("uniqueLoadId " + uniqueLoadId);
            return uniqueLoadId;
        }
    }
}