using System;
using KSP.UI.Screens;
using UnityEngine;

namespace MuMech
{
    public enum ascentType { CLASSIC, GRAVITYTURN, PVG };
    public enum pvgTargetType { KEPLER_HUMAN, KEPLER, FLIGHTANGLE_HUMAN, FLIGHTANGLE, MAXNRG };

    //Todo: -reimplement measurement of LPA
    //      -Figure out exactly how throttle-limiting should work and interact
    //       with the global throttle-limit option
    public class MechJebModuleAscentAutopilot : ComputerModule
    {
        public MechJebModuleAscentAutopilot(MechJebCore core) : base(core) { }

        public string status = "";

        // FIXME: use the proper hooks for on saving / loading confignode data to coerce the values

        // deliberately private, do not bypass
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        private int ascentPathIdx;

        // this is the public API for ascentPathIdx which is enum type and does wiring
        public ascentType ascentPathIdxPublic {
            get {
                return (ascentType) this.ascentPathIdx;
            }
            set {
                Debug.Log("set ascentPathIdxPublic in Autopilot");
                this.ascentPathIdx = (int) value;
                doWiring();
            }
        }

        // deliberately private, do not bypass
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        private int pvgTargetIdx;

        // this is the public API for pvgTargetIdx which is enum type and does wiring
        public pvgTargetType pvgTargetIdxPublic {
            get {
                return (pvgTargetType) this.pvgTargetIdx;
            }
            set {
                Debug.Log("set pvgTargetIdxPublic in Autopilot");
                this.pvgTargetIdx = (int) value;
                doWiring();
            }
        }

        // after manually loading the private ascentPathIdx (e.g. from a ConfigNode) this needs to be called to force the wiring
        public void doWiring()
        {
            Debug.Log("doWiring() called");
            ascentPath = ascentPathForType((ascentType)ascentPathIdx);
            ascentMenu = ascentMenuForType((ascentType)ascentPathIdx);
            disablePathModulesOtherThan(ascentPathIdx);
        }

        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredOrbitAltitude = 100000; // Classic+GT
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredAltitude = 185000; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredPeriapsis = 185000; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredApoapsis = 0; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredSMA = 6556000; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredECC = 0; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredFPA = 0; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public int maxStages = 2; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public int desiredShapeMode = 0; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredInclination = 0; // Classic+GT+PVG
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public int desiredIncMode = 0; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredLAN = 0; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public int desiredLANMode = 2; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public double desiredArgP = 0; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public int desiredArgPMode = 2; // PVG only
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public int timedAscentMode = 0; // 0 = NONE, 1 = PLANE, 2 = RENDEZVOUS, 3 = INTERPLANETARY

        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public bool autoThrottle = true;
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public bool correctiveSteering = false;
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDouble correctiveSteeringGain = 0.6; //control gain
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public bool forceRoll = true;
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDouble verticalRoll = new EditableDouble(90);
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDouble turnRoll = new EditableDouble(90);
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public bool autodeploySolarPanels = true;
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public bool autoDeployAntennas = true;
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public bool skipCircularization = false;
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public bool _autostage = true;
        public bool autostage
        {
            get { return _autostage; }
            set
            {
                bool changed = (value != _autostage);
                _autostage = value;
                if (changed)
                {
                    if (_autostage && this.enabled) core.staging.users.Add(this);
                    if (!_autostage) core.staging.users.Remove(this);
                }
            }
        }


        /* classic AoA limter */
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public bool limitAoA = true;
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDouble maxAoA = 5;
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDoubleMult aoALimitFadeoutPressure = new EditableDoubleMult(2500);
        public bool limitingAoA = false;

        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDouble limitQa = new EditableDouble(2000);
        public bool limitQaEnabled = false;

        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDouble launchPhaseAngle = 0;

        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDouble launchLANDifference = 0;

        [Persistent(pass = (int)(Pass.Global))]
        public EditableInt warpCountDown = 11;

        public bool timedLaunch = false;
        public double launchTime = 0;

        public double currentMaxAoA = 0;

        public double launchLatitude = 0 ;

