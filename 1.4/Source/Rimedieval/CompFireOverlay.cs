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
	[StaticConstructorOnStartup]
	public class CompFireOverlay : CompFireOverlayBase
	{
		protected CompRefuelable refuelableComp;

		public static readonly Graphic FireGraphic = GraphicDatabase.Get<Graphic_Flicker>("Things/Building/BigCampfire/Flame_Tall", ShaderDatabase.TransparentPostLight, Vector2.one, Color.white);
		public new CompProperties_FireOverlay Props => (CompProperties_FireOverlay)props;
		public override void PostDraw()
		{
			base.PostDraw();
			if (refuelableComp == null || refuelableComp.HasFuel)
			{
				Vector3 drawPos = parent.DrawPos;
				drawPos.y += 3f / 74f;
				drawPos.x += 0.135f;
				drawPos.z += 2.1f;
				FireGraphic.Draw(drawPos, Rot4.North, parent);
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			refuelableComp = parent.GetComp<CompRefuelable>();
		}

		public override void CompTick()
		{
			if ((refuelableComp == null || refuelableComp.HasFuel) && startedGrowingAtTick < 0)
			{
				startedGrowingAtTick = GenTicks.TicksAbs;
			}
		}
	}

}
