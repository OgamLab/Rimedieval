﻿using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

            MethodInfo allowedPrecepts = AccessTools.Method(typeof(HarmonyPatches), nameof(HarmonyPatches.AllowedPrecepts));
            foreach (Type preceptWorkerType in typeof(PreceptWorker).AllSubclasses().AddItem(typeof(PreceptWorker)))
            {
                try
                {
                    MethodInfo method = AccessTools.Method(preceptWorkerType, "get_ThingDefs");
                    RimedievalMod.harmony.Patch(method, null, postfix: new HarmonyMethod(allowedPrecepts));
                }
                catch
                {
                }
            }
            MethodInfo filterItems = AccessTools.Method(typeof(HarmonyPatches), nameof(HarmonyPatches.FilterItems));
            foreach (var subType in typeof(ThingSetMaker).AllSubclasses())
            {
                try
                {
                    var method = AccessTools.Method(subType, "Generate", new Type[] { typeof(ThingSetMakerParams), typeof(List<Thing>) });
                    RimedievalMod.harmony.Patch(method, null, postfix: new HarmonyMethod(filterItems));
                }
                catch { }
            }
        }

        public static IEnumerable<PreceptThingChance> AllowedPrecepts(IEnumerable<PreceptThingChance> __result)
        {
            return __result.Where(x => x.def.IsAllowedForRimedieval() && x.chance > 0);
        }

        public static void FilterItems(ThingSetMakerParams __0, List<Thing> __1)
        {
            var count = __1.RemoveAll(x => x.def.IsAllowedForRimedieval() is false);
            if (count > 0)
            {
                Log.Message("Removed " + count);
            }
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



    [HarmonyPatch(typeof(SymbolResolver_SingleThing), "Resolve")]
    public static class SymbolResolver_SingleThing_Resolve_Patch
    {
        public static bool Prefix(ResolveParams rp)
        {
            if (rp.singleThingDef.IsAllowedForRimedieval() is false || rp.singleThingToSpawn != null && rp.singleThingToSpawn.def.IsAllowedForRimedieval() is false)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PawnWeaponGenerator), "TryGenerateWeaponFor")]
    public class PawnWeaponGenerator_TryGenerateWeaponFor
    {
        public static bool generatingWeapons;
        public static bool shouldBeMelee;
        [HarmonyPriority(0)]
        public static void Prefix(Pawn pawn, PawnGenerationRequest request)
        {
            generatingWeapons = true;
            Commonality_Patch.pawnToLookInto = pawn;
            if (Rand.Chance(0.25f))
            {
                shouldBeMelee = false;
            }
            else
            {
                shouldBeMelee = true;
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = instructions.ToList();
            MethodInfo method = AccessTools.Method(typeof(Pawn_EquipmentTracker), "AddEquipment");
            FieldInfo pawnField = AccessTools.Field(typeof(PawnWeaponGenerator).GetNestedTypes(AccessTools.all).First(c => c.Name.Contains("c__DisplayClass5_0")), "pawn");
            FieldInfo workingWeaponsField = AccessTools.Field(typeof(PawnWeaponGenerator), "workingWeapons");
            MethodInfo methodToCall = AccessTools.Method(typeof(PawnWeaponGenerator_TryGenerateWeaponFor), "TryGenerateWeaponForOverride");
            MethodInfo randomWeight = AccessTools.Method(typeof(GenCollection), "TryRandomElementByWeight", generics: new[] { typeof(ThingStuffPair) });
            bool patched = false;
            Label label = generator.DefineLabel();
            for (int i = 0; i < codes.Count; i++)
            {
                yield return i > 1 && codes[i - 1].Calls(randomWeight) ? new CodeInstruction(OpCodes.Brfalse_S, label) : codes[i];

                if (codes[i].Calls(method))
                {
                    int ldsFieldInd = codes.FirstIndexOf(x => codes.IndexOf(x) > i && x.LoadsField(workingWeaponsField));
                    yield return new CodeInstruction(OpCodes.Br_S, codes[ldsFieldInd].labels.First());
                    yield return new CodeInstruction(OpCodes.Ldloc_0).WithLabels(label);
                    yield return new CodeInstruction(OpCodes.Ldfld, pawnField);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, methodToCall);
                    patched = true;
                }
            }
            if (!patched)
            {
                Log.Error("Rimedieval failed to patch TryGenerateWeaponFor");
            }
        }
        public static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            Commonality_Patch.pawnToLookInto = null;
            generatingWeapons = false;
        }

        public static void TryGenerateWeaponForOverride(Pawn pawn, PawnGenerationRequest request)
        {
            PawnWeaponGenerator.workingWeapons.Clear();
            if (pawn.kindDef.weaponTags == null || pawn.kindDef.weaponTags.Count == 0 || !pawn.RaceProps.ToolUser || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return;
            }
            HashSet<string> weaponTags = shouldBeMelee ? Utils.medievalMeleeWeaponTags : Utils.medievalRangeWeaponTags;
            float randomInRange = pawn.kindDef.weaponMoney.RandomInRange;
            for (int i = 0; i < PawnWeaponGenerator.allWeaponPairs.Count; i++)
            {
                ThingStuffPair w2 = PawnWeaponGenerator.allWeaponPairs[i];
                if (!(w2.Price > randomInRange) && DefCleaner.GetTechLevelFor(w2.thing) <= (pawn.Faction?.def?.techLevel ?? TechLevel.Medieval)
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
            if (PawnWeaponGenerator.workingWeapons.TryRandomElementByWeight((ThingStuffPair w) => w.Commonality 
            * w.Price * PawnWeaponGenerator.GetWeaponCommonalityFromIdeo(pawn, w), out ThingStuffPair result))
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
                float num = (request.BiocodeWeaponChance > 0f) ? request.BiocodeWeaponChance : pawn.kindDef.biocodeWeaponChance;
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
            List<CodeInstruction> codes = instructions.ToList();
            FieldInfo workingSetField = AccessTools.Field(typeof(PawnApparelGenerator), "workingSet");
            MethodInfo giveToPawnMeth = AccessTools.Method(typeof(PawnApparelGenerator.PossibleApparelSet), "GiveToPawn");
            MethodInfo methodToCall = AccessTools.Method(typeof(Patch_GenerateStartingApparelFor), "TryGenerateStartingApparelForOverride");
            bool patched = false;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(workingSetField) && codes[i + 3].Calls(giveToPawnMeth))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_1).MoveLabelsFrom(codes[i]);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, methodToCall);
                    patched = true;
                }
                yield return codes[i];
            }
            if (!patched)
            {
                Log.Error("Rimedieval failed to patch GenerateStartingApparelFor");
            }
        }

        public static void TryGenerateStartingApparelForOverride(float randomInRange, Pawn pawn, PawnGenerationRequest request)
        {
            if ((!PawnApparelGenerator.workingSet.IsNaked(pawn.gender) && !pawn.RaceProps.ToolUser) || !pawn.RaceProps.IsFlesh)
            {
                return;
            }
            randomInRange -= PawnApparelGenerator.workingSet.TotalPrice;
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

            NeededWarmth neededWarmth = PawnApparelGenerator.ApparelWarmthNeededNow(pawn, request, out float mapTemperature);
            bool allowHeadgear = Rand.Value < pawn.kindDef.apparelAllowHeadgearChance;

            int @int = Rand.Int;
            PawnApparelGenerator.tmpApparelCandidates.Clear();
            List<string> pairs = pawn.kindDef.apparelTags?.Any() ?? false ? pawn.kindDef.IsWarrior() ? Utils.medievalArmorTags : Utils.medievalApparelTags : new List<string>();

            for (int i = 0; i < PawnApparelGenerator.allApparelPairs.Count; i++)
            {
                ThingStuffPair thingStuffPair = PawnApparelGenerator.allApparelPairs[i];
                if (CanUsePairCustom(pairs, thingStuffPair, pawn, randomInRange, allowHeadgear, @int))
                {
                    PawnApparelGenerator.tmpApparelCandidates.Add(thingStuffPair);
                }
            }
            int num = 0;
            if (randomInRange < 0.001f)
            {
                GenerateWorkingPossibleApparelSetForOverride(isOverwritten, pawn, randomInRange, PawnApparelGenerator.tmpApparelCandidates);
            }
            else
            {
                num = 0;
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
            if ((!pawn.kindDef.apparelIgnoreSeasons || request.ForceAddFreeWarmLayerIfNeeded) 
                && !PawnApparelGenerator.workingSet.SatisfiesNeededWarmth(neededWarmth, mustBeSafe: true, mapTemperature))
            {
                PawnApparelGenerator.workingSet.AddFreeWarmthAsNeeded(neededWarmth, mapTemperature, pawn);
            }
            if (ModsConfig.BiotechActive && !pawn.kindDef.apparelIgnorePollution && num > 0.05f 
                && !PawnApparelGenerator.workingSet.SatisfiesNeededToxicEnvironmentResistance(num))
            {
                PawnApparelGenerator.workingSet.AddFreeToxicEnvironmentResistanceAsNeeded(num, delegate (ThingStuffPair pa)
                {
                    if (!pa.thing.apparel.CorrectAgeForWearing(pawn))
                    {
                        return false;
                    }
                    if (pawn.kindDef.apparelIgnoreSeasons && !request.ForceAddFreeWarmLayerIfNeeded)
                    {
                        return true;
                    }
                    return (!(PawnApparelGenerator.workingSet.GetReplacedInsulationCold(pa) > pa.InsulationCold)) ? true : false;
                });
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
                    if ((!att[j].RequiredTag.NullOrEmpty() || !att[j].AlternateTagChoices.NullOrEmpty()) && PawnApparelGenerator.allApparelPairs.Where((ThingStuffPair pa) => PawnApparelGenerator.ApparelRequirementTagsMatch(att[j], pa.thing) && PawnApparelGenerator.ApparelRequirementHandlesThing(att[j], pa.thing) && PawnApparelGenerator.CanUseStuff(pawn, pa) && pa.thing.apparel.CorrectGenderForWearing(pawn.gender) && !PawnApparelGenerator.workingSet.PairOverlapsAnything(pa)).TryRandomElementByWeight((ThingStuffPair pa) => pa.Commonality, out ThingStuffPair result))
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
                    && !PawnApparelGenerator.workingSet.PairOverlapsAnything(pa)).TryRandomElementByWeight((ThingStuffPair pa) => pa.Commonality, out ThingStuffPair result2))
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
            while ((pawn.Ideo == null || !pawn.Ideo.IdeoPrefersNudityForGender(pawn.gender) || (pawn.Faction != null && pawn.Faction.IsPlayer)) && (!(Rand.Value < 0.1f) || !(money < 9999999f)) && PawnApparelGenerator.usableApparel.Where((ThingStuffPair pa) => PawnApparelGenerator.CanUseStuff(pawn, pa)).TryRandomElementByWeight((ThingStuffPair pa) => pa.Commonality, out ThingStuffPair result3))
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
            return pair.thing.generateAllowChance >= 1f || Rand.ChanceSeeded(pair.thing.generateAllowChance, fixedSeed ^ pair.thing.shortHash ^ 0x3D28557);
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
            Faction faction = pawnToLookInto?.Faction;
            if (faction != null)
            {
                if (faction.def.techLevel > TechLevel.Medieval)
                {
                    FactionTracker.Instance.SetNewTechLevelForFaction(faction.def);
                }
                if (faction.def.techLevel <= TechLevel.Medieval && DefCleaner.GetTechLevelFor(__instance.thing) > TechLevel.Medieval)
                {
                    __result = 0f;
                    return false;
                }
            }
            if (PawnWeaponGenerator_TryGenerateWeaponFor.generatingWeapons)
            {
                if (PawnWeaponGenerator_TryGenerateWeaponFor.shouldBeMelee && __instance.thing.IsRangedWeapon) 
                {
                    __result = 0f;
                    return false;
                }
                else if (PawnWeaponGenerator_TryGenerateWeaponFor.shouldBeMelee is false && __instance.thing.IsMeleeWeapon)
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




    [HarmonyPatch(typeof(MainTabWindow_Research), "VisibleResearchProjects", MethodType.Getter)]
    public static class VisibleResearchProjects_Patch
    {
        public static void Postfix(ref List<ResearchProjectDef> __result)
        {
            __result = __result.GetAllowedProjectDefs();
        }
    }

    [HarmonyPatch]
    public class ResearchPal_Patch
    {
        static List<MethodInfo> meths = new List<MethodInfo>();
        private static bool Prepare()
        {
            var meth = AccessTools.Method("ResearchPal.Tree:PopulateNodes");
            if (meth != null)
            {
                meths.Add(meth);
            }
            meth = AccessTools.Method("ResearchPowl.Tree:PopulateNodes");
            if (meth != null)
            {
                meths.Add(meth);
            }
            return meths.Any();
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var meth in meths)
            {
                yield return meth;
            }
        }
        public static void Prefix(out List<ResearchProjectDef> __state)
        {
            __state = new List<ResearchProjectDef>();
            List<ResearchProjectDef> list = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            List<ResearchProjectDef> alloweedDefs = list.GetAllowedProjectDefs();
            for (int num = list.Count - 1; num >= 0; num--)
            {
                ResearchProjectDef def = list[num];
                if (!alloweedDefs.Contains(def))
                {
                    __state.Add(def);
                    RemoveDef(def);
                }
            }
        }
        public static void Postfix(List<ResearchProjectDef> __state)
        {
            foreach (ResearchProjectDef def in __state)
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

    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival))]
    [HarmonyPatch("TryExecuteWorker")]
    public static class IncidentWorker_OrbitalTraderArrival_TryExecuteWorkerPatch
    {
        public static bool Prefix(IncidentParms parms)
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy))]
    [HarmonyPatch("TryExecuteWorker")]
    public static class TryExecuteWorkerPatch
    {
        public static bool Prefix(IncidentParms parms)
        {
            return parms.faction == null || parms.faction.def != FactionDefOf.Mechanoid || !RimedievalMod.settings.disableMechanoids;
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
            return !RimedievalMod.settings.disableMechanoids;
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
            foreach (IncidentDef r in __result)
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
            List<CodeInstruction> codes = instructions.ToList();
            MethodInfo method = AccessTools.Method(typeof(ThingMaker), "MakeThing");
            MethodInfo methodToCall = AccessTools.Method(typeof(SymbolResolver_AncientCryptosleepCasket_Resolve), "GetRandomStuff");
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