        // useful to track time since launch event (works around bugs with physics starting early and vessel.launchTime being wildly off)
        // FIXME?: any time we lift off from a rock this should probably be set to that time?
        public double launchStarted;

        public double tMinus
        {
            get { return launchTime - vesselState.time; }
        }

        //internal state:
        enum AscentMode { PRELAUNCH, ASCEND, CIRCULARIZE };
        AscentMode mode;
        bool placedCircularizeNode = false;
        private double lastTMinus = 999;

        // wiring for launchStarted
        public void OnLaunch(EventReport report)
        {
            launchStarted = vesselState.time;
            Debug.Log("[MechJebModuleAscentAutopilot] LaunchStarted = " + launchStarted);
        }

        // wiring for launchStarted
        public override void OnStart(PartModule.StartState state)
        {
            launchStarted = -1;
            GameEvents.onLaunch.Add(OnLaunch);
        }

        private void FixupLaunchStart()
        {
            // continuously update the launchStarted time if we're sitting on the ground on the water anywhere (once we are not, then we've launched)
            if ( vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH || vessel.situation == Vessel.Situations.SPLASHED )
                launchStarted = vesselState.time;
        }

        // various events can cause launchStarted to be before or after vessel.launchTime, but the most recent one is so far always the most accurate
        // (physics wobbles can start vessel.launchTime (KSP's zero MET) early, while staging before engaging the autopilot can cause launchStarted to happen early)
        // this will only be valid AFTER launching
        public double MET { get { return vesselState.time - ( launchStarted > vessel.launchTime ? launchStarted : vessel.launchTime ); } }

        public override void OnModuleEnabled()
        {
            Debug.Log("MechJebModuleAscentAutopilot: Enabled");
            // since we cannot serialize enums, we serialize ascentPathIdx instead, but this bypasses the code in the property, so on module
            // enabling, we force that value back through the property to enforce sanity.
            doWiring();

            ascentPath.enabled = true;

            mode = AscentMode.PRELAUNCH;

            placedCircularizeNode = false;

            launchLatitude = vesselState.latitude;

            core.attitude.users.Add(this);
            core.thrust.users.Add(this);
            if (autostage) core.staging.users.Add(this);

            status = "Pre Launch";
        }

        public override void OnModuleDisabled()
        {
            Debug.Log("MechJebModuleAscentAutopilot: Disabled");
            ascentPath.enabled = false;
            core.attitude.attitudeDeactivate();
            if (!core.rssMode)
                core.thrust.ThrustOff();
            core.thrust.users.Remove(this);
            core.staging.users.Remove(this);

            if (placedCircularizeNode) core.node.Abort();

            status = "Off";
        }

        public void StartCountdown()
        {
            if ( timedAscentMode == 0 ) // none
                return;

            timedLaunch = true;
            lastTMinus = 999;

            // we update the desiredLAN/desiredInclination here and the AutopilotGuidance GUI should read it back off
            // of here when that is appropriate.
            //
            if ( timedAscentMode == 1 ) { // plane
                if ( ascentPathIdxPublic != ascentType.PVG )
                {
                    desiredInclination = MuUtils.Clamp(core.target.TargetOrbit.inclination, Math.Abs(vesselState.latitude), 180 - Math.Abs(vesselState.latitude));
                    desiredInclination *=
                        Math.Sign(Vector3d.Dot(core.target.TargetOrbit.SwappedOrbitNormal(),
                                    Vector3d.Cross(vesselState.CoM - mainBody.position, mainBody.transform.up)));
                    launchTime = vesselState.time + MinimumTimeToPlane(core.target.TargetOrbit.LAN, core.target.TargetOrbit.inclination, launchLANDifference);
                }
                else
                {
                    if ( desiredIncMode == 1 && core.target.NormalTargetExists ) // targ
                    {
                        desiredInclination = core.target.TargetOrbit.inclination;
                    }
                    else if ( desiredIncMode == 2 ) // current
                    {
                        desiredInclination = vessel.orbit.inclination;
                    }
                    if ( desiredLANMode == 1 && core.target.NormalTargetExists ) // targ
                    {
                        desiredLAN = core.target.TargetOrbit.LAN;
                    }
                    // desiredLANMode == 2 should never happen here
                    launchTime = vesselState.time + MinimumTimeToPlane(desiredLAN, desiredInclination, 0);
                }
            } else if ( timedAscentMode == 2 ) { // rendezvous
                launchTime = vesselState.time + LaunchTiming.TimeToPhaseAngle(launchPhaseAngle,
                            mainBody, vesselState.longitude, core.target.TargetOrbit);
            } else if ( timedAscentMode == 3 ) { // interplanetary
                //compute the desired launch date
                OrbitalManeuverCalculator.DeltaVAndTimeForHohmannTransfer(
                        mainBody.orbit, core.target.TargetOrbit, vesselState.time, out launchTime
                        );
                double desiredOrbitPeriod = 2 * Math.PI *
                    Math.Sqrt( Math.Pow(mainBody.Radius + desiredOrbitAltitude, 3) / mainBody.gravParameter );
                //launch just before the window, but don't try to launch in the past
                launchTime -= 3*desiredOrbitPeriod;
                launchTime = Math.Max(vesselState.time + warpCountDown, launchTime);
            }
        }

