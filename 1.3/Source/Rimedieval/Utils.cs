using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimedieval
{
    [StaticConstructorOnStartup]
    public static class Utils
    {
        public static HashSet<string> medievalMeleeWeaponTags = new HashSet<string>();
        public static HashSet<string> medievalRangeWeaponTags = new HashSet<string>();

        public static List<string> medievalArmorTags = new List<string>();
        public static List<string> medievalApparelTags = new List<string>();

        public static Dictionary<string, List<ThingDef>> apparelsByTags = new Dictionary<string, List<ThingDef>>();

        public static HashSet<ThingDef> armoredApparels = new HashSet<ThingDef>();

        public static List<ThingDef> allApparels;
        static Utils()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Log.Message("Rimedieval is starting apparel caching");
            AssignApparelLists();
            AssignWeaponLists();
            DoubleResearchCostAfterElectricity();
            stopwatch.Stop();
            Log.Message("Cache is completed! It took " + stopwatch.Elapsed);
            if (ModsConfig.IdeologyActive)
            {
                DoIdeologyPatches();
            }
        }

        private static List<string> preceptsToRemove = new List<string>
        {
            "SleepAccelerator_Preferred",
            "NeuralSupercharge_Preferred",
            "Biosculpting_Accelerated",
            "AgeReversal_Demanded",
            "BioSculpter_Despised",
            "NutrientPasteEating_DontMind",
            "NutrientPasteEating_Disgusting",
        };
        public static void DoIdeologyPatches()
        {
            foreach (var def in DefDatabase<PreceptDef>.defsList.Where(x => preceptsToRemove.Contains(x.defName)))
            {
                foreach (var meme in def.requiredMemes)
                {
                    meme.requireOne.RemoveAll(x => x.Any(y => preceptsToRemove.Contains(y.defName)));
                }
                def.requiredMemes.Clear();
            }
            DefDatabase<PreceptDef>.defsList.RemoveAll(x => preceptsToRemove.Contains(x.defName));

        }
        public static IEnumerable<Thing> GetAllowedThings(this IEnumerable<Thing> things)
        {
            foreach (var thing in things)
            {
                if (thing.def.IsAllowed())
                {
                    yield return thing;
                }
            }
        }
        public static IEnumerable<ThingDef> GetAllowedThingDefs(this IEnumerable<ThingDef> things)
        {
            foreach (var def in things)
            {
                if (def.IsAllowed())
                {
                    yield return def;
                }
            }
        }
        public static bool IsAllowed(this ThingDef def)
        {
            var techLevel = GetTechLevelFor(def);
            if (techLevel < TechLevel.Industrial)
            {
                return true;
            }
            var defName = def.defName;
            if (defName.Contains("Psytrainer") || defName.Contains("Neurotrainer"))
            {
                return true;
            }
            return false;
        }
        private static void DoubleResearchCostAfterElectricity()
        {
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (def.ContainsTechProjectAsPrerequisite(RimedievalDefOf.Electricity))
                {
                    def.baseCost *= 2;
                }
            }
        }

        public static List<ResearchProjectDef> GetAllowedProjectDefs(this List<ResearchProjectDef> list)
        {
            if (RimedievalMod.settings.restrictTechToMedievalOnly)
            {
                list = list.Where(x => x.techLevel <= TechLevel.Medieval).ToList();
            }
            else
            {
                var microElectronics = DefDatabase<ResearchProjectDef>.GetNamed("MicroelectronicsBasics");
                list = list.Where(x => x.techLevel <= TechLevel.Industrial && x != microElectronics && !x.ContainsTechProjectAsPrerequisite(microElectronics)).ToList();
            }
            return list;
        }
        private static void AssignApparelLists()
        {
            allApparels = DefDatabase<ThingDef>.AllDefs.Where(x => x.IsApparel).ToList();
            foreach (var thingDef in allApparels)
            {
                if (thingDef.statBases != null && thingDef.statBases.Any(x => x.stat == StatDefOf.ArmorRating_Blunt && x.value > 0
                    || x.stat == StatDefOf.ArmorRating_Sharp && x.value > 0
                        || x.stat == StatDefOf.StuffEffectMultiplierArmor && x.value >= 0.5f) || thingDef.thingCategories != null && thingDef.thingCategories.Contains(ThingCategoryDefOf.ApparelArmor))
                {
                    armoredApparels.Add(thingDef);
                }

                if (GetTechLevelFor(thingDef) <= TechLevel.Medieval)
                {
                    if (thingDef.IsArmor())
                    {
                        foreach (var tag in thingDef.apparel.tags)
                        {
                            if (apparelsByTags.ContainsKey(tag))
                            {
                                if (!apparelsByTags[tag].Contains(thingDef))
                                {
                                    apparelsByTags[tag].Add(thingDef);
                                }
                            }
                            else
                            {
                                apparelsByTags[tag] = new List<ThingDef> { thingDef };
                            }
                            if (!medievalArmorTags.Contains(tag))
                            {
                                medievalArmorTags.Add(tag);
                            }
                        }
                    }
                    else
                    {
                        foreach (var tag in thingDef.apparel.tags)
                        {
                            if (apparelsByTags.ContainsKey(tag))
                            {
                                if (!apparelsByTags[tag].Contains(thingDef))
                                {
                                    apparelsByTags[tag].Add(thingDef);
                                }
                            }
                            else
                            {
                                apparelsByTags[tag] = new List<ThingDef> { thingDef };
                            }
                            if (!medievalApparelTags.Contains(tag))
                            {
                                medievalApparelTags.Add(tag);
                            }
                        }
                    }
                }
            }

        }

        private static bool IsArmor(this ThingDef apparelDef)
        {
            return armoredApparels.Contains(apparelDef);
        }
        public static bool IsWarrior(this PawnKindDef pawnKindDef)
        {
            var armoredCount = 0;
            var regularCount = 0;

            foreach (var tag in pawnKindDef.apparelTags)
            {
                if (apparelsByTags.TryGetValue(tag, out var list))
                {
                    foreach (var thingDef in list)
                    {
                        if (thingDef.IsArmor())
                        {
                            armoredCount++;
                        }
                        else
                        {
                            regularCount++;
                        }
                    }
                }
            }

            return armoredCount >= regularCount;
        }
        private static void AssignWeaponLists()
        {
            List<ThingStuffPair> allWeaponPairs = PawnWeaponGenerator.allWeaponPairs;
            foreach (var weapon in allWeaponPairs)
            {
                if (GetTechLevelFor(weapon.thing) <= TechLevel.Medieval)
                {
                    if (weapon.thing.IsRangedWeapon)
                    {
                        foreach (var tag in weapon.thing.weaponTags)
                        {
                            medievalRangeWeaponTags.Add(tag);
                        }
                    }
                    else
                    {
                        foreach (var tag in weapon.thing.weaponTags)
                        {
                            medievalMeleeWeaponTags.Add(tag);
                        }
                    }
                }
            }
        }
        public static bool IsMelee(this PawnKindDef pawnKindDef)
        {
            if (pawnKindDef.weaponTags.NullOrEmpty())
            {
                return true;
            }
            var meleeWeapons = 0;
            var rangeWeapons = 0;
            foreach (var weapon in PawnWeaponGenerator.allWeaponPairs)
            {
                foreach (var tag in pawnKindDef.weaponTags)
                {
                    if (weapon.thing.weaponTags.Contains(tag))
                    {
                        if (weapon.thing.IsRangedWeapon)
                        {
                            rangeWeapons++;
                        }
                        else
                        {
                            meleeWeapons++;
                        }
                    }
                }
            }
            return meleeWeapons >= rangeWeapons;
        }

        public static readonly Dictionary<ThingDef, TechLevel> thingsByTechLevels = new Dictionary<ThingDef, TechLevel>
        {
            {ThingDefOf.Chemfuel, TechLevel.Industrial },
            {ThingDefOf.ComponentIndustrial, TechLevel.Industrial },
            {ThingDefOf.ComponentSpacer, TechLevel.Spacer },
            {ThingDefOf.Plasteel, TechLevel.Spacer },
            {ThingDefOf.Hyperweave, TechLevel.Spacer },
        };

        private static Dictionary<ThingDef, TechLevel> cachedTechLevelValues = new Dictionary<ThingDef, TechLevel>();
        public static TechLevel GetTechLevelFor(ThingDef thingDef)
        {
            if (!cachedTechLevelValues.TryGetValue(thingDef, out TechLevel techLevel))
            {
                cachedTechLevelValues[thingDef] = techLevel = GetTechLevelForInt(thingDef);
            }
            return techLevel;
        }
        private static TechLevel GetTechLevelForInt(ThingDef thingDef)
        {
            List<TechLevel> techLevelSources = new List<TechLevel>();
            if (thingDef.GetCompProperties<CompProperties_Techprint>() != null)
            {
                //Log.Message("0 Result: " + thingDef.GetCompProperties<CompProperties_Techprint>().project.techLevel + " - " + thingDef);
                techLevelSources.Add(thingDef.GetCompProperties<CompProperties_Techprint>().project.techLevel);
            }

            if (thingsByTechLevels.TryGetValue(thingDef, out var level))
            {
                //Log.Message("1 Result: " + level + " - " + thingDef);
                techLevelSources.Add(level);
            }
            if (thingDef.recipeMaker != null)
            {
                if (thingDef.recipeMaker.researchPrerequisite != null)
                {
                    var techLevel = thingDef.recipeMaker.researchPrerequisite.techLevel;
                    if (techLevel != TechLevel.Undefined)
                    {
                        //Log.Message("2 Result: " + techLevel + " - " + thingDef);
                        techLevelSources.Add(techLevel);
                    }
                }
                if (thingDef.recipeMaker.researchPrerequisites?.Any() ?? false)
                {
                    var num = thingDef.recipeMaker.researchPrerequisites.MaxBy(x => (int)x.techLevel).techLevel;
                    var techLevel = (TechLevel)num;
                    if (techLevel != TechLevel.Undefined)
                    {
                        //Log.Message("3 Result: " + techLevel + " - " + thingDef);
                        techLevelSources.Add(techLevel);
                    }
                }
                if (thingDef.recipeMaker.recipeUsers?.Any() ?? false)
                {
                    List<TechLevel> techLevels = new List<TechLevel>();
                    foreach (var recipeUser in thingDef.recipeMaker.recipeUsers)
                    {
                        techLevels.Add(GetTechLevelFor(recipeUser));
                    }
                    var minTechLevel = techLevels.Min();
                    if (minTechLevel != TechLevel.Undefined)
                    {
                        //Log.Message("4 Result: " + minTechLevel + " - " + thingDef);
                        techLevelSources.Add(minTechLevel);
                    }
                }
            }
            if (thingDef.researchPrerequisites?.Any() ?? false)
            {
                var num = thingDef.researchPrerequisites.MaxBy(x => (int)x.techLevel).techLevel;
                var techLevel = (TechLevel)num;
                if (techLevel != TechLevel.Undefined)
                {
                    //Log.Message("5 Result: " + techLevel + " - " + thingDef);
                    techLevelSources.Add(techLevel);
                }
            }
            if (thingDef.techLevel == TechLevel.Undefined && (thingDef.costList?.Any() ?? false))
            {
                var maxTechMaterial = thingDef.costList.MaxBy(x => GetTechLevelFor(x.thingDef));
                var techLevel = GetTechLevelFor(maxTechMaterial.thingDef);
                //Log.Message("6 Result: " + techLevel + " - " + thingDef);
                techLevelSources.Add(techLevel);
            }
            //Log.Message("7 Result: " + thingDef.techLevel + " - " + thingDef);
            //Log.ResetMessageCount();
            techLevelSources.Add(thingDef.techLevel);
            Log.Message(thingDef + " - FINAL: " + techLevelSources.Max());
            return techLevelSources.Max();
        }
        public static bool ContainsTechProjectAsPrerequisite(this ResearchProjectDef def, ResearchProjectDef techProject)
        {
            if (def.prerequisites != null)
            {
                for (int i = 0; i < def.prerequisites.Count; i++)
                {
                    if (def.prerequisites[i] == techProject)
                    {
                        return true;
                    }
                    else if (ContainsTechProjectAsPrerequisite(def.prerequisites[i], techProject))
                    {
                        return true;
                    }
                }
            }
            if (def.hiddenPrerequisites != null)
            {
                for (int j = 0; j < def.hiddenPrerequisites.Count; j++)
                {
                    if (def.hiddenPrerequisites[j] == techProject)
                    {
                        return true;
                    }
                    else if (ContainsTechProjectAsPrerequisite(def.hiddenPrerequisites[j], techProject))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
