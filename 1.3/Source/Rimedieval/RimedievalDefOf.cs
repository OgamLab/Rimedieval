using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Rimedieval
{
    [DefOf]
    public static class RimedievalDefOf
    {
        public static SketchResolverDef RM_NewCity;
        public static ResearchProjectDef Electricity;
        public static QuestScriptDef RM_FinalQuest_NewCity;
        public static ThingDef RM_NewCityMarker;
    }
}