        public override void OnFixedUpdate()
        {
            Debug.Log("MechJebModuleAscentAutopilot: OnFixedUpdate");
            FixupLaunchStart();
            if (timedLaunch)
            {
                if (tMinus < 3*vesselState.deltaT || (tMinus > 10.0 && lastTMinus < 1.0))
                {
                    if (enabled && vesselState.thrustAvailable < 10E-4) // only stage if we have no engines active
                        StageManager.ActivateNextStage();
                    ascentPath.timedLaunchHook();  // let ascentPath modules do stuff edge triggered on launch starting
                    timedLaunch = false;
                }
                else
                {
                    if (core.node.autowarp)
                        core.warp.WarpToUT(launchTime - warpCountDown);
                }
                lastTMinus = tMinus;
            }
        }

        public override void Drive(FlightCtrlState s)
        {
            limitingAoA = false;

            switch (mode)
            {
                case AscentMode.PRELAUNCH:
                    DrivePrelaunch(s);
                    break;

                case AscentMode.ASCEND:
                    DriveAscent(s);
                    break;

                case AscentMode.CIRCULARIZE:
                    DriveCircularizationBurn(s);
                    break;
            }

        }

        void DriveDeployableComponents(FlightCtrlState s)
        {
            if (autodeploySolarPanels)
            {
                if (vesselState.altitudeASL > mainBody.RealMaxAtmosphereAltitude())
                {
                    core.solarpanel.ExtendAll();
                }
                else
                {
                    core.solarpanel.RetractAll();
                }
            }

            if (autoDeployAntennas)
            {
                if (vesselState.altitudeASL > mainBody.RealMaxAtmosphereAltitude())
                    core.antennaControl.ExtendAll();
                else
                    core.antennaControl.RetractAll();
            }
        }

        void DrivePrelaunch(FlightCtrlState s)
        {
            if (vessel.LiftedOff() && !vessel.Landed) {
                status = "Vessel is not landed, skipping pre-launch";
                mode = AscentMode.ASCEND;
                return;
            }

            if (autoThrottle) {
                core.thrust.ThrustOff();
            }

            core.attitude.AxisControl(false, false, false);

            if (timedLaunch && tMinus > 10.0)
            {
                status = "Pre Launch";
                return;
            }

            if (autodeploySolarPanels && mainBody.atmosphere)
            {
                core.solarpanel.RetractAll();
                if (core.solarpanel.AllRetracted()) {
                    Debug.Log("Prelaunch -> Ascend");
                    mode = AscentMode.ASCEND;
                }
                else
                {
                    status = "Retracting solar panels";
                }
            } else {
                mode = AscentMode.ASCEND;
            }
        }

