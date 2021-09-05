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
using Verse.Grammar;

namespace Rimedieval
{
	[HarmonyPatch(typeof(CaravanArrivalAction_VisitSettlement), "Arrived")]
	public class Patch_Arrived
	{
		public static void Prefix(CaravanArrivalAction_VisitSettlement __instance, Caravan caravan)
		{
			if (__instance.settlement is NewCity newCity)
            {
				if (!newCity.HasMap)
				{
					LongEventHandler.QueueLongEvent(delegate
					{
						DoEnter(caravan, newCity);
					}, "GeneratingMap", doAsynchronously: false, null);
				}
				else
				{
					DoEnter(caravan, newCity);
				}
			}
		}

		private static void DoEnter(Caravan caravan, Settlement settlement)
		{
			LookTargets lookTargets = new LookTargets(caravan.PawnsListForReading);
			bool draftColonists = settlement.Faction == null || settlement.Faction.HostileTo(Faction.OfPlayer);
			Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, null);
			CaravanEnterMapUtility.Enter(caravan, orGenerateMap, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, draftColonists);
		}
	}

	public class NewCity : Settlement
    {
        public override bool Visitable => !this.HasMap;
    }

	public static class QuestGen_NewHome
	{
		private const string RootSymbol = "root";
		public static WorldObject MakeSite(int tile, Faction faction)
		{
			WorldObject site = WorldObjectMaker.MakeWorldObject(RimedievalDefOf.RM_NewCityObj);
			site.Tile = tile;
			site.SetFaction(faction);
			return site;
		}
		public static QuestPart_SpawnWorldObject SpawnWorldObject(this Quest quest, WorldObject worldObject, List<ThingDef> defsToExcludeFromHyperlinks = null, string inSignal = null)
		{
			QuestPart_SpawnWorldObject questPart_SpawnWorldObject = new QuestPart_SpawnWorldObject();
			questPart_SpawnWorldObject.inSignal = QuestGenUtility.HardcodedSignalWithQuestID(inSignal) ?? QuestGen.slate.Get<string>("inSignal");
			questPart_SpawnWorldObject.defsToExcludeFromHyperlinks = defsToExcludeFromHyperlinks;
			questPart_SpawnWorldObject.worldObject = worldObject;
			quest.AddPart(questPart_SpawnWorldObject);
			return questPart_SpawnWorldObject;
		}
		public static QuestPart_StartDetectionRaids StartRecurringRaids(this Quest quest, WorldObject worldObject, FloatRange? delayRangeHours = null, int? firstRaidDelayTicks = null, string inSignal = null)
		{
			QuestPart_StartDetectionRaids questPart_StartDetectionRaids = new QuestPart_StartDetectionRaids();
			questPart_StartDetectionRaids.inSignal = QuestGenUtility.HardcodedSignalWithQuestID(inSignal) ?? QuestGen.slate.Get<string>("inSignal");
			questPart_StartDetectionRaids.worldObject = worldObject;
			questPart_StartDetectionRaids.delayRangeHours = delayRangeHours;
			questPart_StartDetectionRaids.firstRaidDelayTicks = firstRaidDelayTicks;
			quest.AddPart(questPart_StartDetectionRaids);
			return questPart_StartDetectionRaids;
		}

		public static WorldObject GenerateSettlement(int tile, Faction faction)
		{
			_ = QuestGen.slate;
			WorldObject site = MakeSite(tile, faction);
			List<Rule> list = new List<Rule>();
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			List<string> list2 = new List<string>();
			if (!list2.Any())
			{
				list.Add(new Rule_String("allSitePartsDescriptions", "HiddenOrNoSitePartDescription".Translate()));
				list.Add(new Rule_String("allSitePartsDescriptionsExceptFirst", "HiddenOrNoSitePartDescription".Translate()));
			}
			else
			{
				list.Add(new Rule_String("allSitePartsDescriptions", list2.ToClauseSequence().Resolve()));
				if (list2.Count >= 2)
				{
					list.Add(new Rule_String("allSitePartsDescriptionsExceptFirst", list2.Skip(1).ToList().ToClauseSequence()));
				}
				else
				{
					list.Add(new Rule_String("allSitePartsDescriptionsExceptFirst", "HiddenOrNoSitePartDescription".Translate()));
				}
			}
			QuestGen.AddQuestDescriptionRules(list);
			QuestGen.AddQuestNameRules(list);
			QuestGen.AddQuestDescriptionConstants(dictionary);
			QuestGen.AddQuestNameConstants(dictionary);
			QuestGen.AddQuestNameRules(new List<Rule>
			{
				new Rule_String("site_label", site.Label)
			});
			return site;
		}

		public static QuestPart_SetSitePartThreatPointsToCurrent SetSitePartThreatPointsToCurrent(this Quest quest, Site site, SitePartDef sitePartDef, MapParent useMapParentThreatPoints, string inSignal = null, float threatPointsFactor = 1f)
		{
			QuestPart_SetSitePartThreatPointsToCurrent questPart_SetSitePartThreatPointsToCurrent = new QuestPart_SetSitePartThreatPointsToCurrent();
			questPart_SetSitePartThreatPointsToCurrent.inSignal = QuestGenUtility.HardcodedSignalWithQuestID(inSignal) ?? QuestGen.slate.Get<string>("inSignal");
			questPart_SetSitePartThreatPointsToCurrent.site = site;
			questPart_SetSitePartThreatPointsToCurrent.sitePartDef = sitePartDef;
			questPart_SetSitePartThreatPointsToCurrent.useMapParentThreatPoints = useMapParentThreatPoints;
			questPart_SetSitePartThreatPointsToCurrent.threatPointsFactor = threatPointsFactor;
			quest.AddPart(questPart_SetSitePartThreatPointsToCurrent);
			return questPart_SetSitePartThreatPointsToCurrent;
		}
	}
}