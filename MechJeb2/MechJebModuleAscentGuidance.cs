using System;
using UnityEngine;
using KSP.UI.Screens;
using KSP.Localization;

namespace MuMech
{
    //When enabled, the ascent guidance module makes the purple navball target point
    //along the ascent path. The ascent path can be set via SetPath. The ascent guidance
    //module disables itself if the player selects a different target.
    public class MechJebModuleAscentGuidance : DisplayModule
    {
        public MechJebModuleAscentGuidance(MechJebCore core) : base(core) { }

        // Ascent settings to be copied to the Autopilot by Apply
        public EditableDoubleMult desiredOrbitAltitude = new EditableDoubleMult(100000, 1000); // Classic+GT only
        public EditableDoubleMult desiredAltitude = new EditableDoubleMult(100000, 1000); // PVG only
        public EditableDoubleMult desiredPeriapsis = new EditableDoubleMult(100000, 1000); // PVG only
        public EditableDoubleMult desiredApoapsis = new EditableDoubleMult(100000, 1000); // PVG only
        public EditableDoubleMult desiredSMA = new EditableDoubleMult(100000, 1000); // PVG only
        public EditableDoubleMult desiredECC = new EditableDouble(0); // PVG only
        public EditableDoubleMult desiredFPA = new EditableDouble(0); // PVG only
        public EditableInt maxStages = 2; // PVG only
        public int desiredShapeMode = 0; // PVG only
        public EditableDouble desiredInclination = new EditableDouble(0); // Classic+GT+PVG
        public int desiredIncMode = 0; // PVG only
        public EditableDouble desiredLAN = new EditableDouble(0); // PVG only
        public int desiredLANMode = 2; // PVG only
        public EditableDouble desiredArgP = new EditableDouble(0); // PVG only
        public int desiredArgPMode = 2; // PVG only
        public int timedAscentMode = 0; // 0 = NONE, 1 = PLANE, 2 = RENDEZVOUS, 3 = INTERPLANETARY

        [Persistent(pass = (int)(Pass.Global))]
        public bool showSettings = true;
        [Persistent(pass = (int)(Pass.Global))]
        public bool showTargeting = true;
        [Persistent(pass = (int)(Pass.Global))]
        public bool showGuidanceSettings = true;
        [Persistent(pass = (int)(Pass.Global))]
        public bool showStatus = true;

        public MechJebModuleAscentAutopilot autopilot { get { return core.GetComputerModule<MechJebModuleAscentAutopilot>(); } }
        public MechJebModuleAscentPVG pvgascent { get { return core.GetComputerModule<MechJebModuleAscentPVG>(); } }
        public MechJebModuleAscentGT gtascent { get { return core.GetComputerModule<MechJebModuleAscentGT>(); } }
        private MechJebModuleStageStats stats { get { return core.GetComputerModule<MechJebModuleStageStats>(); } }
        private FuelFlowSimulation.Stats[] atmoStats { get { return stats.atmoStats; } }

        private ascentType ascentPathIdx;
        private pvgTargetType pvgTargetIdx;

        MechJebModuleAscentNavBall navBall;

        private void Apply()
        {
            // copy state to autopilot
            autopilot.ascentPathIdxPublic = ascentPathIdx;
            autopilot.pvgTargetIdxPublic = pvgTargetIdx;
            autopilot.desiredOrbitAltitude = desiredOrbitAltitude.val;
            autopilot.desiredAltitude = desiredAltitude.val;
            autopilot.desiredPeriapsis = desiredPeriapsis.val;
            autopilot.desiredApoapsis = desiredApoapsis.val;
            autopilot.desiredSMA = desiredSMA.val;
            autopilot.desiredECC = desiredECC.val;
            autopilot.desiredFPA = desiredFPA.val;
            autopilot.maxStages = maxStages.val;
            autopilot.desiredShapeMode = desiredShapeMode;
            autopilot.desiredInclination = desiredInclination.val;
            autopilot.desiredIncMode = desiredIncMode;
            autopilot.desiredLAN = desiredLAN.val;
            autopilot.desiredLANMode = desiredLANMode;
            autopilot.desiredArgP = desiredArgP.val;
            autopilot.desiredArgPMode = desiredArgPMode;
            autopilot.timedAscentMode = timedAscentMode;
            autopilot.StartCountdown();
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (autopilot != null)
            {
                // copy state from autopilot
                ascentPathIdx = autopilot.ascentPathIdxPublic;
                pvgTargetIdx = autopilot.pvgTargetIdxPublic;
                desiredOrbitAltitude.val = autopilot.desiredOrbitAltitude;
                desiredAltitude.val = autopilot.desiredAltitude;
                desiredPeriapsis.val = autopilot.desiredPeriapsis;
                desiredApoapsis.val = autopilot.desiredApoapsis;
                desiredSMA.val = autopilot.desiredSMA;
                desiredECC.val = autopilot.desiredECC;
                desiredFPA.val = autopilot.desiredFPA;
                maxStages.val = autopilot.maxStages;
                desiredShapeMode = autopilot.desiredShapeMode;
                desiredInclination.val = autopilot.desiredInclination;
                desiredIncMode = autopilot.desiredIncMode;
                desiredLAN.val = autopilot.desiredLAN;
                desiredLANMode = autopilot.desiredLANMode;
                desiredArgP.val = autopilot.desiredArgP;
                desiredArgPMode = autopilot.desiredArgPMode;
                timedAscentMode = autopilot.timedAscentMode;
            }
            navBall = core.GetComputerModule<MechJebModuleAscentNavBall>();
        }