        void DriveAscent(FlightCtrlState s)
        {
            if (timedLaunch)
            {
                status = "Awaiting liftoff";
                // kill the optimizer if it is running.
                core.guidance.enabled = false;

                core.attitude.AxisControl(false, false, false);
                return;
            }

            DriveDeployableComponents(s);

            if ( ascentPath.DriveAscent(s) ) {
                if (GameSettings.VERBOSE_DEBUG_LOG) { Debug.Log("Remaining in Ascent"); }
                status = ascentPath.status;
            } else {
                if (GameSettings.VERBOSE_DEBUG_LOG) { Debug.Log("Ascend -> Circularize"); }
                mode = AscentMode.CIRCULARIZE;
            }
        }

        void DriveCircularizationBurn(FlightCtrlState s)
        {
            if (!vessel.patchedConicsUnlocked() || skipCircularization)
            {
                this.users.Clear();
                return;
            }

            DriveDeployableComponents(s);

            if (placedCircularizeNode)
            {
                if (vessel.patchedConicSolver.maneuverNodes.Count == 0)
                {
                    MechJebModuleFlightRecorder recorder = core.GetComputerModule<MechJebModuleFlightRecorder>();
                    if (recorder != null) launchPhaseAngle = recorder.phaseAngleFromMark;
                    if (recorder != null) launchLANDifference = vesselState.orbitLAN - recorder.markLAN;

                    //finished circularize
                    this.users.Clear();
                    return;
                }
            }
            else
            {
                //place circularization node
                vessel.RemoveAllManeuverNodes();
                double UT = orbit.NextApoapsisTime(vesselState.time);
                //During the circularization burn, try to correct any inclination errors because it's better to combine the two burns.
                //  For example, if you're about to do a 1500 m/s circularization burn, if you combine a 200 m/s inclination correction
                //  into it, you actually only spend 1513 m/s to execute combined manuver.  Mechjeb should also do correction burns before
                //  this if possible, and this can't correct all errors... but it's better then nothing.
                //   (A better version of this should try to match inclination & LAN if target is specified)
                // FIXME? this inclination correction is unlikely to be at tha AN/DN and will throw the LAN off with anything other than high
                // TWR launches from equatorial launch sites -- should probably be made optional (or clip it if the correction is too large).
                Vector3d inclinationCorrection = OrbitalManeuverCalculator.DeltaVToChangeInclination(orbit, UT, Math.Abs(desiredInclination));
                Vector3d smaCorrection = OrbitalManeuverCalculator.DeltaVForSemiMajorAxis(orbit.PerturbedOrbit(UT, inclinationCorrection), UT,
                    desiredOrbitAltitude + mainBody.Radius);
                Vector3d dV = inclinationCorrection + smaCorrection;
                vessel.PlaceManeuverNode(orbit, dV, UT);
                placedCircularizeNode = true;

                core.node.ExecuteOneNode(this);
            }

            if (core.node.burnTriggered) status = "Circularizing";
            else status = "Coasting to circularization burn";
        }

        //////////////////////////////////////////////////
        // wiring for switching the different ascent types
        //////////////////////////////////////////////////

        public string[] ascentPathList = { "Classic Ascent Profile", "Stock-style GravityTurn™", "Primer Vector Guidance (RSS/RO)" };
        public string[] pvgTargetList = { "Keperian (ApA/PeA)", "Keplerian (SMA/ECC)", "Flightpath Angle (ApA/PeA)", "Flightpath Angle (SMA/ECC)", "Max Energy" };

        public MechJebModuleAscentBase ascentPath;
        public MechJebModuleAscentMenuBase ascentMenu;

        private void disablePathModulesOtherThan(int type)
        {
            foreach(int i in Enum.GetValues(typeof(ascentType)))
            {
                if (i != (int)type)
                    enablePathModules((ascentType)i, false);
            }
        }

        private void enablePathModules(ascentType type, bool enabled)
        {
            ascentPathForType(type).enabled = enabled;
            var menu = ascentMenuForType(type);
            if (menu != null)
                menu.enabled = enabled;
        }

