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
		public SlateRef<string> inSignal;
		public override bool TestRunInt(Slate slate)
		{
			return true;
		}

		public override void RunInt()
		{
			Slate slate = QuestGen.slate;
			List<Faction> newFactions = new List<Faction>();
			foreach (FactionDef item in factionDefs.GetValue(slate))
			{
				FactionTracker.Instance.ignoredFactions.Add(item);
				item.techLevel = TechLevel.Industrial;
				Faction faction = NewGeneratedFaction(new FactionGeneratorParms(item));
				newFactions.Add(faction);
			}
			slate.Set("enemyFactions", newFactions);
			QuestPart_AddFactions questPart = new QuestPart_AddFactions();
			questPart.inSignal = QuestGenUtility.HardcodedSignalWithQuestID(inSignal.GetValue(slate)) ?? slate.Get<string>("inSignal");
			questPart.enemyFactions = newFactions;
			QuestGen.quest.AddPart(questPart);
		}
		public static Faction NewGeneratedFaction(FactionGeneratorParms parms)
		{
			FactionDef factionDef = parms.factionDef;
			parms.ideoGenerationParms.forFaction = factionDef;
			Faction faction = new Faction();
			faction.def = factionDef;
			faction.loadID = Find.UniqueIDsManager.GetNextFactionID();
			faction.colorFromSpectrum = FactionGenerator.NewRandomColorFromSpectrum(faction);
			faction.hidden = parms.hidden;
			if (factionDef.humanlikeFaction)
			{
				faction.ideos = new FactionIdeosTracker(faction);
				if (!faction.IsPlayer || !ModsConfig.IdeologyActive || !Find.GameInitData.startedFromEntry)
				{
					faction.ideos.ChooseOrGenerateIdeo(parms.ideoGenerationParms);
				}
			}
			if (!factionDef.isPlayer)
			{
				if (factionDef.fixedName != null)
				{
					faction.Name = factionDef.fixedName;
				}
				else
				{
					string text = "";
					for (int i = 0; i < 10; i++)
					{
						string text2 = NameGenerator.GenerateName(faction.def.factionNameMaker, Find.FactionManager.AllFactionsVisible.Select((Faction fac) => fac.Name));
						if (text2.Length <= 20)
						{
							text = text2;
						}
					}
					if (text.NullOrEmpty())
					{
						text = NameGenerator.GenerateName(faction.def.factionNameMaker, Find.FactionManager.AllFactionsVisible.Select((Faction fac) => fac.Name));
					}
					faction.Name = text;
				}
			}
			faction.centralMelanin = Rand.Value;
			return faction;
		}
	}
	public class QuestPart_AddFactions : QuestPart
	{
		public string inSignal; 
		public List<Faction> enemyFactions;
		public override void Notify_QuestSignalReceived(Signal signal)
		{
			base.Notify_QuestSignalReceived(signal);
			if (!(signal.tag == inSignal))
			{
				return;
			}

			foreach (var faction in enemyFactions)
			{
				foreach (Faction item in Find.FactionManager.AllFactionsListForReading)
				{
					faction.TryMakeInitialRelationsWith(item);
				}
				Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
				settlement.SetFaction(faction);
				settlement.Tile = TileFinder.RandomSettlementTileFor(faction);
				settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement);
				Find.WorldObjects.Add(settlement);
				faction.TryGenerateNewLeader();
				Find.FactionManager.Add(faction);

				faction.TryAffectGoodwillWith(Faction.OfPlayer, -100, false);

			}
			Find.IdeoManager.SortIdeos();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref inSignal, "inSignal"); 
			Scribe_Collections.Look(ref enemyFactions, "enemyFactions", LookMode.Deep);
		}
	}
}