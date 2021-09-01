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
	public class QuestNode_WealthRequirement : QuestNode
	{
		[NoTranslate]
		public SlateRef<int> wealth;
		public SlateRef<string> outSignalCompleted;
		public override bool TestRunInt(Slate slate)
		{
			return true;
		}

		public override void RunInt()
		{
			Quest quest = QuestGen.quest;
			Slate slate = QuestGen.slate;
			QuestPart_RequirementsToAcceptPlayerWealth questPart_RequirementsToAcceptPlayerWealth = new QuestPart_RequirementsToAcceptPlayerWealth();
			questPart_RequirementsToAcceptPlayerWealth.requiredPlayerWealth = wealth.GetValue(slate);
			quest.AddPart(questPart_RequirementsToAcceptPlayerWealth);
		}
	}

	public class QuestPart_RequirementsToAcceptPlayerWealth : QuestPart_RequirementsToAccept
	{
		public float requiredPlayerWealth = -1f;
		public override AcceptanceReport CanAccept()
		{
			if (WealthUtility.PlayerWealth < requiredPlayerWealth)
			{
				return new AcceptanceReport("QuestRequiredPlayerWealth".Translate(requiredPlayerWealth.ToStringMoney()));
			}
			return true;
		}

        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref requiredPlayerWealth, "requiredPlayerWealth", 0f);
		}
	}
}