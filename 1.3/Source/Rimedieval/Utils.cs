using System;
using System.Collections.Generic;
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

        public static HashSet<string> medievalArmors = new HashSet<string>();
        public static HashSet<string> medievalApparels = new HashSet<string>();

        static Utils()
        {
            AssignApparelLists();
            AssignWeaponLists();
        }
        private static void AssignApparelLists()
        {
            List<ThingStuffPair> allApparelPairs = PawnApparelGenerator.allApparelPairs;
            foreach (var apparel in allApparelPairs)
            {
                if (GetTechLevelFor(apparel.thing) <= TechLevel.Medieval)
                {
                    if (apparel.thing.IsArmor())
                    {
                        foreach (var tag in apparel.thing.apparel.tags)
                        {
                            medievalArmors.Add(tag);
                        }
                    }
                    else
                    {
                        foreach (var tag in apparel.thing.apparel.tags)
                        {
                            medievalApparels.Add(tag);
                        }
                    }
                }
            }

        }

        private static bool IsArmor(this ThingDef apparelDef)
        {
            return apparelDef.statBases.Any(x => x.stat == StatDefOf.ArmorRating_Blunt && x.value > 0 
            || x.stat == StatDefOf.ArmorRating_Sharp && x.value > 0
            || x.stat == StatDefOf.StuffEffectMultiplierArmor && x.value >= 0.5f) || apparelDef.thingCategories.Contains(ThingCategoryDefOf.ApparelArmor);
        }

        public static bool IsWarrior(this PawnKindDef pawnKindDef)
        {
            if (pawnKindDef.apparelTags.NullOrEmpty())
            {
                return true;
            }
            var armorApparels = new List<ThingStuffPair>();
            var regularApparels = new List<ThingStuffPair>();
            foreach (var apparel in PawnApparelGenerator.allApparelPairs)
            {
                foreach (var tag in pawnKindDef.apparelTags)
                {
                    if (apparel.thing.apparel.tags.Contains(tag))
                    {
                        if (apparel.thing.IsArmor())
                        {
                            armorApparels.Add(apparel);
                        }
                        else
                        {
                            regularApparels.Add(apparel);
                        }
                    }
                }
            }

            return armorApparels.Count >= regularApparels.Count;
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
            var meleeWeapons = new List<ThingStuffPair>();
            var rangeWeapons = new List<ThingStuffPair>();
            foreach (var weapon in PawnWeaponGenerator.allWeaponPairs)
            {
                foreach (var tag in pawnKindDef.weaponTags)
                {
                    if (weapon.thing.weaponTags.Contains(tag))
                    {
                        if (weapon.thing.IsRangedWeapon)
                        {
                            rangeWeapons.Add(weapon);
                        }
                        else
                        {
                            meleeWeapons.Add(weapon);
                        }
                    }
                }
            }
            return meleeWeapons.Count >= rangeWeapons.Count;
        }

        public static readonly Dictionary<ThingDef, TechLevel> thingsByTechLevels = new Dictionary<ThingDef, TechLevel>
        {
            {ThingDefOf.ComponentIndustrial, TechLevel.Industrial },
            {ThingDefOf.ComponentSpacer, TechLevel.Spacer },
            {ThingDefOf.Plasteel, TechLevel.Spacer },
            {ThingDefOf.Hyperweave, TechLevel.Spacer },
        };

        public static bool IsAllowed(this ThingDef thingDef)
        {
            var defName = thingDef.defName;
            if (defName.Contains("Psytrainer") || defName.Contains("Neurotrainer"))
            {
                return true;
            }
            return false;
        }
        public static TechLevel GetTechLevelFor(ThingDef thingDef)
        {
            if (thingDef.GetCompProperties<CompProperties_Techprint>() != null)
            {
                return thingDef.GetCompProperties<CompProperties_Techprint>().project.techLevel;
            }
            if (thingsByTechLevels.TryGetValue(thingDef, out var level))
            {
                return level;
            }
            if (thingDef.recipeMaker != null)
            {
                if (thingDef.recipeMaker.researchPrerequisite != null)
                {
                    var techLevel = thingDef.recipeMaker.researchPrerequisite.techLevel;
                    if (techLevel != TechLevel.Undefined)
                    {
                        return techLevel;
                    }
                }
                if (thingDef.recipeMaker.researchPrerequisites?.Any() ?? false)
                {
                    var num = thingDef.recipeMaker.researchPrerequisites.MaxBy(x => (int)x.techLevel).techLevel;
                    var techLevel = (TechLevel)num;
                    if (techLevel != TechLevel.Undefined)
                    {
                        return techLevel;
                    }
                }
            }
            if (thingDef.researchPrerequisites?.Any() ?? false)
            {
                var num = thingDef.researchPrerequisites.MaxBy(x => (int)x.techLevel).techLevel;
                var techLevel = (TechLevel)num;
                if (techLevel != TechLevel.Undefined)
                {
                    return techLevel;
                }
            }
            if (thingDef.techLevel == TechLevel.Undefined && (thingDef.costList?.Any() ?? false))
            {
                var maxTechMaterial = thingDef.costList.MaxBy(x => GetTechLevelFor(x.thingDef));
                var techLevel = GetTechLevelFor(maxTechMaterial.thingDef);
                return techLevel;
            }
            return thingDef.techLevel;
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
