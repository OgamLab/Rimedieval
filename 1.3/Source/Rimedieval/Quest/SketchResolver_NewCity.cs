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
	public class SketchResolver_NewCity : SketchResolver
	{
		public override bool CanResolveInt(ResolveParams parms)
		{
			if (parms.rect.HasValue)
			{
				return parms.sketch != null;
			}
			return false;
		}


        public static IntVec3 GetOffsetPosition(IntVec3 cell, IntVec3 offset)
        {
            return cell + offset;
        }
        public static IntVec3 GetCellCenterFor(List<IntVec3> cells)
        {
            var x_Averages = cells.OrderBy(x => x.x);
            var x_average = x_Averages.ElementAt(x_Averages.Count() / 2).x;
            var z_Averages = cells.OrderBy(x => x.z);
            var z_average = z_Averages.ElementAt(z_Averages.Count() / 2).z;
            var middleCell = new IntVec3(x_average, 0, z_average);
            return middleCell;
        }

        public static int GetMaxWidth(List<IntVec3> cells)
        {
            int maxWidth = 0;
            foreach (var cell in cells)
            {
                foreach (var cell2 in cells)
                {
                    if (cell.z == cell2.z)
                    {
                        var curWidth = cell2.x - cell.x;
                        if (curWidth > maxWidth)
                        {
                            maxWidth = curWidth;
                        }
                    }
                }
            }
            return maxWidth + 1;
        }

        public static int GetMaxHeight(List<IntVec3> cells)
        {
            int maxHeight = 0;
            foreach (var cell in cells)
            {
                foreach (var cell2 in cells)
                {
                    if (cell.x == cell2.x)
                    {
                        var curHeight = cell2.z - cell.z;
                        if (curHeight > maxHeight)
                        {
                            maxHeight = curHeight;
                        }
                    }
                }
            }
            return maxHeight + 1;
        }
        public override void ResolveInt(ResolveParams parms)
		{
            var path = Path.GetFullPath(ModLister.GetActiveModWithIdentifier("Ogam.Rimedieval").RootDir + "/Presets/TEST.xml");
            List<Building> buildings = new List<Building>();
            List<Thing> things = new List<Thing>();
            Dictionary<IntVec3, TerrainDef> terrains = new Dictionary<IntVec3, TerrainDef>();
            Dictionary<IntVec3, RoofDef> roofs = new Dictionary<IntVec3, RoofDef>();

            Scribe.loader.InitLoading(path);

            Scribe_Collections.Look<Building>(ref buildings, "Buildings", LookMode.Deep, new object[0]);
            Scribe_Collections.Look<Thing>(ref things, "Things", LookMode.Deep, new object[0]);

            Scribe_Collections.Look<IntVec3, TerrainDef>(ref terrains, "Terrains", LookMode.Value, LookMode.Def, ref terrainKeys, ref terrainValues);
            Scribe_Collections.Look<IntVec3, RoofDef>(ref roofs, "Roofs", LookMode.Value, LookMode.Def, ref roofsKeys, ref roofsValues);
            Scribe.loader.FinalizeLoading();

            if (buildings is null)
            {
                buildings = new List<Building>();
            }
            else
            {
                buildings.RemoveAll(x => x is null);
            }
            if (things is null)
            {
                things = new List<Thing>();
            }
            else
            {
                things.RemoveAll(x => x is null);
            }
            var cells = things.Select(x => x.Position).Concat(buildings.Select(x => x.Position)).Distinct().ToList();

            var width = GetMaxWidth(cells);
            var height = GetMaxHeight(cells);
            parms.rect = new CellRect(0, 0, width, height);
            CellRect outerRect = parms.rect ?? parms.sketch.OccupiedRect;
            var firstCell = outerRect.First();
            var offset = outerRect.BottomLeft - GetCellCenterFor(cells);

            foreach (var building in buildings)
            {
                parms.sketch.AddThing(building.def, GetOffsetPosition(building.Position, offset), building.Rotation, building.Stuff, building.stackCount);
            }

            foreach (var thing in things)
            {
                parms.sketch.AddThing(thing.def, GetOffsetPosition(thing.Position, offset), thing.Rotation, thing.Stuff, thing.stackCount);
            }

            foreach (var data in terrains)
            {
                parms.sketch.AddTerrain(data.Value, GetOffsetPosition(data.Key, offset));
            }
        }

        public static List<IntVec3> terrainKeys = new List<IntVec3>();
        public static List<TerrainDef> terrainValues = new List<TerrainDef>();
        public static List<IntVec3> roofsKeys = new List<IntVec3>();
        public static List<RoofDef> roofsValues = new List<RoofDef>();
    }
}