        private MechJebModuleAscentBase ascentPathForType(ascentType type)
        {
            switch (type)
            {
                case ascentType.CLASSIC:
                    return core.GetComputerModule<MechJebModuleAscentClassic>();
                case ascentType.GRAVITYTURN:
                    return core.GetComputerModule<MechJebModuleAscentGT>();
                case ascentType.PVG:
                    return core.GetComputerModule<MechJebModuleAscentPVG>();
            }
            return null;
        }

        private MechJebModuleAscentMenuBase ascentMenuForType(ascentType type)
        {
            if ( type == ascentType.CLASSIC )
                return core.GetComputerModule<MechJebModuleAscentClassicMenu>();
            else
                return null;
        }

        // handles picking the smaller time to plane of northgoing and southgoing ground tracks
        public double MinimumTimeToPlane(double LAN, double inc, double LANDifference)
        {
            double one = TimeToPlane(LAN, inc, LANDifference);
            double two = TimeToPlane(LAN, -inc, LANDifference);
            return Math.Min(one, two);
        }

        // seconds of planetary rotation to meet the target plane defined by inc.  safe to call for inclination
        // below the current latitude.
        public double TimeToPlane(double LAN, double inc, double LANDifference)
        {
            Debug.Log("LAN = " + LAN + " inc = " + inc + " LANDiff = " + LANDifference);
            // alpha is the 90 degrees angle between the line of longitude and the equator and omitted
            double beta = OrbitalManeuverCalculator.HeadingForInclination(inc, vesselState.latitude) * UtilMath.Deg2Rad;
            double c = vesselState.latitude * UtilMath.Deg2Rad;
            //double gamma = Math.Acos(Math.Sin(beta) * Math.Cos(c)); // law of cosines
            // b is how many radians to the west of the launch site (-180, 180] that the LAN is
            double b = Math.Atan2( 2 * Math.Sin(beta), Math.Cos(beta) / Math.Tan(c/2) + Math.Tan(c/2) * Math.Cos(beta) ); // napier's analogies
            // LAN if we launched now
            double LANnow = vesselState.orbitLongitude - b * UtilMath.Rad2Deg;

            return MuUtils.ClampDegrees360( LAN - LANnow - LANDifference ) / 360 * mainBody.rotationPeriod;
        }
    }

    public abstract class MechJebModuleAscentMenuBase : DisplayModule
    {
        public MechJebModuleAscentMenuBase(MechJebCore core) : base(core) { }
    }

    public abstract class MechJebModuleAscentBase : ComputerModule
    {
        public MechJebModuleAscentBase(MechJebCore core) : base(core) { }

        public string status { get; set; }

        public MechJebModuleAscentAutopilot autopilot { get { return core.GetComputerModule<MechJebModuleAscentAutopilot>(); } }
        private MechJebModuleStageStats stats { get { return core.GetComputerModule<MechJebModuleStageStats>(); } }
        private FuelFlowSimulation.Stats[] vacStats { get { return stats.vacStats; } }

        public abstract bool DriveAscent(FlightCtrlState s);

        public Vector3d thrustVectorForNavball;

        //data used by ThrottleToRaiseApoapsis
        float raiseApoapsisLastThrottle = 0;
        double raiseApoapsisLastApR = 0;
        double raiseApoapsisLastUT = 0;
        MovingAverage raiseApoapsisRatePerThrottle = new MovingAverage(3, 0);

        public virtual void timedLaunchHook()
        {
            // triggered when timed launches start the actual launch
        }

        public override void OnModuleEnabled()
        {
            core.attitude.users.Add(this);
            core.thrust.users.Add(this);
        }

        public override void OnModuleDisabled()
        {
            core.attitude.attitudeDeactivate();
            if (!core.rssMode)
                core.thrust.ThrustOff();
            core.thrust.users.Remove(this);
        }

