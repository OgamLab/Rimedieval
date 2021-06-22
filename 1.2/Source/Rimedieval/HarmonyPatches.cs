using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace Rimedieval
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("Ogam.Rimedieval");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(PawnWeaponGenerator), "TryGenerateWeaponFor")]
    internal class patch_PawnWeaponGenerator_TryGenerateWeaponFor
    {
        [HarmonyPriority(0)]
        public static void Prefix(Pawn pawn, PawnGenerationRequest request)
        {
            Commonality_Patch.pawnToLookInto = pawn;
        }
        public static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            Commonality_Patch.pawnToLookInto = null;
        }
    }

    [HarmonyPatch(typeof(PawnApparelGenerator), "GenerateStartingApparelFor")]
    internal static class Patch_GenerateStartingApparelFor
    {
        [HarmonyPriority(0)]
        public static void Prefix(Pawn pawn, PawnGenerationRequest request)
        {
            Commonality_Patch.pawnToLookInto = pawn;

        }
        private static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            Commonality_Patch.pawnToLookInto = null;
        }
    }

    [HarmonyPatch(typeof(ThingStuffPair), "Commonality", MethodType.Getter)]
    public class Commonality_Patch
    {
        public static Pawn pawnToLookInto;
        private static bool Prefix(ThingStuffPair __instance, ref float __result)
        {
            if (pawnToLookInto?.Faction != null && !pawnToLookInto.Faction.IsPlayer)
            {
                if (Utils.GetTechLevelFor(__instance.thing) > TechLevel.Medieval)
                {
                    __result = 0f;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Faction), "GetReportText", MethodType.Getter)]
    public static class GetReportText_Patch
    {
        public static void Postfix(Faction __instance, ref string __result)
        {
            __result += "\n\n" + "RM.FactionTechLevelInfo".Translate(__instance.def.techLevel.ToStringHuman());
        }
    }

    [HarmonyPatch(typeof(ThingSetMakerUtility), "GetAllowedThingDefs")]
    public static class GetAllowedThingDefs_Patch
    {
        public static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> __result)
        {
            foreach (var r in __result)
            {
                var techLevel = Utils.GetTechLevelFor(r);
                if (techLevel < TechLevel.Industrial)
                {
                    //Log.Message("GetAllowedThingDefs: " + r + " - " + techLevel);
                    //Log.ResetMessageCount();
                    yield return r;
                }
            }
        }
    }

    [HarmonyPatch(typeof(RaidStrategyWorker), "MinimumPoints")]
    public static class MinimumPoints_Patch
    {
        public static void Prefix(ref Faction faction, ref PawnGroupKindDef groupKind)
        {
            if (faction is null)
            {
                faction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Pirate"));
            }
            if (groupKind is null)
            {
                groupKind = PawnGroupKindDefOf.Combat;
            }
        }
    }

    [HarmonyPatch(typeof(ResearchProjectDef), "CanStartNow", MethodType.Getter)]
    public static class CanStartNow_Patch
    {
        public static void Postfix(ResearchProjectDef __instance, ref bool __result)
        {
            __result = FactionTracker.Instance.AllowedTechLevels().Contains(__instance);
        }
    }

	[HarmonyPatch(typeof(IncidentWorker_RaidEnemy))]
    [HarmonyPatch("FactionCanBeGroupSource")]
    public static class FactionCanBeGroupSourcePatch
    {
        public static void Postfix(ref bool __result, Faction f)
        {
            if (__result && f?.def == FactionDefOf.Mechanoid && RimedievalMod.settings.disableMechanoids)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy))]
    [HarmonyPatch("TryExecuteWorker")]
    public static class TryExecuteWorkerPatch
    {
        public static bool Prefix(IncidentParms parms)
        {
            if (parms.faction != null && parms.faction.def == FactionDefOf.Mechanoid && RimedievalMod.settings.disableMechanoids)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy))]
    [HarmonyPatch("TryResolveRaidFaction")]
    public static class TryResolveRaidFactionPatch
    {
        public static void Postfix(ref bool __result, IncidentParms parms)
        {
            if (__result && parms.faction?.def == FactionDefOf.Mechanoid && RimedievalMod.settings.disableMechanoids)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("SpawnSetup")]
    public static class Pawn_SpawnSetup
    {
        public static void Postfix(Pawn __instance)
        {
            if (RimedievalMod.settings.disableMechanoids && __instance.RaceProps.IsMechanoid)
            {
                __instance.Destroy();
            }
        }
    }

    [HarmonyPatch(typeof(GenStep_ScatterShrines))]
    [HarmonyPatch("CanScatterAt")]
    public static class CanScatterAt
    {
        public static bool Prefix(ref bool __result)
        {
            if (RimedievalMod.settings.disableMechanoids)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GenStep_MechCluster))]
    [HarmonyPatch("Generate")]
    public static class Generate
    {
        public static bool Prefix()
        {
            if (RimedievalMod.settings.disableMechanoids)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_MechCluster))]
    [HarmonyPatch("TryExecuteWorker")]
    public static class TryExecuteWorker
    {
        public static bool Prefix(ref bool __result)
        {
            if (RimedievalMod.settings.disableMechanoids)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MechClusterUtility))]
    [HarmonyPatch("SpawnCluster")]
    public static class SpawnCluster
    {
        public static bool Prefix(ref List<Thing> __result)
        {
            if (RimedievalMod.settings.disableMechanoids)
            {
                __result = new List<Thing>();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(FactionUtility), "HostileTo")]
    public static class Patch_HostileTo
    {
        public static void Postfix(ref bool __result, Faction fac, Faction other)
        {
            if (__result && RimedievalMod.settings.disableMechanoids)
            {
                if (fac == Faction.OfMechanoids && other.IsPlayer)
                {
                    Log.Message(fac.def + " - " + other.def);
                    __result = false;
                }
                else if (other == Faction.OfMechanoids && fac.IsPlayer)
                {
                    Log.Message(fac.def + " - " + other.def);
                    __result = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(QuestNode_GetFaction))]
    [HarmonyPatch("IsGoodFaction")]
    public static class IsGoodFaction_Patch
    {
        public static bool Prefix(ref bool __result, Faction faction, Slate slate)
        {
            if (RimedievalMod.settings.disableMechanoids && faction == Faction.OfMechanoids)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ThreatsGenerator))]
    [HarmonyPatch("GetPossibleIncidents")]
    public static class GetPossibleIncidents_Patch
    {
        public static IEnumerable<IncidentDef> Postfix(IEnumerable<IncidentDef> __result)
        {
            foreach (var r in __result)
            {
                if (r == IncidentDefOf.MechCluster && RimedievalMod.settings.disableMechanoids)
                {
                    continue;
                }
                else
                {
                    yield return r;
                }
            }
        }
    }
}