        public override void OnModuleDisabled()
        {
            timedAscentMode = 0;
        }

        [GeneralInfoItem("Toggle Ascent Navball Guidance", InfoItem.Category.Misc, showInEditor = false)]
            public void ToggleAscentNavballGuidanceInfoItem()
            {
                if (navBall != null)
                {
                    if (navBall.NavBallGuidance)
                    {
                        if (GUILayout.Button(Localizer.Format("#MechJeb_NavBallGuidance_btn1")))//Hide ascent navball guidance
                            navBall.NavBallGuidance = false;
                    }
                    else
                    {
                        if (GUILayout.Button(Localizer.Format("#MechJeb_NavBallGuidance_btn2")))//Show ascent navball guidance
                            navBall.NavBallGuidance = true;
                    }
                }
            }

        public static GUIStyle btNormal, btActive;

        public bool NeedsApply()
        {
            if ( ascentPathIdx != autopilot.ascentPathIdxPublic )
                return true;
            if ( pvgTargetIdx != autopilot.pvgTargetIdxPublic )
                return true;
            if ( desiredShapeMode != autopilot.desiredShapeMode )
                return true;
            if ( desiredIncMode != autopilot.desiredIncMode )
                return true;
            if ( desiredLANMode != autopilot.desiredLANMode )
                return true;
            if ( desiredArgPMode != autopilot.desiredArgPMode )
                return true;
            if ( desiredAltitude != autopilot.desiredAltitude )
                return true;
            if ( desiredOrbitAltitude != autopilot.desiredOrbitAltitude )
                return true;
            if ( desiredShapeMode == 0 && desiredPeriapsis != autopilot.desiredPeriapsis )
                return true;
            if ( desiredShapeMode == 0 && desiredApoapsis != autopilot.desiredApoapsis )
                return true;
            if ( desiredSMA != autopilot.desiredSMA )
                return true;
            if ( desiredECC != autopilot.desiredECC )
                return true;
            if ( desiredFPA != autopilot.desiredFPA )
                return true;
            if ( ( ascentPathIdx != ascentType.PVG || desiredIncMode == 0 ) && desiredInclination != autopilot.desiredInclination )
                return true;
            if ( desiredLANMode == 0 && desiredLAN != autopilot.desiredLAN )
                return true;
            if ( desiredArgPMode == 0 && desiredArgP != autopilot.desiredArgP )
                return true;
            if ( maxStages != autopilot.maxStages )
                return true;
            if ( timedAscentMode != autopilot.timedAscentMode )
                return true;

            return false;
        }

