using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimedieval
{
    class RimedievalSettings : ModSettings
    {
        public const string RimedievalMechAddonModName = "RimedievalMechAddon";
        public bool disableMechanoids = true;
        public bool rimedievalMechAddonWasLoaded = false;
        public bool disableTechRestriction;
        public bool restrictTechToMedievalOnly;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref disableMechanoids, "disableMechanoids", true);
            Scribe_Values.Look(ref rimedievalMechAddonWasLoaded, "rimedievalMechAddonWasLoaded", false);
            Scribe_Values.Look(ref disableTechRestriction, "disableTechRestriction", false);
            Scribe_Values.Look(ref restrictTechToMedievalOnly, "restrictTechToMedievalOnly", false);
        }
        public void DoSettingsWindowContents(Rect inRect)
        {
            Rect rect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            Rect rect2 = new Rect(0f, 0f, inRect.width - 30f, 150);
            Widgets.BeginScrollView(rect, ref scrollPosition, rect2, true);
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(rect2);
            listingStandard.CheckboxLabeled("RM.DisableMechanoids".Translate(), ref disableMechanoids);
            listingStandard.CheckboxLabeled("RM.DisableTechRestriction".Translate(), ref disableTechRestriction);
            listingStandard.CheckboxLabeled("RM.RestrictTechToMedievalOnly".Translate(), ref restrictTechToMedievalOnly);

            listingStandard.End();
            Widgets.EndScrollView();
            base.Write();
        }
        private static Vector2 scrollPosition = Vector2.zero;

    }
}

