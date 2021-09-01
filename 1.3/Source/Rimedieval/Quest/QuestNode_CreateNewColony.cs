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
	public class QuestNode_CreateNewColony : QuestNode
	{
		[NoTranslate]
		public SlateRef<WorldObject> worldObject;
		public override bool TestRunInt(Slate slate)
		{
			return true;
		}
		public override void RunInt()
		{
			Slate slate = QuestGen.slate;
			QuestPart_CreateNewColony questPart = new QuestPart_CreateNewColony();
			questPart.newCity = worldObject.GetValue(slate);
			questPart.inSignal = QuestGenUtility.HardcodedSignalWithQuestID(QuestGen.slate.Get<string>("inSignal"));
			questPart.newCityMarker = slate.Get<NewCityMarker>("monumentMarker");
			questPart.newFactions = slate.Get<List<Faction>>("enemyFactions");
			Log.Message("questPart.newCityMarker: " + questPart.newCityMarker + " - questPart.newFactions: " + questPart.newFactions);
			QuestGen.quest.AddPart(questPart);
		}
	}

	public class QuestPart_CreateNewColony : QuestPart
	{
		public string inSignal;
		public WorldObject newCity;
		public NewCityMarker newCityMarker;
		public List<Faction> newFactions;
		public override void Notify_QuestSignalReceived(Signal signal)
		{
			base.Notify_QuestSignalReceived(signal);
			Log.Message("Notify_QuestSignalReceived: " + signal.tag);
			if (!(signal.tag == inSignal))
			{
				return;
			}

			var map = (newCity as MapParent).Map;
			var things = map.Center.GetThingList(map);

			for (int num = things.Count - 1; num >= 0; num--)
            {
				var thing = things[num];
				if (thing.def.IsEdifice())
                {
					thing.DeSpawn(DestroyMode.Vanish);
                }
            }
			GenSpawn.Spawn(newCityMarker, map.Center, map);
			GenerateFactionsIntoWorld();

			Log.Message(map + " newCity: " + newCityMarker + " - " + newCityMarker.Position);
		}
		public void GenerateFactionsIntoWorld()
		{
			foreach (var faction in newFactions)
			{
				Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
				settlement.SetFaction(faction);
				settlement.Tile = TileFinder.RandomSettlementTileFor(faction);
				settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement);
				Find.WorldObjects.Add(settlement);
			}
			Find.IdeoManager.SortIdeos();
		}

        public override void ExposeData()
        {
			base.ExposeData();
			Scribe_Values.Look(ref inSignal, "inSignal");
			Scribe_References.Look(ref newCity, "newCity");
			Scribe_Deep.Look(ref newCityMarker, "newCityMarker");
			Scribe_Collections.Look(ref newFactions, "newFactions", LookMode.Deep);
		}
    }
}