using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using RimWorld;
using System.Reflection;
using Verse;

namespace Rimworld_Prospector
{
    [StaticConstructorOnStartup]
    class Main
    {
        // this static constructor runs to create a HarmonyInstance and install a patch.
        static Main()
        {
            var harmony = HarmonyInstance.Create("com.github.firefueled.rimworld_prospector");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(WindowStack))]
        [HarmonyPatch("Add")]
        [HarmonyPatch(new Type[] { typeof(Window) })]
        class PatchWindow
        {
            static void Prefix(Window window)
            {
                Log.Warning("Hello RimWorld");
            }
        }

        [HarmonyPatch(typeof(Mineable), "Destroy")]
        class PatchDestroy
        {
            static void Prefix(DestroyMode mode)
            {
                Log.Warning("Destroy: " + mode.ToString());
            }
        }

        [HarmonyPatch(typeof(Mineable), "PreApplyDamage")]
        //, new Type[] { typeof(DamageInfo), typeof(bool) }
        class PatchPreApplyDamage
        {
            static void Prefix(DamageInfo dinfo, out bool absorbed)
            {
                Log.Warning("PreApplyDamage: " + dinfo.ToString());
                absorbed = false;
            }
        }

        [HarmonyPatch(typeof(Mineable), "DestroyMined")]
        class PatchDestroyMined
        {
            static void Prefix(Pawn pawn)
            {
                Log.Warning("DestroyMined: " + pawn.ToString());
            }
        }

    }
}
