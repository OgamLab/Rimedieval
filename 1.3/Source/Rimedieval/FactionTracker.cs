﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Rimedieval
{
    public class FactionTracker : GameComponent
    {
        public bool finalQuestIsInitialized;
        public List<FactionDef> ignoredFactions;
        public Dictionary<FactionDef, TechLevel> originalTechLevelValues;
        public Dictionary<FactionDef, TechLevel> changedTechLevelValues;
        public TechLevel playerTechLevel = TechLevel.Neolithic;

        public bool isTechLimitDisabled;

        public static FactionTracker Instance;
        public FactionTracker()
        {
            Instance = this;
        }

        public FactionTracker(Game game)
        {
            Instance = this;
        }


        public void PreInit()
        {
            if (this.originalTechLevelValues == null) this.originalTechLevelValues = new Dictionary<FactionDef, TechLevel>();
            if (this.changedTechLevelValues == null) this.changedTechLevelValues = new Dictionary<FactionDef, TechLevel>();
            if (this.ignoredFactions == null) this.ignoredFactions = new List<FactionDef>();
            Instance = this;
        }
        public override void LoadedGame()
        {
            base.LoadedGame();
            this.PreInit();
            RestoreTechLevelForAllFactions();
            ChangeTechLevelForFactions();
        }
        public override void StartedNewGame()
        {
            base.StartedNewGame();
            this.PreInit();
            RestoreTechLevelForAllFactions();
            ChangeTechLevelForFactions();
            playerTechLevel = TechLevel.Neolithic;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (GenDate.DaysPassed >= 7 && !finalQuestIsInitialized)
            {
                finalQuestIsInitialized = true;
                var quest = QuestUtility.GenerateQuestAndMakeAvailable(RimedievalDefOf.RM_FinalQuest_NewCity, StorytellerUtility.DefaultThreatPointsNow(Find.World));

                TaggedString label = IncidentDefOf.GiveQuest_Random.letterLabel + ": " + quest.name;
                TaggedString text = "RM.QuestDescription".Translate(); ;
                if (quest.initiallyAccepted)
                {
                    label = "LetterQuestAutomaticallyAcceptedTitle".Translate(quest.name);
                }

                ChoiceLetter choiceLetter = LetterMaker.MakeLetter(label, text, (quest.root != null && quest.root.questAvailableLetterDef != null) ? quest.root.questAvailableLetterDef : IncidentDefOf.GiveQuest_Random.letterDef, LookTargets.Invalid, null, quest);
                choiceLetter.title = quest.name;
                Find.LetterStack.ReceiveLetter(choiceLetter);
            }
        }
        public void ChangeTechLevelForFactions()
        {
            RestoreTechLevelForAllFactions();
            foreach (var factionDef in DefDatabase<FactionDef>.AllDefs)
            {
                if (factionDef != Faction.OfPlayer.def)
                {
                    originalTechLevelValues[factionDef] = factionDef.techLevel;
                    if (factionDef.techLevel > TechLevel.Medieval)
                    {
                        SetNewTechLevelForFaction(factionDef);
                    }
                }
            }
        }

        public void SetNewTechLevelForFaction(FactionDef factionDef)
        {
            this.PreInit();
            if (!ignoredFactions.Contains(factionDef) && factionDef.humanlikeFaction)
            {
                if (factionDef == FactionDefOf.Empire || factionDef == DefDatabase<FactionDef>.GetNamedSilentFail("Pirate"))
                {
                    factionDef.techLevel = TechLevel.Medieval;
                }
                else
                {
                    factionDef.techLevel = GetRandomTechLevel();
                }
                changedTechLevelValues[factionDef] = factionDef.techLevel;
            }

        }

        private TechLevel GetRandomTechLevel()
        {
            var num = Rand.RangeInclusive(1, 2);
            switch (num)
            {
                case 1: return TechLevel.Neolithic;
                case 2: return TechLevel.Medieval;
            }
            return TechLevel.Medieval;
        }

        public void RestoreTechLevelForAllFactions()
        {
            this.PreInit();
            if (originalTechLevelValues != null)
            {
                foreach (var factionDef in DefDatabase<FactionDef>.AllDefs)
                {
                    if (originalTechLevelValues.ContainsKey(factionDef) && factionDef != Faction.OfPlayer.def && factionDef.humanlikeFaction)
                    {
                        factionDef.techLevel = originalTechLevelValues[factionDef];
                    }
                }
            }
        }

        public List<ResearchProjectDef> AllowedResearchProjects(IEnumerable<ResearchProjectDef> projects)
        {
            playerTechLevel = DetermineCurrentPlayerTechLevel();
            var techLevelToUnlock = EnoughTechProgress() ? playerTechLevel + 1 : playerTechLevel;
            return projects.Where(x => x.techLevel <= techLevelToUnlock).ToList();
        }
        private TechLevel DetermineCurrentPlayerTechLevel()
        {
            var determinedTechLevel = TechLevel.Neolithic;
            foreach (var techLevel in Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>())
            {
                if (techLevel < TechLevel.Neolithic)
                {
                    continue;
                }
                var curTechProjects = DefDatabase<ResearchProjectDef>.AllDefs.Where(x => x.techLevel == techLevel).ToList();
                if (curTechProjects.Any() && curTechProjects.All(x => x.IsFinished))
                {
                    determinedTechLevel = techLevel;
                }
            }
            return determinedTechLevel;
        }
        private bool EnoughTechProgress()
        {
            var curTechProjects = DefDatabase<ResearchProjectDef>.AllDefs.Where(x => x.techLevel == playerTechLevel).ToList();
            var keyTeckProjects = GetKeyProjectsFrom(curTechProjects, playerTechLevel).ToList();
            return (float)((float)curTechProjects.Where(x => x.IsFinished).Count() / (float)curTechProjects.Count()) >= 0.5f && keyTeckProjects.All(x => x.IsFinished);
        }

        private IEnumerable<ResearchProjectDef> GetKeyProjectsFrom(List<ResearchProjectDef> list, TechLevel techLevel)
        {
            return list.Where(x => x.techLevel == techLevel && (x.GetModExtension<TechExtension>()?.isKeyProject ?? false));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref changedTechLevelValues, "changedTechLevelValues", LookMode.Def, LookMode.Value, ref defKeys, ref levelValues);
            Scribe_Values.Look(ref playerTechLevel, "playerTechLevel", defaultValue: TechLevel.Neolithic);
            Scribe_Values.Look(ref finalQuestIsInitialized, "finalQuestIsInitialized");
            Scribe_Collections.Look(ref ignoredFactions, "ignoredFactions", LookMode.Def);
            Scribe_Values.Look(ref isTechLimitDisabled, "isTechLimitDisabled");
            Instance = this;
        }
        private List<FactionDef> defKeys;
        private List<TechLevel> levelValues;
    }
}
