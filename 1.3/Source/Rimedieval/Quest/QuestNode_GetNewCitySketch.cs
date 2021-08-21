using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimedieval
{
	public class QuestNode_GetNewCitySketch : QuestNode
	{
		[NoTranslate]
		public SlateRef<string> storeAs;
		public override bool TestRunInt(Slate slate)
		{
			return true;
		}

		public override void RunInt()
		{
			DoWork(QuestGen.slate);
		}

		private bool DoWork(Slate slate)
		{

			ResolveParams parms = default(ResolveParams);
			parms.sketch = new Sketch();
			parms.onlyBuildableByPlayer = true;

			Sketch sketch = SketchGen.Generate(RimedievalDefOf.RM_NewCity, parms);

			List<SketchThing> things = sketch.Things;
			for (int i = 0; i < things.Count; i++)
			{
				things[i].stuff = null;
			}
			List<SketchTerrain> terrain = sketch.Terrain;
			for (int j = 0; j < terrain.Count; j++)
			{
				terrain[j].treatSimilarAsSame = true;
			}

			slate.Set(storeAs.GetValue(slate), sketch);
			return true;
		}
	}
}