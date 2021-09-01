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
	public class QuestNode_SendRaids : QuestNode
	{
		[NoTranslate]
		public SlateRef<string> inSignalEnable;

		[NoTranslate]
		public SlateRef<string> inSignalDisable;

		public SlateRef<int?> intervalTicks;

		public SlateRef<int?> randomIncidents;

		public SlateRef<int> startOffsetTicks;

		public SlateRef<int> duration;

		public SlateRef<float> points;

		public SlateRef<List<Faction>> enemyFactions;

		public SlateRef<WorldObject> worldObject;
		public override bool TestRunInt(Slate slate)
		{
			if (points.GetValue(slate) < IncidentDefOf.RaidEnemy.minThreatPoints)
			{
				return false;
			}
			return true;
		}
		public override void RunInt()
		{
			Slate slate = QuestGen.slate;
			int value = duration.GetValue(slate);
			int value2 = startOffsetTicks.GetValue(slate);
			IncidentDef value3 = IncidentDefOf.RaidEnemy;
			float value4 = points.GetValue(slate);
			string delayInSignal = slate.Get<string>("inSignal");
			string disableSignal = QuestGenUtility.HardcodedSignalWithQuestID(inSignalDisable.GetValue(slate));
			int? value6 = randomIncidents.GetValue(slate);
			if (value6.HasValue)
			{
				for (int i = 0; i < value6; i++)
				{
					CreateDelayedIncident(slate, Rand.Range(value2, value), delayInSignal, disableSignal, value3, value4);
				}
			}
		}

		private void CreateDelayedIncident(Slate slate, int delayTicks, string delayInSignal, string disableSignal, IncidentDef incident, float points)
		{
			Quest quest = QuestGen.quest;
			QuestPart_Delay questPart_Delay = new QuestPart_Delay();
			questPart_Delay.delayTicks = delayTicks;
			questPart_Delay.inSignalEnable = delayInSignal;
			questPart_Delay.inSignalDisable = disableSignal;
			questPart_Delay.debugLabel = questPart_Delay.delayTicks.ToStringTicksToDays() + "_" + IncidentDefOf.RaidEnemy.ToString();
			quest.AddPart(questPart_Delay);
			QuestPart_Incident questPart_Incident = new QuestPart_Incident();
			questPart_Incident.incident = incident;
			questPart_Incident.inSignal = questPart_Delay.OutSignalCompleted;
			questPart_Incident.MapParent = worldObject.GetValue(slate) as MapParent;
			questPart_Incident.incidentParms = new IncidentParms
			{
				forced = true,
				points = points,
				raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
				raidStrategy = RaidStrategyDefOf.ImmediateAttack,
				faction = enemyFactions.GetValue(slate).RandomElement()
			};
			quest.AddPart(questPart_Incident);
		}
	}
}