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
    public static class Utils
    {
        public static readonly Dictionary<ThingDef, TechLevel> thingsByTechLevels = new Dictionary<ThingDef, TechLevel>
        {
            {ThingDefOf.ComponentIndustrial, TechLevel.Industrial },
            {ThingDefOf.ComponentSpacer, TechLevel.Spacer },
            {ThingDefOf.Plasteel, TechLevel.Spacer },
            {ThingDefOf.Hyperweave, TechLevel.Spacer },

        };
        public static TechLevel GetTechLevelFor(ThingDef thingDef)
        {
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