        //gives a throttle setting that reduces as we approach the desired apoapsis
        //so that we can precisely match the desired apoapsis instead of overshooting it
        protected float ThrottleToRaiseApoapsis(double currentApR, double finalApR)
        {
            float desiredThrottle;

            if (currentApR > finalApR + 5.0)
            {
                desiredThrottle = 0.0F; //done, throttle down
            }
            else if (raiseApoapsisLastUT > vesselState.time - 1)
            {
                //reduce throttle as apoapsis nears target
                double instantRatePerThrottle = (orbit.ApR - raiseApoapsisLastApR) / ((vesselState.time - raiseApoapsisLastUT) * raiseApoapsisLastThrottle);
                instantRatePerThrottle = Math.Max(1.0, instantRatePerThrottle); //avoid problems from negative rates
                raiseApoapsisRatePerThrottle.value = instantRatePerThrottle;
                double desiredApRate = (finalApR - currentApR) / 1.0;
                desiredThrottle = Mathf.Clamp((float)(desiredApRate / raiseApoapsisRatePerThrottle), 0.05F, 1.0F);
            }
            else
            {
                desiredThrottle = 1.0F; //no recent data point; just use max thrust.
            }

            //record data for next frame
            raiseApoapsisLastThrottle = desiredThrottle;
            raiseApoapsisLastApR = orbit.ApR;
            raiseApoapsisLastUT = vesselState.time;

            return desiredThrottle;
        }

        protected double srfvelPitch() {
            return 90.0 - Vector3d.Angle(vesselState.surfaceVelocity, vesselState.up);
        }

        protected double srfvelHeading() {
            return vesselState.HeadingFromDirection(vesselState.surfaceVelocity.ProjectOnPlane(vesselState.up));
        }

        // this provides ground track heading based on desired inclination and is what most consumers should call
        protected void attitudeTo(double desiredPitch)
        {
            double desiredHeading = OrbitalManeuverCalculator.HeadingForLaunchInclination(vessel, vesselState, autopilot.desiredInclination);
            attitudeTo(desiredPitch, desiredHeading);
        }

        // provides AoA limiting and roll control
        // provides no ground tracking and should only be called by autopilots like PVG that deeply know what they're doing with yaw control
        // (possibly should be moved into the attitude controller, but right now it collaborates too heavily with the ascent autopilot)
        //
        protected void attitudeTo(double desiredPitch, double desiredHeading)
        {
            /*
            Vector6 rcs = vesselState.rcsThrustAvailable;

            // FIXME?  should this be up/down and not forward/back?  seems wrong?  why was i using down before for the ullage direction?
            bool has_rcs = vessel.hasEnabledRCSModules() && vessel.ActionGroups[KSPActionGroup.RCS] && ( rcs.left > 0.01 ) && ( rcs.right > 0.01 ) && ( rcs.forward > 0.01 ) && ( rcs.back > 0.01 );

            if ( (vesselState.thrustCurrent / vesselState.thrustAvailable < 0.50) && !has_rcs )
            {
                // if engines are spooled up at less than 50% and we have no RCS in the stage, do not issue any guidance commands yet
                return;
            }
            */

            Vector3d desiredHeadingVector = Math.Sin(desiredHeading * UtilMath.Deg2Rad) * vesselState.east + Math.Cos(desiredHeading * UtilMath.Deg2Rad) * vesselState.north;

            Vector3d desiredThrustVector = Math.Cos(desiredPitch * UtilMath.Deg2Rad) * desiredHeadingVector
                + Math.Sin(desiredPitch * UtilMath.Deg2Rad) * vesselState.up;

            desiredThrustVector = desiredThrustVector.normalized;

            thrustVectorForNavball = desiredThrustVector;

            /* old style AoA limiter */
            if (autopilot.limitAoA && !autopilot.limitQaEnabled)
            {
                float fade = vesselState.dynamicPressure < autopilot.aoALimitFadeoutPressure ? (float)(autopilot.aoALimitFadeoutPressure / vesselState.dynamicPressure) : 1;
                autopilot.currentMaxAoA = Math.Min(fade * autopilot.maxAoA, 180d);
                autopilot.limitingAoA = vessel.altitude < mainBody.atmosphereDepth && Vector3.Angle(vesselState.surfaceVelocity, desiredThrustVector) > autopilot.currentMaxAoA;

                if (autopilot.limitingAoA)
                {
                    desiredThrustVector = Vector3.RotateTowards(vesselState.surfaceVelocity, desiredThrustVector, (float)(autopilot.currentMaxAoA * Mathf.Deg2Rad), 1).normalized;
                }
            }

            /* AoA limiter for PVG */
            if (autopilot.limitQaEnabled)
            {
                double lim = MuUtils.Clamp(autopilot.limitQa, 100, 10000);
                autopilot.limitingAoA = vesselState.dynamicPressure * Vector3.Angle(vesselState.surfaceVelocity, desiredThrustVector) * UtilMath.Deg2Rad > lim;
                if (autopilot.limitingAoA)
                {
                    autopilot.currentMaxAoA = lim / vesselState.dynamicPressure * UtilMath.Rad2Deg;
                    desiredThrustVector = Vector3.RotateTowards(vesselState.surfaceVelocity, desiredThrustVector, (float)(autopilot.currentMaxAoA * UtilMath.Deg2Rad), 1).normalized;
                }
            }

            double pitch = 90 - Vector3d.Angle(desiredThrustVector, vesselState.up);

            double hdg;

            if (pitch > 89.9) {
                hdg = desiredHeading;
            } else {
                hdg = MuUtils.ClampDegrees360(UtilMath.Rad2Deg * Math.Atan2(Vector3d.Dot(desiredThrustVector, vesselState.east), Vector3d.Dot(desiredThrustVector, vesselState.north)));
            }

            if (autopilot.forceRoll)
            {
                core.attitude.AxisControl(!vessel.Landed, !vessel.Landed, !vessel.Landed && (vesselState.altitudeBottom > 50));
                if ( desiredPitch == 90.0)
                {
                    core.attitude.attitudeTo(hdg, pitch, autopilot.verticalRoll, this, !vessel.Landed, !vessel.Landed, !vessel.Landed && (vesselState.altitudeBottom > 50));
                }
                else
                {
                    core.attitude.attitudeTo(hdg, pitch, autopilot.turnRoll, this, !vessel.Landed, !vessel.Landed, !vessel.Landed && (vesselState.altitudeBottom > 50));
                }
            }
            else
            {
                core.attitude.attitudeTo(desiredThrustVector, AttitudeReference.INERTIAL, this);
            }
        }

    }

