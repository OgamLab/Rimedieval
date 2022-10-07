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
	public class QuestNode_GenerateNewCityMarker : QuestNode
	{
		[NoTranslate]
		public SlateRef<string> storeAs;

		public SlateRef<Sketch> sketch;

		public override bool TestRunInt(Slate slate)
		{
			return true;
		}

		public override void RunInt()
		{
			Slate slate = QuestGen.slate;
			NewCityMarker newCityMarker = ThingMaker.MakeThing(RimedievalDefOf.RM_NewCityMarker) as NewCityMarker;
			newCityMarker.sketch = sketch.GetValue(slate);
			slate.Set(storeAs.GetValue(slate), newCityMarker);
		}
	}
}