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
    class RimedievalMod : Mod
    {
        public static RimedievalSettings settings;
        public RimedievalMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<RimedievalSettings>();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }
        public override string SettingsCategory()
        {
            return "Rimedieval";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            DefsAlterer.DoDefsAlter();
        }
    }
    [StaticConstructorOnStartup]
    public static class DefsAlterer
    {
        static DefsAlterer()
        {
            Setup();
            DoDefsAlter();
        }

        public static void Setup()
        {
            if (!RimedievalMod.settings.rimedievalMechAddonWasLoaded && ModLister.HasActiveModWithName(RimedievalSettings.RimedievalMechAddonModName))
            {
                RimedievalMod.settings.rimedievalMechAddonWasLoaded = true;
                RimedievalMod.settings.disableMechanoids = false;
            }
        }

        public static void DoDefsAlter()
        {

        }
    }
}