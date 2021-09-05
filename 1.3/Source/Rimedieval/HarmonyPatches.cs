using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
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
            RimedievalMod.harmony.PatchAll();

            var allowedPrecepts = AccessTools.Method(typeof(HarmonyPatches), nameof(HarmonyPatches.AllowedPrecepts));
            foreach (var preceptWorkerType in typeof(PreceptWorker).AllSubclasses().AddItem(typeof(PreceptWorker)))
            {
                try
                {
                    var method = AccessTools.Method(preceptWorkerType, "get_ThingDefs");
                    RimedievalMod.harmony.Patch(method, null, new HarmonyMethod(allowedPrecepts));
                }
                catch
                {
                }
            }
        }

        public static IEnumerable<PreceptThingChance> AllowedPrecepts(IEnumerable<PreceptThingChance> __result)
        {
            return __result.Where(x => x.def.IsAllowed() && x.chance > 0);
        }
    }

    [HarmonyPatch(typeof(ThingSetMakerUtility), "GetAllowedThingDefs")]
    public static class GetAllowedThingDefs_Patch
    {
        public static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> __result)
        {
            return __result.GetAllowedThingDefs();
        }
    }

    [HarmonyPatch(typeof(PawnWeaponGenerator), "TryGenerateWeaponFor")]
    public class PawnWeaponGenerator_TryGenerateWeaponFor
    {
        [HarmonyPriority(0)]
        public static void Prefix(Pawn pawn, PawnGenerationRequest request)
        {
            Commonality_Patch.pawnToLookInto = pawn;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var method = AccessTools.Method(typeof(Pawn_EquipmentTracker), "AddEquipment");
            var pawnField = AccessTools.Field(typeof(PawnWeaponGenerator).GetNestedTypes(AccessTools.all).First(c => c.Name.Contains("c__DisplayClass5_0")), "pawn");
            var workingWeaponsField = AccessTools.Field(typeof(PawnWeaponGenerator), "workingWeapons");
            var methodToCall = AccessTools.Method(typeof(PawnWeaponGenerator_TryGenerateWeaponFor), "TryGenerateWeaponForOverride");
            var randomWeight = AccessTools.Method(typeof(GenCollection), "TryRandomElementByWeight", generics: new[] { typeof(ThingStuffPair) });
        
            Label label = generator.DefineLabel();
            for (int i = 0; i < codes.Count; i++)
            {
                if (i > 1 && codes[i - 1].Calls(randomWeight))
                {
                    yield return new CodeInstruction(OpCodes.Brfalse_S, label);
                }
                else
                {
                    yield return codes[i];
                }
        
                if (codes[i].Calls(method))
                {
                    var ldsFieldInd = codes.FirstIndexOf(x => codes.IndexOf(x) > i && x.LoadsField(workingWeaponsField));
                    yield return new CodeInstruction(OpCodes.Br_S, codes[ldsFieldInd].labels.First());
                    yield return new CodeInstruction(OpCodes.Ldloc_0).WithLabels(label);
                    yield return new CodeInstruction(OpCodes.Ldfld, pawnField);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, methodToCall); 
                }
            }
        }
        public static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            Commonality_Patch.pawnToLookInto = null;
        }

        public static void TryGenerateWeaponForOverride(Pawn pawn, PawnGenerationRequest request)
        {
            PawnWeaponGenerator.workingWeapons.Clear();
            if (pawn.kindDef.weaponTags == null || pawn.kindDef.weaponTags.Count == 0 || !pawn.RaceProps.ToolUser || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return;
            }

            var weaponTags = pawn.kindDef.IsMelee() ? Utils.medievalMeleeWeaponTags : Rand.Chance(0.3f) ? Utils.medievalRangeWeaponTags : Utils.medievalMeleeWeaponTags;

            float randomInRange = pawn.kindDef.weaponMoney.RandomInRange;
            for (int i = 0; i < PawnWeaponGenerator.allWeaponPairs.Count; i++)
            {
                ThingStuffPair w2 = PawnWeaponGenerator.allWeaponPairs[i];
                if (!(w2.Price > randomInRange) && Utils.GetTechLevelFor(w2.thing) <= (pawn.Faction?.def?.techLevel ?? TechLevel.Medieval) 
                    && weaponTags.Any((string tag) => w2.thing.weaponTags.Contains(tag)) 
                    && (pawn.kindDef.weaponStuffOverride == null || w2.stuff == pawn.kindDef.weaponStuffOverride) 
                    && (!w2.thing.IsRangedWeapon || !pawn.WorkTagIsDisabled(WorkTags.Shooting)) 
                    && (!(w2.thing.generateAllowChance < 1f) || Rand.ChanceSeeded(w2.thing.generateAllowChance, pawn.thingIDNumber ^ w2.thing.shortHash ^ 0x1B3B648)))
                {
                    PawnWeaponGenerator.workingWeapons.Add(w2);
                }
            }
            if (PawnWeaponGenerator.workingWeapons.Count == 0)
            {
                return;
            }
            pawn.equipment.DestroyAllEquipment();
            if (PawnWeaponGenerator.workingWeapons.TryRandomElementByWeight((ThingStuffPair w) => w.Commonality * w.Price * PawnWeaponGenerator.GetWeaponCommonalityFromIdeo(pawn, w), out var result))
            {
                ThingWithComps thingWithComps = (ThingWithComps)ThingMaker.MakeThing(result.thing, result.stuff);
                PawnGenerator.PostProcessGeneratedGear(thingWithComps, pawn);
                CompEquippable compEquippable = thingWithComps.TryGetComp<CompEquippable>();
                if (compEquippable != null)
                {
                    if (pawn.kindDef.weaponStyleDef != null)
                    {
                        compEquippable.parent.StyleDef = pawn.kindDef.weaponStyleDef;
                    }
                    else if (pawn.Ideo != null)
                    {
                        compEquippable.parent.StyleDef = pawn.Ideo.GetStyleFor(thingWithComps.def);
                    }
                }
                float num = ((request.BiocodeWeaponChance > 0f) ? request.BiocodeWeaponChance : pawn.kindDef.biocodeWeaponChance);
                if (Rand.Value < num)
                {
                    thingWithComps.TryGetComp<CompBiocodable>()?.CodeFor(pawn);
                }
                pawn.equipment.AddEquipment(thingWithComps);
            }
            PawnWeaponGenerator.workingWeapons.Clear();
        }
    }

    [HarmonyPatch(typeof(PawnApparelGenerator), "GenerateStartingApparelFor")]
    public class Patch_GenerateStartingApparelFor
    {
        [HarmonyPriority(0)]
        public static void Prefix(Pawn pawn, PawnGenerationRequest request)
        {
            Commonality_Patch.pawnToLookInto = pawn;
        }
    
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var workingSetField = AccessTools.Field(typeof(PawnApparelGenerator), "workingSet");
            var giveToPawnMeth = AccessTools.Method(typeof(PawnApparelGenerator.PossibleApparelSet), "GiveToPawn");
            var methodToCall = AccessTools.Method(typeof(Patch_GenerateStartingApparelFor), "TryGenerateStartingApparelForOverride");
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(workingSetField) && codes[i + 2].Calls(giveToPawnMeth))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_1).MoveLabelsFrom(codes[i]);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, methodToCall);
                }
                yield return codes[i];
            }
        }
        
        public static void TryGenerateStartingApparelForOverride(float randomInRange, Pawn pawn, PawnGenerationRequest request)
        {
            if (!PawnApparelGenerator.workingSet.IsNaked(pawn.gender) && !pawn.RaceProps.ToolUser || !pawn.RaceProps.IsFlesh)
            {
                return;
            }
            randomInRange = randomInRange - PawnApparelGenerator.workingSet.TotalPrice;
            if (randomInRange <= 0)
            {
                return;
            }
        
            bool isOverwritten = false;
            if (PawnApparelGenerator.workingSet.TotalPrice <= 0)
            {
                pawn.apparel.DestroyAll();
                pawn.outfits?.forcedHandler?.Reset();
                isOverwritten = true;
            }
        
            float mapTemperature;
            NeededWarmth neededWarmth = PawnApparelGenerator.ApparelWarmthNeededNow(pawn, request, out mapTemperature);
            bool allowHeadgear = Rand.Value < pawn.kindDef.apparelAllowHeadgearChance;
        
            int @int = Rand.Int;
            PawnApparelGenerator.tmpApparelCandidates.Clear();
            var pairs = pawn.kindDef.apparelTags?.Any() ?? false ? pawn.kindDef.IsWarrior() ? Utils.medievalArmorTags : Utils.medievalApparelTags : new List<string>();

            for (int i = 0; i < PawnApparelGenerator.allApparelPairs.Count; i++)
            {
                ThingStuffPair thingStuffPair = PawnApparelGenerator.allApparelPairs[i];
                if (CanUsePairCustom(pairs, thingStuffPair, pawn, randomInRange, allowHeadgear, @int))
                {
                    PawnApparelGenerator.tmpApparelCandidates.Add(thingStuffPair);
                }
            }
            if (randomInRange < 0.001f)
            {
                GenerateWorkingPossibleApparelSetForOverride(isOverwritten, pawn, randomInRange, PawnApparelGenerator.tmpApparelCandidates);
            }
            else
            {
                int num = 0;
                while (true)
                {
                    GenerateWorkingPossibleApparelSetForOverride(isOverwritten, pawn, randomInRange, PawnApparelGenerator.tmpApparelCandidates);
                    if (num < 10 && Rand.Value < 0.85f && randomInRange < 9999999f)
                    {
                        float num2 = Rand.Range(0.45f, 0.8f);
                        float totalPrice = PawnApparelGenerator.workingSet.TotalPrice;
                        if (totalPrice < randomInRange * num2)
                        {
                            goto IL_0399;
                        }
                    }
                    if (num < 20 && Rand.Value < 0.97f && !PawnApparelGenerator.workingSet.Covers(BodyPartGroupDefOf.Torso))
                    {
                    }
                    else if (num < 30 && Rand.Value < 0.8f && PawnApparelGenerator.workingSet.CoatButNoShirt())
                    {
                    }
                    else
                    {
                        if (num < 50)
                        {
                            bool mustBeSafe = num < 17;
                            if (!PawnApparelGenerator.workingSet.SatisfiesNeededWarmth(neededWarmth, mustBeSafe, mapTemperature))
                            {
                                goto IL_0399;
                            }
                        }
                        if (num >= 80 || !PawnApparelGenerator.workingSet.IsNaked(pawn.gender))
                        {
                            break;
                        }
                    }
                    goto IL_0399;
                    IL_0399:
                    num++;
                }
            }
            if ((!pawn.kindDef.apparelIgnoreSeasons || request.ForceAddFreeWarmLayerIfNeeded) && !PawnApparelGenerator.workingSet.SatisfiesNeededWarmth(neededWarmth, mustBeSafe: true, mapTemperature))
            {
                PawnApparelGenerator.workingSet.AddFreeWarmthAsNeeded(neededWarmth, mapTemperature);
            }
        }
        private static void GenerateWorkingPossibleApparelSetForOverride(bool isOverwritten, Pawn pawn, float money, List<ThingStuffPair> apparelCandidates)
        {
            if (isOverwritten)
            {
                PawnApparelGenerator.workingSet.Reset(pawn.RaceProps.body, pawn.def);
            }
            float num = money;
            List<SpecificApparelRequirement> att = pawn.kindDef.specificApparelRequirements;
            if (att != null)
            {
                int j;
                for (j = 0; j < att.Count; j++)
                {
                    if ((!att[j].RequiredTag.NullOrEmpty() || !att[j].AlternateTagChoices.NullOrEmpty()) && PawnApparelGenerator.allApparelPairs.Where((ThingStuffPair pa) => PawnApparelGenerator.ApparelRequirementTagsMatch(att[j], pa.thing) && PawnApparelGenerator.ApparelRequirementHandlesThing(att[j], pa.thing) && PawnApparelGenerator.CanUseStuff(pawn, pa) && pa.thing.apparel.CorrectGenderForWearing(pawn.gender) && !PawnApparelGenerator.workingSet.PairOverlapsAnything(pa)).TryRandomElementByWeight((ThingStuffPair pa) => pa.Commonality, out var result))
                    {
                        PawnApparelGenerator.workingSet.Add(result);
                        num -= result.Price;
                    }
                }
            }
            List<ThingDef> reqApparel = pawn.kindDef.apparelRequired;
            if (reqApparel != null)
            {
                int i;
                for (i = 0; i < reqApparel.Count; i++)
                {
                    if (PawnApparelGenerator.allApparelPairs.Where((ThingStuffPair pa) => pa.thing == reqApparel[i] && PawnApparelGenerator.CanUseStuff(pawn, pa) 
                    && !PawnApparelGenerator.workingSet.PairOverlapsAnything(pa)).TryRandomElementByWeight((ThingStuffPair pa) => pa.Commonality, out var result2))
                    {
                        PawnApparelGenerator.workingSet.Add(result2);
                        num -= result2.Price;
                    }
                }
            }
            PawnApparelGenerator.usableApparel.Clear();
            for (int k = 0; k < apparelCandidates.Count; k++)
            {
                if (!PawnApparelGenerator.workingSet.PairOverlapsAnything(apparelCandidates[k]))
                {
                    PawnApparelGenerator.usableApparel.Add(apparelCandidates[k]);
                }
            }
            ThingStuffPair result3;
            while ((pawn.Ideo == null || !pawn.Ideo.IdeoPrefersNudityForGender(pawn.gender) || (pawn.Faction != null && pawn.Faction.IsPlayer)) && (!(Rand.Value < 0.1f) || !(money < 9999999f)) && PawnApparelGenerator.usableApparel.Where((ThingStuffPair pa) => PawnApparelGenerator.CanUseStuff(pawn, pa)).TryRandomElementByWeight((ThingStuffPair pa) => pa.Commonality, out result3))
            {
                PawnApparelGenerator.workingSet.Add(result3);
                num -= result3.Price;
                for (int num2 = PawnApparelGenerator.usableApparel.Count - 1; num2 >= 0; num2--)
                {
                    if (PawnApparelGenerator.usableApparel[num2].Price > num || PawnApparelGenerator.workingSet.PairOverlapsAnything(PawnApparelGenerator.usableApparel[num2]))
                    {
                        PawnApparelGenerator.usableApparel.RemoveAt(num2);
                    }
                }
            }
        }
        private static bool CanUsePairCustom(List<string> pairs, ThingStuffPair pair, Pawn pawn, float moneyLeft, bool allowHeadgear, int fixedSeed)
        {
            if (pair.Price > moneyLeft)
            {
                return false;
            }
            if (!allowHeadgear && PawnApparelGenerator.IsHeadgear(pair.thing))
            {
                return false;
            }
            if (!pair.thing.apparel.CorrectGenderForWearing(pawn.gender))
            {
                return false;
            }
        
            if (!pairs.NullOrEmpty())
            {
                bool flag = false;
                for (int i = 0; i < pairs.Count; i++)
                {
                    for (int j = 0; j < pair.thing.apparel.tags.Count; j++)
                    {
                        if (pairs[i] == pair.thing.apparel.tags[j])
                        {
                            flag = true;
                            break;
                        }
                    }
                    if (flag)
                    {
                        break;
                    }
                }
                if (!flag)
                {
                    return false;
                }
            }
            if (!pawn.kindDef.apparelDisallowTags.NullOrEmpty())
            {
                for (int k = 0; k < pawn.kindDef.apparelDisallowTags.Count; k++)
                {
                    if (pair.thing.apparel.tags.Contains(pawn.kindDef.apparelDisallowTags[k]))
                    {
                        return false;
                    }
                }
            }
            if (pair.thing.generateAllowChance < 1f && !Rand.ChanceSeeded(pair.thing.generateAllowChance, fixedSeed ^ pair.thing.shortHash ^ 0x3D28557))
            {
                return false;
            }
            return true;
        }
        public static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            Commonality_Patch.pawnToLookInto = null;
        }
    }

    [HarmonyPatch(typeof(ThingStuffPair), "Commonality", MethodType.Getter)]
    public class Commonality_Patch
    {
        public static Pawn pawnToLookInto;
        public static bool Prefix(ThingStuffPair __instance, ref float __result)
        {
            var faction = pawnToLookInto?.Faction;
            if (faction != null)
            {
                if (faction.def.techLevel > TechLevel.Medieval)
                {
                    FactionTracker.Instance.SetNewTechLevelForFaction(faction.def);
                }
                if (faction.def.techLevel <= TechLevel.Medieval && Utils.GetTechLevelFor(__instance.thing) > TechLevel.Medieval)
                {
                    __result = 0f;
                    return false;
                }
            }
            return true;
        }
        public static void Postfix(ThingStuffPair __instance, ref float __result)
        {
            if (pawnToLookInto?.Faction != null && !pawnToLookInto.Faction.IsPlayer)
            {
                if (pawnToLookInto.Faction.def.techLevel <= TechLevel.Medieval && __instance.thing.IsRangedWeapon && __instance.thing.Verbs.Any(x => x.muzzleFlashScale > 0))
                {
                    if (__result > 0)
                    {
                        __result *= 0.25f;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Faction), "GetReportText", MethodType.Getter)]
    public static class GetReportText_Patch
    {
        public static void Postfix(Faction __instance, ref string __result)
        {
            if (__instance.def.techLevel > TechLevel.Medieval)
            {
                FactionTracker.Instance.SetNewTechLevelForFaction(__instance.def);
            }
            __result += "\n\n" + "RM.FactionTechLevelInfo".Translate(__instance.def.techLevel.ToStringHuman());
        }
    }


    [HarmonyPatch(typeof(ResearchProjectDef), "CanStartNow", MethodType.Getter)]
    public static class CanStartNow_Patch
    {
        public static void Postfix(ResearchProjectDef __instance, ref bool __result)
        {
            if (__result && !RimedievalMod.settings.disableTechRestriction)
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
                __result = __result.GetAllowedProjectDefs();
            }
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
    public class ResearchPal_Patch
    {
        private static bool Prepare()
        {
            return ModLister.HasActiveModWithName("ResearchPal") || ModLister.HasActiveModWithName("ResearchPal - Forked");
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var meth = AccessTools.Method("ResearchPal.Tree:PopulateNodes");
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
                var alloweedDefs = list.GetAllowedProjectDefs();
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
                    __result = false;
                }
                else if (other == Faction.OfMechanoids && fac.IsPlayer)
                {
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
        [HarmonyPriority(Priority.Last)]
        public static IEnumerable<Blueprint_Build> Postfix(IEnumerable<Blueprint_Build> __result, float points, Map map, Faction ___faction, IntVec3 ___center)
        {
            var list = PlaceArtilleryBlueprints(ref points, map, ___faction, ___center);
            foreach (var r in list)
            {
                yield return r;
            }
        }
    
        private static bool IsMedievalArtillery(ThingDef thingDef)
        {
            if (thingDef.building?.turretGunDef != null)
            {
                if (thingDef.building.buildingTags.Contains("ArtilleryMedieval_BaseDestroyer") || thingDef.building.buildingTags.Contains("ArtilleryMedieval"))
                {
                    return true;
                }
                //var defName = thingDef.defName.ToLower();
                //if (defName.Contains("trebuchet") || defName.Contains("ballista"))
                //{
                //    return true;
                //}
            }
            return false;
        }
        public static List<Blueprint_Build> PlaceArtilleryBlueprints(ref float points, Map map, Faction ___faction, IntVec3 ___center)
        {
            var list = new List<Blueprint_Build>();
            IEnumerable<ThingDef> artyDefs = DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => IsMedievalArtillery(def));
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
            else
            {
                Log.Message("Failed to find arty");
            }
            points = 0;
            return list;
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

    [HarmonyPatch(typeof(MonumentMarker), "FirstDisallowedBuilding", MethodType.Getter)]
    public class Patch_FirstDisallowedBuilding
    {
        public static bool Prefix(MonumentMarker __instance)
        {
            if (__instance is NewCityMarker)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_ShipChunkDrop), "CanFireNowSub")]
    public class Patch_CanFireNowSub
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(StorytellerComp_ShipChunkDrop), "MakeIntervalIncidents")]
    public class Patch_MakeIntervalIncidents
    {
        [HarmonyPriority(Priority.Last)]
        public static IEnumerable<FiringIncident> Postfix(IEnumerable<FiringIncident> __result)
        {
            yield break;
        }
    }

    [HarmonyPatch(typeof(SymbolResolver_AncientCryptosleepCasket), "Resolve")]
    public class SymbolResolver_AncientCryptosleepCasket_Resolve
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var method = AccessTools.Method(typeof(ThingMaker), "MakeThing");
            var methodToCall = AccessTools.Method(typeof(SymbolResolver_AncientCryptosleepCasket_Resolve), "GetRandomStuff");
            bool found = false;
            for (int i = 0; i < codes.Count; i++)
            {
                if (!found && codes[i].opcode == OpCodes.Ldnull && codes[i + 1].Calls(method))
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Call, methodToCall);
                }
                else
                {
                    yield return codes[i];
                }
            }
        }

        public static ThingDef GetRandomStuff()
        {
            return GenStuff.RandomStuffFor(ThingDefOf.AncientCryptosleepCasket);
        }
    }
}
