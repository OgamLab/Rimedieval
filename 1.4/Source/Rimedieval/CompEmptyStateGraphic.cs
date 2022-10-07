using RimWorld;
using UnityEngine;
using Verse;

namespace Rimedieval
{
	public class CompEmptyStateGraphic : ThingComp
	{
		private CompProperties_EmptyStateGraphic Props => (CompProperties_EmptyStateGraphic)props;
		public bool ParentIsEmpty
		{
			get
			{
				if (parent is Building_Casket building_Casket && !building_Casket.HasAnyContents)
				{
					return true;
				}
				var compPawnSpawnOnWakeup = parent.TryGetComp<CompPawnSpawnOnWakeup>();
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
				var mesh = Props.graphicData.Graphic.MeshAt(parent.Rotation);
				var drawPos = parent.DrawPos;
				drawPos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
				Graphics.DrawMesh(mesh, drawPos + Props.graphicData.drawOffset.RotatedBy(parent.Rotation), Quaternion.identity, Props.graphicData.GraphicColoredFor(parent).MatAt(parent.Rotation), 0);
			}
		}
	}
}