        protected override void WindowGUI(int windowID)
        {
            if (btNormal == null)
            {
                btNormal = new GUIStyle(GUI.skin.button);
                btNormal.normal.textColor = btNormal.focused.textColor = Color.white;
                btNormal.hover.textColor = btNormal.active.textColor = Color.yellow;
                btNormal.onNormal.textColor = btNormal.onFocused.textColor = btNormal.onHover.textColor = btNormal.onActive.textColor = Color.green;

                btActive = new GUIStyle(btNormal);
                btActive.active = btActive.onActive;
                btActive.normal = btActive.onNormal;
                btActive.onFocused = btActive.focused;
                btActive.hover = btActive.onHover;
            }

            GUILayout.BeginVertical();

            if (autopilot != null)
            {
                if (autopilot.enabled)
                {
                    if (GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button1")))//Disengage autopilot
                    {
                        autopilot.users.Remove(this);
                    }
                }
                else
                {
                    if (GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button2")))//Engage autopilot
                    {
                        if (NeedsApply())
                            Apply();
                        autopilot.users.Add(this);
                    }
                }
                if (ascentPathIdx == ascentType.PVG)
                {
                    if (GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button3")))//Reset Guidance (DO NOT PRESS)
                        core.guidance.Reset();


                    GUILayout.BeginHorizontal(); // EditorStyles.toolbar);

                    if ( GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button4"), showTargeting ? btActive : btNormal, GUILayout.ExpandWidth(true)) ) // TARG
                        showTargeting = !showTargeting;
                    if ( GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button5"), showGuidanceSettings ? btActive : btNormal, GUILayout.ExpandWidth(true)) ) // GUID
                        showGuidanceSettings = !showGuidanceSettings;
                    if ( GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button6"), showSettings ? btActive : btNormal, GUILayout.ExpandWidth(true)) ) // OPTS
                        showSettings = !showSettings;
                    if ( GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button7"), showStatus ? btActive : btNormal, GUILayout.ExpandWidth(true)) ) // STATUS
                        showStatus = !showStatus;
                    GUILayout.EndHorizontal();
                }
                else if (ascentPathIdx == ascentType.GRAVITYTURN)
                {
                    GUILayout.BeginHorizontal(); // EditorStyles.toolbar);
                    if ( GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button8"), showTargeting ? btActive : btNormal, GUILayout.ExpandWidth(true)) ) // TARG
                        showTargeting = !showTargeting;
                    if ( GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button9"), showGuidanceSettings ? btActive : btNormal, GUILayout.ExpandWidth(true)) ) // GUID
                        showGuidanceSettings = !showGuidanceSettings;
                    if ( GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button10"), showSettings ? btActive : btNormal, GUILayout.ExpandWidth(true)) ) // OPTS
                        showSettings = !showSettings;
                    GUILayout.EndHorizontal();
                }
                else if (ascentPathIdx == ascentType.CLASSIC)
                {
                    GUILayout.BeginHorizontal(); // EditorStyles.toolbar);
                    if ( GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button11"), showTargeting ? btActive : btNormal, GUILayout.ExpandWidth(true)) ) // TARG
                        showTargeting = !showTargeting;
                    if ( GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button12"), showSettings ? btActive : btNormal, GUILayout.ExpandWidth(true)) ) //OPTS
                        showSettings = !showSettings;
                    GUILayout.EndHorizontal();
                }

                if (showTargeting)
                {
                    if (ascentPathIdx == ascentType.PVG)
                    {
                        string[] modeStringsNOFRE = { "MAN", "TGT" };
                        string[] modeStringsINC = { "MAN", "TGT", "CUR" };
                        string[] modeStrings = { "MAN", "TGT", "FRE" };
                        int oldDesiredShapeMode = desiredShapeMode;
                        int oldDesiredIncMode = desiredIncMode;
                        int oldDesiredLANMode = desiredLANMode;
                        int oldDesiredArgPMode = desiredArgPMode;
                        Orbit oldTarget = null;

                        float gridWidth = 130;
                        float leftWidth = 35;
                        float textWidth = 65;

                        GUILayout.BeginHorizontal();
                        pvgTargetIdx = (pvgTargetType)GuiUtils.ComboBox2.Box((int)pvgTargetIdx, autopilot.pvgTargetList, this);
                        GUILayout.EndHorizontal();

                        if (pvgTargetIdx == pvgTargetType.KEPLER_HUMAN || pvgTargetIdx == pvgTargetType.FLIGHTANGLE_HUMAN)
                        {
                            GUILayout.BeginHorizontal();
                            if ( core.target.NormalTargetExists && desiredShapeMode == 1 ) // target
                                GuiUtils.AlternateTextBox("PeA:", ( core.target.TargetOrbit.PeA / 1000.0 ).ToString(), "km", leftWidth, textWidth);
                            else // manual
                                GuiUtils.AlternateTextBox("PeA:", desiredPeriapsis, "km", leftWidth, textWidth);
                            desiredShapeMode = GUILayout.SelectionGrid(desiredShapeMode, modeStringsNOFRE, 2, btNormal, GUILayout.Width(2.0f*gridWidth/3.0f));
                            GUILayout.Space(gridWidth/3.0f);
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            if ( core.target.NormalTargetExists && desiredShapeMode == 1 ) // target
                                GuiUtils.AlternateTextBox("ApA:", ( core.target.TargetOrbit.ApA / 1000.0 ).ToString(), "km", leftWidth, textWidth);
                            else // manual
                                GuiUtils.AlternateTextBox("ApA:", desiredApoapsis, "km", leftWidth, textWidth);
                            desiredShapeMode = GUILayout.SelectionGrid(desiredShapeMode, modeStringsNOFRE, 2, btNormal, GUILayout.Width(2.0f*gridWidth/3.0f));
                            GUILayout.Space(gridWidth/3.0f);
                            GUILayout.EndHorizontal();
                        }
                        if (pvgTargetIdx == pvgTargetType.KEPLER || pvgTargetIdx == pvgTargetType.FLIGHTANGLE)
                        {
                            GUILayout.BeginHorizontal();
                            if ( core.target.NormalTargetExists && desiredShapeMode == 1 ) // target
                                GuiUtils.AlternateTextBox("SMA:", ( core.target.TargetOrbit.semiMajorAxis / 1000.0 ).ToString(), "km", leftWidth, textWidth);
                            else // manual
                                GuiUtils.AlternateTextBox("SMA:", desiredSMA, "km", leftWidth, textWidth);
                            desiredShapeMode = GUILayout.SelectionGrid(desiredShapeMode, modeStringsNOFRE, 2, btNormal, GUILayout.Width(2.0f*gridWidth/3.0f));
                            GUILayout.Space(gridWidth/3.0f);
                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal();
                            if ( core.target.NormalTargetExists && desiredShapeMode == 1 ) // target
                                GuiUtils.AlternateTextBox("Ecc:", ( core.target.TargetOrbit.eccentricity ).ToString(), "", leftWidth, textWidth);
                            else // manual
                                GuiUtils.AlternateTextBox("Ecc:", desiredECC, "", leftWidth, textWidth);
                            desiredShapeMode = GUILayout.SelectionGrid(desiredShapeMode, modeStringsNOFRE, 2, btNormal, GUILayout.Width(2.0f*gridWidth/3.0f));
                            GUILayout.Space(gridWidth/3.0f);
                            GUILayout.EndHorizontal();
                        }

                        if (pvgTargetIdx == pvgTargetType.MAXNRG)
                        {
                            GUILayout.BeginHorizontal();
                            GuiUtils.AlternateTextBox("ALT:", desiredAltitude, "km", leftWidth, textWidth);
                            GUILayout.EndHorizontal();
                        }

                        if (pvgTargetIdx == pvgTargetType.FLIGHTANGLE || pvgTargetIdx == pvgTargetType.FLIGHTANGLE_HUMAN || pvgTargetIdx == pvgTargetType.MAXNRG)
                        {
                            GUILayout.BeginHorizontal();
                            GuiUtils.AlternateTextBox("FPA:", desiredFPA, "º", leftWidth, textWidth);
                            GUILayout.EndHorizontal();
                        }

                        GUILayout.BeginHorizontal();
                        if ( core.target.NormalTargetExists && desiredIncMode == 1 ) // target
                            GuiUtils.AlternateTextBox("Inc:", core.target.TargetOrbit.inclination.ToString(), "º", leftWidth, textWidth);
                        else if ( desiredIncMode == 2 ) // "free" is always just "current"
                            GuiUtils.AlternateTextBox("Inc:", vessel.orbit.inclination.ToString(), "º", leftWidth, textWidth);
                        else // manual
                            GuiUtils.AlternateTextBox("Inc:", desiredInclination.text, "º", leftWidth, textWidth);
                        desiredIncMode = GUILayout.SelectionGrid(desiredIncMode, modeStringsINC, 3, btNormal, GUILayout.Width(gridWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        if ( core.target.NormalTargetExists && desiredLANMode == 1 ) // target
                            GuiUtils.AlternateTextBox("LAN:", core.target.TargetOrbit.LAN.ToString(), "º", leftWidth, textWidth);
                        else // manual or free
                            GuiUtils.AlternateTextBox("LAN:", desiredLAN, "º", leftWidth, textWidth);
                        desiredLANMode = GUILayout.SelectionGrid(desiredLANMode, modeStrings, 3, btNormal, GUILayout.Width(gridWidth));
                        GUILayout.EndHorizontal();

                        if ( pvgTargetIdx == pvgTargetType.KEPLER_HUMAN || pvgTargetIdx == pvgTargetType.KEPLER )
                        {
                            GUILayout.BeginHorizontal();
                            if ( core.target.NormalTargetExists && desiredArgPMode == 1 ) // target
                                GuiUtils.AlternateTextBox("ArgP:", core.target.TargetOrbit.argumentOfPeriapsis.ToString(), "º", leftWidth, textWidth);
                            else // manual or free
                                GuiUtils.AlternateTextBox("ArgP:", desiredArgP, "º", leftWidth, textWidth);
                            desiredArgPMode = GUILayout.SelectionGrid(desiredArgPMode, modeStrings, 3, btNormal, GUILayout.Width(gridWidth));
                            GUILayout.EndHorizontal();
                        }

                        if (pvgTargetIdx == pvgTargetType.MAXNRG)
                        {
                            GUILayout.BeginHorizontal();
                            GuiUtils.AlternateTextBox("Stages:", maxStages, "", 120, 75);
                            GUILayout.EndHorizontal();
                        }

                        // fix if someone tries to select target without a target or the target goes away
                        if ( !core.target.NormalTargetExists )
                        {
                            if ( desiredShapeMode == 1 )
                                desiredShapeMode = ( oldDesiredShapeMode != 1 ) ? oldDesiredShapeMode : 0;
                            if ( desiredIncMode == 1 )
                                desiredIncMode = ( oldDesiredIncMode != 1 ) ? oldDesiredIncMode : 2;
                            if ( desiredLANMode == 1 )
                                desiredLANMode = ( oldDesiredLANMode != 1 ) ? oldDesiredLANMode : 2;
                            if ( desiredArgPMode == 1 )
                                desiredArgPMode = ( oldDesiredArgPMode != 1 ) ? oldDesiredArgPMode : 2;
                        }

                        // force reintialization of the launch countdown if the mode switches
                        if ( oldDesiredLANMode != desiredLANMode )
                            timedAscentMode = 0;

                        // also force reinitialization if the target orbit changes
                        if ( oldTarget != core.target.TargetOrbit )
                            timedAscentMode = 0;

                        oldTarget = core.target.TargetOrbit;

                        // impossible ApAs result in circularization
                        if ( desiredApoapsis >= 0 && desiredApoapsis < desiredPeriapsis )
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.normal.textColor = Color.yellow;
                            GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label3"), s);//Ap < Pe: circularizing orbit
                        }

                        // negative ApAs result in hyperbolic orbits (which might be a nasty surprise if unintended)
                        if ( desiredApoapsis < 0 )
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.normal.textColor = XKCDColors.Orange;
                            GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label4"), s);//Hyperbolic target orbit (neg Ap)
                        }

                        // impossible ApAs result in circularization
                        if ( ( desiredArgPMode == 0 || desiredArgPMode == 1 ) && desiredLANMode == 2 )
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.normal.textColor = Color.red;
                            GUILayout.Label("Fixed ArgP without Fixed LAN may have convergence difficulties ", s);
                        }
                    }
                    else
                    {
                        GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Ascent_label5"), desiredOrbitAltitude, "km"); //Orbit altitude

                        GUIStyle si = new GUIStyle(GUI.skin.label);
                        if (Math.Abs(desiredInclination) < Math.Abs(vesselState.latitude) - 2.001)
                            si.onHover.textColor = si.onNormal.textColor = si.normal.textColor = XKCDColors.Orange;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label6"), si, GUILayout.ExpandWidth(true)); //Orbit inc.
                        desiredInclination.text = GUILayout.TextField(desiredInclination.text, GUILayout.ExpandWidth(true), GUILayout.Width(100));
                        GUILayout.Label("º", GUILayout.ExpandWidth(false));
                        if (GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button13"))) //Current
                            desiredInclination.val = vesselState.latitude;
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        if (Math.Abs(desiredInclination) < Math.Abs(vesselState.latitude) - 2.001)
                            GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label7", Math.Abs(vesselState.latitude) - Math.Abs(desiredInclination)), si); //inc {0:F1}º below current latitude
                        GUILayout.EndHorizontal();
                    }
                }

                if (showGuidanceSettings)
                {
                    if (ascentPathIdx == ascentType.GRAVITYTURN)
                    {
                        GUILayout.BeginVertical();

                        GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Ascent_label8"), gtascent.turnStartAltitude, "km");//Turn start altitude:
                        GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Ascent_label9"), gtascent.turnStartVelocity, "m/s");//Turn start velocity:
                        GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Ascent_label10"), gtascent.turnStartPitch, "deg");//Turn start pitch:
                        GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Ascent_label11"), gtascent.intermediateAltitude, "km");//Intermediate altitude:
                        GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Ascent_label12"), gtascent.holdAPTime, "s");//Hold AP Time:

                        GUILayout.EndVertical();
                    }
                    else if (ascentPathIdx == ascentType.PVG)
                    {
                        float leftWidth = 120;
                        float textWidth = 75;
                        float rightWidth = 40;

                        GUILayout.BeginVertical();
                        GuiUtils.AlternateTextBox(Localizer.Format("#MechJeb_Ascent_label13"), pvgascent.pitchStartVelocity, "m/s", leftWidth, textWidth, rightWidth);//Booster Pitch start:
                        GuiUtils.AlternateTextBox(Localizer.Format("#MechJeb_Ascent_label14"), pvgascent.pitchRate, "°/s", leftWidth, textWidth, rightWidth);//Booster Pitch rate:
                        GuiUtils.AlternateTextBox(Localizer.Format("#MechJeb_Ascent_label15"), core.guidance.pvgInterval, "s", leftWidth, textWidth, rightWidth);//Guidance Interval:
                        if ( core.guidance.pvgInterval < 1 || core.guidance.pvgInterval > 30 )
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.normal.textColor = Color.yellow;
                            GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label16"), s);//Guidance intervals are limited to between 1s and 30s
                        }
                        GuiUtils.AlternateTextBox(Localizer.Format("#MechJeb_Ascent_label17"), autopilot.limitQa, "Pa-rad", leftWidth, textWidth, rightWidth);//Qα limit
                        if ( autopilot.limitQa < 100 || autopilot.limitQa > 4000 )
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.normal.textColor = Color.yellow;

                            if ( autopilot.limitQa < 100 )
                                GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label18"), s);//Qα limit cannot be set to lower than 100 Pa-rad
                            else if ( autopilot.limitQa > 10000 )
                                GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label19"), s);//Qα limit cannot be set to higher than 10000 Pa-rad
                            else
                                GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label20"), s);//Qα limit is recommended to be 1000 to 4000 Pa-rad
                        }
                        pvgascent.omitCoast = GUILayout.Toggle(pvgascent.omitCoast, Localizer.Format("#MechJeb_Ascent_checkbox1"));//Omit Coast
                        GUILayout.EndVertical();
                    }
                }

                GUIStyle r = new GUIStyle(GUI.skin.button);

                if ( NeedsApply() )
                    r.onHover.textColor = r.normal.textColor = Color.red;

                if (GUILayout.Button("Lock Guidance Target", r))
                {
                    GUILayout.BeginHorizontal();
                    Apply();
                    GUILayout.EndHorizontal();
                }

                autopilot.limitQaEnabled = ( ascentPathIdx == ascentType.PVG );  // this is mandatory for PVG

                if (showSettings)
                {
                    ToggleAscentNavballGuidanceInfoItem();
                    if ( ascentPathIdx != ascentType.PVG )
                    {
                        core.thrust.LimitToPreventOverheatsInfoItem();
                        //core.thrust.LimitToTerminalVelocityInfoItem();
                        core.thrust.LimitToMaxDynamicPressureInfoItem();
                        core.thrust.LimitAccelerationInfoItem();
                        core.thrust.LimitThrottleInfoItem();
                        core.thrust.LimiterMinThrottleInfoItem();
                        core.thrust.LimitElectricInfoItem();
                    }
                    else
                    {
                        core.thrust.LimitToPreventOverheatsInfoItem();
                        //core.thrust.LimitToTerminalVelocityInfoItem();
                        core.thrust.LimitToMaxDynamicPressureInfoItem();
                        core.thrust.LimitAccelerationInfoItem();
                        //core.thrust.LimitThrottleInfoItem();
                        core.thrust.LimiterMinThrottleInfoItem();
                        //core.thrust.LimitElectricInfoItem();
                        core.thrust.limitThrottle = false;
                        core.thrust.electricThrottle = false;
                    }
                    core.thrust.limitToTerminalVelocity = false;

                    GUILayout.BeginHorizontal();
                    autopilot.forceRoll = GUILayout.Toggle(autopilot.forceRoll, Localizer.Format("#MechJeb_Ascent_checkbox2"));//Force Roll
                    if (autopilot.forceRoll)
                    {
                        GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Ascent_label22"), autopilot.verticalRoll, "º", 30f);//climb
                        GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Ascent_label23"), autopilot.turnRoll, "º", 30f);//turn
                    }
                    GUILayout.EndHorizontal();

                    if (ascentPathIdx != ascentType.PVG)
                    {
                        GUILayout.BeginHorizontal();
                        GUIStyle s = new GUIStyle(GUI.skin.toggle);
                        if (autopilot.limitingAoA) s.onHover.textColor = s.onNormal.textColor = Color.green;
                        autopilot.limitAoA = GUILayout.Toggle(autopilot.limitAoA, Localizer.Format("#MechJeb_Ascent_checkbox3"), s, GUILayout.ExpandWidth(true));//Limit AoA to
                        autopilot.maxAoA.text = GUILayout.TextField(autopilot.maxAoA.text, GUILayout.Width(30));
                        GUILayout.Label("º (" + autopilot.currentMaxAoA.ToString("F1") + "°)", GUILayout.ExpandWidth(true));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(25);
                        if (autopilot.limitAoA)
                        {
                            GUIStyle sl = new GUIStyle(GUI.skin.label);
                            if (autopilot.limitingAoA && vesselState.dynamicPressure < autopilot.aoALimitFadeoutPressure)
                                sl.normal.textColor = sl.hover.textColor = Color.green;
                            GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Ascent_label24"), autopilot.aoALimitFadeoutPressure, "Pa", 50, sl);//Dynamic Pressure Fadeout
                        }
                        GUILayout.EndHorizontal();
                        autopilot.limitQaEnabled = false; // this is only for PVG
                    }

                    if ( ascentPathIdx == ascentType.CLASSIC )
                    {
                        // corrective steering only applies to Classic
                        GUILayout.BeginHorizontal();
                        autopilot.correctiveSteering = GUILayout.Toggle(autopilot.correctiveSteering, Localizer.Format("#MechJeb_Ascent_checkbox4"), GUILayout.ExpandWidth(false));//Corrective steering
                        if (autopilot.correctiveSteering)
                        {
                            GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label25"), GUILayout.ExpandWidth(false));//Gain
                            autopilot.correctiveSteeringGain.text = GUILayout.TextField(autopilot.correctiveSteeringGain.text, GUILayout.Width(40));
                        }
                        GUILayout.EndHorizontal();
                    }

                    autopilot.autostage = GUILayout.Toggle(autopilot.autostage, Localizer.Format("#MechJeb_Ascent_checkbox5"));//Autostage
                    if (autopilot.autostage) core.staging.AutostageSettingsInfoItem();

                    autopilot.autodeploySolarPanels = GUILayout.Toggle(autopilot.autodeploySolarPanels,
                            Localizer.Format("#MechJeb_Ascent_checkbox6"));//Auto-deploy solar panels

                    autopilot.autoDeployAntennas = GUILayout.Toggle(autopilot.autoDeployAntennas,
                            Localizer.Format("#MechJeb_Ascent_checkbox7"));//Auto-deploy antennas

                    GUILayout.BeginHorizontal();
                    core.node.autowarp = GUILayout.Toggle(core.node.autowarp, Localizer.Format("#MechJeb_Ascent_checkbox8"));//Auto-warp
                    if ( ascentPathIdx != ascentType.PVG )
                    {
                        autopilot.skipCircularization = GUILayout.Toggle(autopilot.skipCircularization, Localizer.Format("#MechJeb_Ascent_checkbox9"));//Skip Circularization
                    }
                    else
                    {
                        // skipCircularization is always true for Optimizer
                        autopilot.skipCircularization = true;
                    }
                    GUILayout.EndHorizontal();
                }

                if (showStatus)
                {
                    if (ascentPathIdx == ascentType.PVG)
                    {
                        if (core.guidance.solution != null)
                        {
                            for(int i = core.guidance.solution.num_segments; i > 0; i--)
                                GUILayout.Label(String.Format("{0}: {1}", i, core.guidance.solution.ArcString(vesselState.time, i-1)));
                        }

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(String.Format("vgo: {0:F1}", core.guidance.vgo), GUILayout.Width(100));
                        GUILayout.Label(String.Format("heading: {0:F1}", core.guidance.heading), GUILayout.Width(100));
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(String.Format("tgo: {0:F3}", core.guidance.tgo), GUILayout.Width(100));
                        GUILayout.Label(String.Format("pitch: {0:F1}", core.guidance.pitch), GUILayout.Width(100));
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUIStyle si = new GUIStyle(GUI.skin.label);
                        if ( core.guidance.isStable() )
                            si.onHover.textColor = si.onNormal.textColor = si.normal.textColor = XKCDColors.Green;
                        else if ( core.guidance.isInitializing() || core.guidance.status == PVGStatus.FINISHED )
                            si.onHover.textColor = si.onNormal.textColor = si.normal.textColor = XKCDColors.Orange;
                        else
                            si.onHover.textColor = si.onNormal.textColor = si.normal.textColor = XKCDColors.Red;
                        GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label26") + core.guidance.status, si);//Guidance Status:
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label27") + core.guidance.successful_converges, GUILayout.Width(100));//converges:
                        GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label28") + core.guidance.last_lm_status, GUILayout.Width(100));//status:
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("n: " + core.guidance.last_lm_iteration_count + "(" + core.guidance.max_lm_iteration_count + ")", GUILayout.Width(100));
                        GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label29") + GuiUtils.TimeToDHMS(core.guidance.staleness));//staleness:
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(String.Format("znorm: {0:G5}", core.guidance.last_znorm));
                        GUILayout.EndHorizontal();
                        if ( core.guidance.last_failure_cause != null )
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.normal.textColor = Color.red;
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label30") + core.guidance.last_failure_cause, s);//LAST FAILURE:
                            GUILayout.EndHorizontal();
                        }

                        if ( vessel.situation != Vessel.Situations.LANDED && vessel.situation != Vessel.Situations.PRELAUNCH && vessel.situation != Vessel.Situations.SPLASHED )
                        {
                            double m0 = atmoStats[vessel.currentStage].startMass;
                            double thrust = atmoStats[vessel.currentStage].startThrust;

                            if (Math.Abs(vesselState.mass - m0) / m0 > 0.01)
                            {
                                GUIStyle s = new GUIStyle(GUI.skin.label);
                                s.normal.textColor = Color.yellow;
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(String.Format(Localizer.Format("#MechJeb_Ascent_label31") +"{0:F1}%", (vesselState.mass - m0) / m0 * 100.0 ), s);//MASS IS OFF BY
                                GUILayout.EndHorizontal();
                            }

                            if (Math.Abs(vesselState.thrustCurrent - thrust) / thrust > 0.01)
                            {
                                GUIStyle s = new GUIStyle(GUI.skin.label);
                                s.normal.textColor = Color.yellow;
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(String.Format(Localizer.Format("#MechJeb_Ascent_label32") +"{0:F1}%", (vesselState.thrustCurrent - thrust) / thrust * 100.0 ), s);//THRUST IS OFF BY
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                }

                if (vessel.LandedOrSplashed)
                {
                        if ((ascentPathIdx != ascentType.PVG && core.target.NormalTargetExists) || (ascentPathIdx == ascentType.PVG && (desiredLANMode == 1 || desiredLANMode == 0)))
                        {
                            if (core.node.autowarp)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label33"), GUILayout.ExpandWidth(true));//Launch countdown:
                                autopilot.warpCountDown.text = GUILayout.TextField(autopilot.warpCountDown.text,
                                        GUILayout.Width(60));
                                GUILayout.Label("s", GUILayout.ExpandWidth(false));
                                GUILayout.EndHorizontal();
                            }
                            if (timedAscentMode == 0)
                            {
                                if ( ascentPathIdx == ascentType.PVG )
                                {
                                    // FIXME: this is a bit uselessly trivial now and needs to get refactored out
                                    timedAscentMode = 1;
                                }
                                else
                                {
                                    GUILayout.BeginHorizontal();
                                    if (GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button14"), GUILayout.ExpandWidth(false)))//Launch to rendezvous:
                                    {
                                        timedAscentMode = 2;
                                        autopilot.timedAscentMode = timedAscentMode;
                                        autopilot.StartCountdown();
                                    }
                                    autopilot.launchPhaseAngle.text = GUILayout.TextField(autopilot.launchPhaseAngle.text,
                                            GUILayout.Width(60));
                                    GUILayout.Label("º", GUILayout.ExpandWidth(false));
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    if (GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button15"), GUILayout.ExpandWidth(false)))//Launch into plane of target
                                    {
                                        timedAscentMode = 1;
                                        autopilot.timedAscentMode = timedAscentMode;
                                        autopilot.StartCountdown();
                                    }
                                    autopilot.launchLANDifference.text = GUILayout.TextField(
                                            autopilot.launchLANDifference.text, GUILayout.Width(60));
                                    GUILayout.Label("º", GUILayout.ExpandWidth(false));
                                    GUILayout.EndHorizontal();

                                    if (core.target.TargetOrbit.referenceBody == orbit.referenceBody.referenceBody)
                                    {
                                        if (GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button16")))//Launch at interplanetary window
                                        {
                                            timedAscentMode = 3;
                                            autopilot.timedAscentMode = timedAscentMode;
                                            autopilot.StartCountdown();
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            timedAscentMode = 0;
                            if ( ascentPathIdx != ascentType.PVG )
                            {
                                GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label34"));//Select a target for a timed launch.
                                GUILayout.Label("Select a target for a timed launch.");
                            }
                        }

                        if ( timedAscentMode != 0 )
                        {
                            string message = "";
                            if ( timedAscentMode == 3 )
                            {
                                message = Localizer.Format("#MechJeb_Ascent_msg1");//Launching at interplanetary window
                            }
                            else if ( timedAscentMode == 1 )
                            {
                                message = Localizer.Format("#MechJeb_Ascent_msg2");//Launching to target plane
                            }
                            else if ( timedAscentMode == 2 )
                            {
                                message = "#MechJeb_Ascent_msg3";//Launching to rendezvous
                            }

                            if (autopilot.tMinus > 3*vesselState.deltaT)
                            {
                                message += ": T-" + GuiUtils.TimeToDHMS(autopilot.tMinus, 1);
                            }

                            GUILayout.Label(message);

                            if ( ascentPathIdx != ascentType.PVG )
                            {
                                if (GUILayout.Button(Localizer.Format("#MechJeb_Ascent_button17")))//Abort
                                if (GUILayout.Button("Abort"))
                                {
                                     timedAscentMode = 0;
                                     autopilot.timedAscentMode = timedAscentMode;
                                }
                            }
                        }
                }

                if (autopilot.enabled)
                {
                    GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label35") + autopilot.status);//Autopilot status:
                }
                if (core.DeactivateControl)
                {
                    GUIStyle s = new GUIStyle(GUI.skin.label);
                    s.normal.textColor = Color.red;
                    GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label36"), s);//CONTROL DISABLED (AVIONICS)
                }
            }

            if (!vessel.patchedConicsUnlocked() && ascentPathIdx != ascentType.PVG)
            {
                GUILayout.Label(Localizer.Format("#MechJeb_Ascent_label37"));//"Warning: MechJeb is unable to circularize without an upgraded Tracking Station."
            }

            GUILayout.BeginHorizontal();
            ascentPathIdx = (ascentType)GuiUtils.ComboBox.Box((int)ascentPathIdx, autopilot.ascentPathList, this);
            GUILayout.EndHorizontal();

            if (autopilot.ascentMenu != null) autopilot.ascentMenu.enabled = GUILayout.Toggle(autopilot.ascentMenu.enabled, Localizer.Format("#MechJeb_Ascent_checkbox10"));//Edit ascent path

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }

        public override GUILayoutOption[] WindowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(240), GUILayout.Height(30) };
        }

        public override string GetName()
        {
            return Localizer.Format("#MechJeb_Ascent_title");//"Ascent Guidance"
        }
    }
}
