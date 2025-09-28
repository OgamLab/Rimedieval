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
            AssignApparelLists();
            AssignWeaponLists();
            DoubleResearchCostAfterElectricity();
            stopwatch.Stop();
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

                if (DefCleaner.GetTechLevelFor(thingDef) <= TechLevel.Medieval)
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
                if (DefCleaner.GetTechLevelFor(weapon.thing) <= TechLevel.Medieval)
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
    }
}
