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
			QuestGen.quest.AddPart(questPart);
		}
	}
	public class QuestPart_CreateNewColony : QuestPart
	{
		public string inSignal;
		public WorldObject newCity;
		public NewCityMarker newCityMarker;
		public override void Notify_QuestSignalReceived(Signal signal)
		{
			base.Notify_QuestSignalReceived(signal);
			if (!(signal.tag == inSignal))
			{
				return;
			}

			var map = (newCity as MapParent).Map;
			map.floodFiller.FloodFill(map.Center, (IntVec3 x) => x.GetEdifice(map) != null, delegate (IntVec3 x)
			{
				var things = x.GetThingList(map);
				for (int num = things.Count - 1; num >= 0; num--)
				{
					var thing = things[num];
					if (thing.def.IsEdifice())
					{
						thing.DeSpawn(DestroyMode.Vanish);
						map.roofGrid.SetRoof(x, null);
					}
				}
			});

			var marker = GenSpawn.Spawn(newCityMarker, map.Center, map) as NewCityMarker;
			var cellRect = marker.CustomRectForSelector.Value;
			
			foreach (var buildable in marker.sketch.Buildables)
            {
				var pos = buildable.pos + marker.Position;
				var terrain = pos.GetTerrain(map);
				if (!terrain.affordances.Any(x => x == buildable.Buildable.terrainAffordanceNeeded))
                {
					foreach (var cell in GenRadial.RadialCellsAround(pos, 5, true))
					{
						if (cell.InBounds(map))
						{
							terrain = cell.GetTerrain(map);
							if (!terrain.affordances.Any(x => x == buildable.Buildable.terrainAffordanceNeeded))
							{
								map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
							}
						}
					}
				}
            }
		}


        public override void ExposeData()
        {
			base.ExposeData();
			Scribe_Values.Look(ref inSignal, "inSignal");
			Scribe_References.Look(ref newCity, "newCity");
			Scribe_Deep.Look(ref newCityMarker, "newCityMarker");
		}
	}
}