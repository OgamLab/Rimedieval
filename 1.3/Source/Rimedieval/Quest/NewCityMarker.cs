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
	public class NewCityMarker : MonumentMarker
	{
        public bool canDestroy;
        public override void Tick()
        {
            base.Tick();
            if (this.complete)
            {
                var totalThingsCount = this.sketch.Things.Count;
                var spawnedThingsCount = 0;
                foreach (var t in this.sketch.Things)
                {
                    var pos = this.sketch.GetOffset(t.pos, Sketch.SpawnPosType.Unchanged) + this.Position;
                    if (t.GetSameSpawned(pos, Map) != null)
                    {
                        spawnedThingsCount++;
                    }
                }
                if (spawnedThingsCount / (float)totalThingsCount < 0.5f)
                {
                    QuestUtility.SendQuestTargetSignals(questTags, "NewCityRuined", this.Named("SUBJECT"));
                }
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (canDestroy)
            {
                base.Destroy(mode);
            }
        }
    }
}