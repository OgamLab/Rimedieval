using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimedieval
{
	public class QuestNode_CreateNewIndustrialFactions : QuestNode
	{
		public SlateRef<List<FactionDef>> factionDefs;
		public override bool TestRunInt(Slate slate)
		{
			return true;
		}

		public override void RunInt()
		{
			Slate slate = QuestGen.slate;
			GenerateFactionsIntoWorld(slate);
		}
		public void GenerateFactionsIntoWorld(Slate slate)
		{
			int num = 0;
			List<Faction> newFactions = new List<Faction>();
			foreach (FactionDef item in factionDefs.GetValue(slate))
			{
				FactionTracker.Instance.ignoredFactions.Add(item);
				item.techLevel = TechLevel.Industrial;
				Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(item));
				Find.FactionManager.Add(faction);
				newFactions.Add(faction);
				faction.TryAffectGoodwillWith(Faction.OfPlayer, -100, false, false);
				if (!faction.Hidden)
				{
					num++;
				}
			}

			foreach (var faction in newFactions)
			{
				Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
				settlement.SetFaction(faction);
				settlement.Tile = TileFinder.RandomSettlementTileFor(faction);
				settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement);
				Find.WorldObjects.Add(settlement);
			}

			slate.Set("enemyFactions", newFactions);
			Find.IdeoManager.SortIdeos();
		}
	}
}