    public static class LaunchTiming
    {
        //Computes the time until the phase angle between the launchpad and the target equals the given angle.
        //The convention used is that phase angle is the angle measured starting at the target and going east until
        //you get to the launchpad.
        //The time returned will not be exactly accurate unless the target is in an exactly circular orbit. However,
        //the time returned will go to exactly zero when the desired phase angle is reached.
        public static double TimeToPhaseAngle(double phaseAngle, CelestialBody launchBody, double launchLongitude, Orbit target)
        {
            double launchpadAngularRate = 360 / launchBody.rotationPeriod;
            double targetAngularRate = 360.0 / target.period;
            if (Vector3d.Dot(-target.GetOrbitNormal().Reorder(132).normalized, launchBody.angularVelocity) < 0) targetAngularRate *= -1; //retrograde target

            Vector3d currentLaunchpadDirection = launchBody.GetSurfaceNVector(0, launchLongitude);
            Vector3d currentTargetDirection = target.SwappedRelativePositionAtUT(Planetarium.GetUniversalTime());
            currentTargetDirection = Vector3d.Exclude(launchBody.angularVelocity, currentTargetDirection);

            double currentPhaseAngle = Math.Abs(Vector3d.Angle(currentLaunchpadDirection, currentTargetDirection));
            if (Vector3d.Dot(Vector3d.Cross(currentTargetDirection, currentLaunchpadDirection), launchBody.angularVelocity) < 0)
            {
                currentPhaseAngle = 360 - currentPhaseAngle;
            }

            double phaseAngleRate = launchpadAngularRate - targetAngularRate;

            double phaseAngleDifference = MuUtils.ClampDegrees360(phaseAngle - currentPhaseAngle);

            if (phaseAngleRate < 0)
            {
                phaseAngleRate *= -1;
                phaseAngleDifference = 360 - phaseAngleDifference;
            }


            return phaseAngleDifference / phaseAngleRate;
        }
    }
}
