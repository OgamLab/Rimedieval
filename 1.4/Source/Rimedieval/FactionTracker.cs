using RimWorld;
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
        public List<FactionDef> ignoredFactions;
        public Dictionary<FactionDef, TechLevel> originalTechLevelValues;
        public Dictionary<FactionDef, TechLevel> changedTechLevelValues;
        public TechLevel playerTechLevel = TechLevel.Neolithic;

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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref changedTechLevelValues, "changedTechLevelValues", LookMode.Def, LookMode.Value, ref defKeys, ref levelValues);
            Scribe_Values.Look(ref playerTechLevel, "playerTechLevel", defaultValue: TechLevel.Neolithic);
            Scribe_Collections.Look(ref ignoredFactions, "ignoredFactions", LookMode.Def);
            Instance = this;
        }
        private List<FactionDef> defKeys;
        private List<TechLevel> levelValues;
    }
}
