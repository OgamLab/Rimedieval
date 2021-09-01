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
	public class QuestNode_GenerateNewHome : QuestNode
	{
		public SlateRef<int> tile;
		[NoTranslate]
		public SlateRef<string> storeAs;

		private const string RootSymbol = "root";
		public override bool TestRunInt(Slate slate)
		{
			return true;
		}
		public override void RunInt()
		{
			Slate slate = QuestGen.slate;
			WorldObject var = QuestGen_NewHome.GenerateSettlement(tile.GetValue(slate), Faction.OfPlayer);
			if (storeAs.GetValue(slate) != null)
			{
				QuestGen.slate.Set(storeAs.GetValue(slate), var);
			}
		}
	}
}