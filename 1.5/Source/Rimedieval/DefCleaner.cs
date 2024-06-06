using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Rimedieval
{
    public static class DefCleaner
    {
        public static readonly Dictionary<ThingDef, TechLevel> thingsByTechLevels = new Dictionary<ThingDef, TechLevel>
        {
            {ThingDefOf.Chemfuel, TechLevel.Industrial },
            {ThingDefOf.ComponentIndustrial, TechLevel.Industrial },
            {ThingDefOf.ComponentSpacer, TechLevel.Spacer },
            {ThingDefOf.Plasteel, TechLevel.Spacer },
            {RimedievalDefOf.Hyperweave, TechLevel.Spacer },
            {ThingDefOf.ReinforcedBarrel, TechLevel.Industrial }
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
            //Log.Message(thingDef + " - FINAL: " + techLevelSources.Max());
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
        public static IEnumerable<Thing> GetAllowedThings(this IEnumerable<Thing> things)
        {
            foreach (var thing in things)
            {
                if (thing.def.IsAllowedForRimedieval())
                {
                    yield return thing;
                }
            }
        }
        public static IEnumerable<ThingDef> GetAllowedThingDefs(this IEnumerable<ThingDef> things)
        {
            foreach (var def in things)
            {
                if (def.IsAllowedForRimedieval())
                {
                    yield return def;
                }
            }
        }

        public static bool MedievalGenepacksModIsActive = ModsConfig.IsActive("DankPyon.MedievalGenepacks");
        public static bool IsAllowedForRimedieval(this ThingDef thingDef)
        {
            if (thingDef is null) return true;
            var defName = thingDef.defName;
            if (MedievalGenepacksModIsActive is false)
            {
                if (defName == "Genepack" || defName == "ArchiteCapsule")
                {
                    return false;
                }
            }

            //if (defName == "DankPyon_QuestFinder")
            //{
            //    return true;
            //}

            if (defName.Contains("Psytrainer") || defName.Contains("Neurotrainer"))
            {
                return true;
            }

            var allowedResearchProjects = DefDatabase<ResearchProjectDef>.AllDefsListForReading.GetAllowedProjectDefs();
            if (thingDef.recipeMaker != null)
            {
                if (thingDef.recipeMaker.researchPrerequisite != null)
                {
                    if (allowedResearchProjects.Contains(thingDef.recipeMaker.researchPrerequisite))
                    {
                        return true;
                    }
                }
                if (thingDef.recipeMaker.researchPrerequisites?.Any() ?? false)
                {
                    var project = thingDef.recipeMaker.researchPrerequisites.MaxBy(x => (int)x.techLevel);
                    if (allowedResearchProjects.Contains(project))
                    {
                        return true;
                    }
                }
            }
            if (thingDef.researchPrerequisites?.Any() ?? false)
            {
                var project = thingDef.researchPrerequisites.MaxBy(x => (int)x.techLevel);
                if (allowedResearchProjects.Contains(project))
                {
                    return true;
                }
            }
            var techLevel = GetTechLevelFor(thingDef);
            if (techLevel < TechLevel.Industrial)
            {
                return true;
            }
            return false;
        }


        public static List<ResearchProjectDef> GetAllowedProjectDefs(this List<ResearchProjectDef> list)
        {
            if (RimedievalMod.settings.restrictTechToPreIndustrialOnly)
            {
                var microElectronics = DefDatabase<ResearchProjectDef>.GetNamed("MicroelectronicsBasics");
                list = list.Where(x => x.techLevel <= TechLevel.Industrial && x != microElectronics && !x.ContainsTechProjectAsPrerequisite(microElectronics)).ToList();
            }
            else
            {
                list = list.Where(x => x.techLevel <= TechLevel.Medieval).ToList();
            }
            return list;
        }
        public static void ClearDefs()
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
            foreach (var precept in DefDatabase<PreceptDef>.AllDefs)
            {
                precept.associatedMemes.RemoveAll(x => memesToRemove.Contains(x.defName));
                precept.conflictingMemes.RemoveAll(x => memesToRemove.Contains(x.defName));
                precept.requiredMemes.RemoveAll(x => memesToRemove.Contains(x.defName));
            }
            DefDatabase<MemeDef>.defsList.RemoveAll(x => memesToRemove.Contains(x.defName));
            foreach (var mapGen in DefDatabase<MapGeneratorDef>.AllDefs)
            {
                mapGen.genSteps.RemoveAll(x => genStepsToRemove.Contains(x.defName));
            }
            DefDatabase<GenStepDef>.defsList.RemoveAll(x => genStepsToRemove.Contains(x.defName));
            DefDatabase<IncidentDef>.defsList.RemoveAll(x => incidentsToRemove.Contains(x.defName));
            if (ModsConfig.IsActive("BotchJob.medievalfantasythemedrelicquests"))
            {
                var questsToAllow = new List<string> { "AncientComplex_Standard", "Hack_Spacedrone", "OpportunitySite_AncientComplex", "AncientComplex_Mission", "Hack_WorshippedTerminal" };
                questsToRemove.RemoveAll(x => questsToAllow.Contains(x));
            }
            DefDatabase<QuestScriptDef>.defsList.RemoveAll(x => questsToRemove.Contains(x.defName) 
            && x.modExtensions?.FirstOrDefault(y => y.GetType().Name.Contains("QuestInformation")) is null);
            DefDatabase<IdeoPresetDef>.defsList.RemoveAll(x => ideoPresetsToRemove.Contains(x.defName));

            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.designationCategory != null && def.IsAllowedForRimedieval() is false)
                {
                    def.designationCategory = null;
                }
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

        private static List<string> memesToRemove = new List<string>
        {
            "Transhumanist",
        };

        private static List<string> genStepsToRemove = new List<string>
        {
            "ScatterRoadDebris",
            "ScatterCaveDebris",
            "AncientUtilityBuilding",
            "MechanoidRemains",
            "AncientTurret",
            "AncientMechs",
            "AncientLandingPad",
            "AncientFences",
            "AncientPipelineSection",
            "AncientJunkClusters",
            "AncientExostriderRemains",
            "AncientPollutionJunk"
        };

        public static List<string> incidentsToRemove = new List<string>
        {
            "DefoliatorShipPartCrash",
            "PsychicEmanatorShipPartCrash",
            "MechCluster",
            "PsychicSoothe",
            "PsychicDrone",
            "ToxicFallout",
            "VolcanicWinter",
            "ShortCircuit",
            "ShipChunkDrop",
            "OrbitalTraderArrival",
            "GiveQuest_EndGame_ShipEscape",
            "ProblemCauser",
            "Disease_FibrousMechanites",
            "Disease_SensoryMechanites",
            "WastepackInfestation",
        };

        public static List<string> questsToRemove = new List<string>
        {
            "EndGame_ShipEscape",
            "LongRangeMineralScannerLump",
            "ThreatReward_MechPods_MiscReward",
            "AncientComplex_Standard",
            "Hack_Spacedrone",
            "OpportunitySite_AncientComplex",
            "AncientComplex_Mission",
            "Hack_WorshippedTerminal",
            "OpportunitySite_AncientComplex_Mechanitor",
            "MechanitorShip",
            "PollutionDump",
            "PollutionRaid",
            "PollutionRetaliation",
            "MechanitorStartingMech",
        };

        public static List<string> ideoPresetsToRemove = new List<string>
        {
            "Techno_Utopians",
            "Progressive_Humanism"
        };
    }
}
