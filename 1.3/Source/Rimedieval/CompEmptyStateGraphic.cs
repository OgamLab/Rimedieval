using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimedieval
{
	public class CompEmptyStateGraphic : ThingComp
	{
		private CompProperties_EmptyStateGraphic Props => (CompProperties_EmptyStateGraphic)props;
		public bool ParentIsEmpty
		{
			get
			{
				Building_Casket building_Casket = parent as Building_Casket;
				if (building_Casket != null && !building_Casket.HasAnyContents)
				{
					return true;
				}
				CompPawnSpawnOnWakeup compPawnSpawnOnWakeup = parent.TryGetComp<CompPawnSpawnOnWakeup>();
				if (compPawnSpawnOnWakeup != null && !compPawnSpawnOnWakeup.CanSpawn)
				{
					return true;
				}
				return false;
			}
		}

		public override void PostDraw()
		{
			base.PostDraw();
			if (ParentIsEmpty)
			{
				Mesh mesh = Props.graphicData.Graphic.MeshAt(parent.Rotation);
				Vector3 drawPos = parent.DrawPos;
				drawPos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
				Graphics.DrawMesh(mesh, drawPos + Props.graphicData.drawOffset.RotatedBy(parent.Rotation), Quaternion.identity, Props.graphicData.GraphicColoredFor(parent).MatAt(parent.Rotation), 0);
			}
		}
	}
}
