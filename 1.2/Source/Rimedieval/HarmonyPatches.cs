using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;

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
                if (techLevel < TechLevel.Industrial || Utils.IsAllowed(r))
                {
                    yield return r;
                }
                else
                {
                    Log.Message("Disallowed: " + r + " - " + techLevel);
                    Log.ResetMessageCount();
                }
            }
        }
    }

    [HarmonyPatch(typeof(ResearchProjectDef), "CanStartNow", MethodType.Getter)]
    public static class CanStartNow_Patch
    {
        public static void Postfix(ResearchProjectDef __instance, ref bool __result)
        {
            if (__result)
            {
                __result = FactionTracker.Instance.AllowedTechLevels().Contains(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "VisibleResearchProjects", MethodType.Getter)]
    public static class VisibleResearchProjects_Patch
    {
        public static void Postfix(ref List<ResearchProjectDef> __result)
        {
            if (!RimedievalMod.settings.disableTechRestriction)
            {
                __result = GetAllowedProjectDefs(__result);
            }
        }

        public static List<ResearchProjectDef> GetAllowedProjectDefs(List<ResearchProjectDef> list)
        {
            if (RimedievalMod.settings.restrictTechToMedievalOnly)
            {
                list = list.Where(x => x.techLevel <= TechLevel.Medieval).ToList();
            }
            else
            {
                var microElectronics = DefDatabase<ResearchProjectDef>.GetNamed("MicroelectronicsBasics");
                list = list.Where(x => !x.ContainsTechProjectAsPrerequisite(microElectronics)).ToList();
            }
            return list;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "DrawLeftRect")]
    public static class DrawLeftRect_Patch
    {
        [TweakValue("000", 500, 800)] public static float yOffset = 565;
        [TweakValue("000", 0, 80)] public static float xOffset = 12;
        public static void Postfix(ResearchProjectDef ___selectedProject, Rect leftOutRect)
        {
            Rect position = leftOutRect;
            GUI.BeginGroup(position);
            if (___selectedProject != null && !___selectedProject.IsFinished && !FactionTracker.Instance.AllowedTechLevels().Contains(___selectedProject))
            {
                Rect rect = new Rect(xOffset, yOffset, position.width, 50f);
                Widgets.Label(rect, "RM.Locked".Translate());
            }
            GUI.EndGroup();
        }
    }

    [HarmonyPatch]
    public class MechSpawn_Patch
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var meth = AccessTools.Method("ResearchPal.Tree:PopulateNodes");
            Log.Message("Meth: " + meth);
            if (meth != null)
            {
                yield return meth;
            }
        }
        public static void Prefix(out List<ResearchProjectDef> __state)
        {
            __state = new List<ResearchProjectDef>();
            if (!RimedievalMod.settings.disableTechRestriction)
            {
                var list = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
                var alloweedDefs = VisibleResearchProjects_Patch.GetAllowedProjectDefs(list);
                for (int num = list.Count - 1; num >= 0; num--)
                {
                    var def = list[num];
                    if (!alloweedDefs.Contains(def))
                    {
                        __state.Add(def);
                        RemoveDef(def);
                    }
                }
            }
        }
        public static void Postfix(List<ResearchProjectDef> __state)
        {
            foreach (var def in __state)
            {
                AddDef(def);
            }
        }

        public static void AddDef(ResearchProjectDef def)
        {
            DefDatabase<ResearchProjectDef>.Add(def);
        }
        public static void RemoveDef(ResearchProjectDef def)
        {
            try
            {
                MethodInfo methodInfo = AccessTools.Method(typeof(DefDatabase<ResearchProjectDef>), "Remove", null, null);
                methodInfo.Invoke(null, new object[]
                {
                    def
                });
            }
            catch { };
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

    [HarmonyPatch(typeof(SiegeBlueprintPlacer))]
    [HarmonyPatch("PlaceArtilleryBlueprints")]
    public static class PlaceArtilleryBlueprints_Patch
    {
        public static bool Prefix(ref IEnumerable<Blueprint_Build> __result, ref float points, Map map, Faction ___faction, IntVec3 ___center)
        {
            var list = new List<Blueprint_Build>();
            IEnumerable<ThingDef> artyDefs = DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => def.building != null && def.building.buildingTags.Contains("MedievalArtillery_BaseDestroyer"));
            if (artyDefs.Any())
            {
                int numArtillery = Mathf.RoundToInt(points / 60f);
                numArtillery = Mathf.Clamp(numArtillery, 1, 2);
                for (int i = 0; i < numArtillery; i++)
                {
                    Rot4 random = Rot4.Random;
                    ThingDef thingDef = artyDefs.RandomElement();
                    IntVec3 intVec = FindArtySpot(thingDef, random, map, ___center);
                    if (!intVec.IsValid)
                    {
                        break;
                    }
                    list.Add(GenConstruct.PlaceBlueprintForBuild(thingDef, intVec, map, random, ___faction, ThingDefOf.WoodLog));
                    points -= 60f;
                }
            }
            __result = list;
            return false;
        }
        private static IntVec3 FindArtySpot(ThingDef artyDef, Rot4 rot, Map map, IntVec3 center)
        {
            CellRect cellRect = CellRect.CenteredOn(center, 8);
            cellRect.ClipInsideMap(map);
            int num = 0;
            IntVec3 randomCell;
            do
            {
                num++;
                if (num > 200)
                {
                    return IntVec3.Invalid;
                }
                randomCell = cellRect.RandomCell;
            }
            while (!map.reachability.CanReach(randomCell, center, PathEndMode.OnCell, TraverseMode.NoPassClosedDoors, Danger.Deadly) || randomCell.Roofed(map) || !CanPlaceBlueprintAt(randomCell, rot, artyDef, map, ThingDefOf.Steel));
            return randomCell;
        }
        private static bool CanPlaceBlueprintAt(IntVec3 root, Rot4 rot, ThingDef buildingDef, Map map, ThingDef stuffDef)
        {
            return GenConstruct.CanPlaceBlueprintAt(buildingDef, root, rot, map, godMode: false, null, null, stuffDef).Accepted;
        }
    }
}
