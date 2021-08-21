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
		public SlateRef<string> storeAs = "map";
		public override bool TestRunInt(Slate slate)
		{
			return true;
		}

		public override void RunInt()
		{
			Slate slate = QuestGen.slate;
			if (TileFinder.TryFindNewSiteTile(out int tile, 15, 30))
            {
				SettleUtility.AddNewHome(tile, Faction.OfPlayer);
				var map = GetOrGenerateMapUtility.GetOrGenerateMap(tile, Find.World.info.initialMapSize, null);
				CameraJumper.TryJump(new GlobalTargetInfo(map.Center, map));
				QuestGen.slate.Set(storeAs.GetValue(slate), map);
			}
		}
	}
}