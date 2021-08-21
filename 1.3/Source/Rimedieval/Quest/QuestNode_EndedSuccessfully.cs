using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
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
	public class QuestNode_EndedSuccessfully : QuestNode
	{
		public SlateRef<string> inSignalEnable;
		public override bool TestRunInt(Slate slate)
		{
			return true;
		}

		public override void RunInt()
		{
			Slate slate = QuestGen.slate;
			var questPart_EndedSuccessfully = new QuestPart_EndedSuccessfully();
			questPart_EndedSuccessfully.map = slate.Get<Map>("map");
			questPart_EndedSuccessfully.newCityMarker = slate.Get<NewCityMarker>("monumentMarker");
			questPart_EndedSuccessfully.inSignalEnable = QuestGenUtility.HardcodedSignalWithQuestID(inSignalEnable.GetValue(slate)) ?? QuestGen.slate.Get<string>("inSignalEnable");
			QuestGen.quest.AddPart(questPart_EndedSuccessfully);
		}
	}

	public class QuestPart_EndedSuccessfully : QuestPartActivable
    {
		public Map map;

		public NewCityMarker newCityMarker;
        public override void QuestPartTick()
        {
            base.QuestPartTick();
			if (!GenHostility.AnyHostileActiveThreatToPlayer_NewTemp(map))
            {
				DiaNode diaNode = new DiaNode("RM.YouHaveEndedFinalQuestSuccessfully".Translate());
				DiaOption diaOption = new DiaOption("RM.KeepPlaying".Translate());
				diaOption.resolveTree = true;
				diaNode.options.Add(diaOption);
				diaOption.action = delegate ()
				{
					newCityMarker.canDestroy = true;
					newCityMarker?.Destroy();
				};
				DiaOption diaOption3 = new DiaOption("RM.FinishGame".Translate());
				diaOption3.action = delegate
				{
					StringBuilder stringBuilder = new StringBuilder();
					List<Pawn> list = (from p in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_Colonists where p.RaceProps.Humanlike select p).ToList();
					foreach (Pawn item in list)
					{
						if (!item.Dead && !item.IsQuestLodger())
						{
							stringBuilder.AppendLine("   " + item.LabelCap);
						}
					}
					GameVictoryUtility.ShowCredits(GameVictoryUtility.MakeEndCredits("RM.GameOverMedievalInvokedIntro".Translate(), 
						"RM.GameOverMedievalInvokedEnding".Translate(), stringBuilder.ToString(), "RM.GameOverColonistsAdvanced", list), SongDefOf.EndCreditsSong, exitToMainMenu: true, 2.5f);

				};
				diaOption3.resolveTree = true;
				diaNode.options.Add(diaOption3);
				Dialog_NodeTree dialog_NodeTree = new Dialog_NodeTree(diaNode, delayInteractivity: true);
				dialog_NodeTree.screenFillColor = Color.clear;
				dialog_NodeTree.silenceAmbientSound = !true;
				dialog_NodeTree.closeOnAccept = true;
				dialog_NodeTree.closeOnCancel = true;
				Find.WindowStack.Add(dialog_NodeTree);
				Find.Archive.Add(new ArchivedDialog(diaNode.text));
			}
        }
        public override void ExposeData()
        {
            base.ExposeData();
			Scribe_References.Look(ref map, "map");
			Scribe_References.Look(ref newCityMarker, "newCityMarker");
        }
    }
}