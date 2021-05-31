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
        public Dictionary<FactionDef, TechLevel> originalTechLevelValues;
        public Dictionary<FactionDef, TechLevel> changedTechLevelValues;
        public FactionTracker()
        {

        }

        public FactionTracker(Game game)
        {

        }


        public void PreInit()
        {
            if (this.originalTechLevelValues == null) this.originalTechLevelValues = new Dictionary<FactionDef, TechLevel>();
            if (this.changedTechLevelValues == null) this.changedTechLevelValues = new Dictionary<FactionDef, TechLevel>();

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
        }


        public void ChangeTechLevelForFactions()
        {
            RestoreTechLevelForAllFactions();
            foreach (var factionDef in DefDatabase<FactionDef>.AllDefs)
            {
                if (factionDef != Faction.OfPlayer.def && factionDef.humanlikeFaction)
                {
                    originalTechLevelValues[factionDef] = factionDef.techLevel;
                    if (factionDef.techLevel > TechLevel.Medieval)
                    {
                        factionDef.techLevel = GetRandomTechLevel();
                        changedTechLevelValues[factionDef] = factionDef.techLevel;
                    }
                }
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
        }
        private List<FactionDef> defKeys;
        private List<TechLevel> levelValues;
    }
}
