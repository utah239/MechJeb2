using System;
using KSP.UI.Screens;
using UnityEngine;
using System.Collections.Generic;

/*
 * Optimized launches for RSS/RO
 */

namespace MuMech
{
    public class MechJebModuleAscentPVG : MechJebModuleAscentBase
    {
        public MechJebModuleAscentPVG(MechJebCore core) : base(core) { }

        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDouble pitchStartVelocity = new EditableDouble(50);
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public EditableDouble pitchRate = new EditableDouble(0.50);
        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        public bool omitCoast = false;

        private MechJebModuleAscentGuidance ascentGuidance { get { return core.GetComputerModule<MechJebModuleAscentGuidance>(); } }

        public override void OnModuleEnabled()
        {
            base.OnModuleEnabled();
            mode = AscentMode.VERTICAL_ASCENT;
            core.guidance.enabled = true;
        }

        public override void OnModuleDisabled()
        {
            base.OnModuleDisabled();
            core.guidance.enabled = false;
        }

        private enum AscentMode { VERTICAL_ASCENT, INITIATE_TURN, GUIDANCE, EXIT };
        private AscentMode mode;

        public override void timedLaunchHook()
        {
            // timedLaunch kills the optimizer so re-enable it here
            core.guidance.enabled = true;
        }

        public override bool DriveAscent(FlightCtrlState s)
        {
            setTarget();
            core.guidance.AssertStart(allow_execution: true);
            switch (mode)
            {
                case AscentMode.VERTICAL_ASCENT:
                    DriveVerticalAscent(s);
                    break;

                case AscentMode.INITIATE_TURN:
                    DriveInitiateTurn(s);
                    break;

                case AscentMode.GUIDANCE:
                    DriveGuidance(s);
                    break;
            }

            return (mode != AscentMode.EXIT);
        }

        // convert PeA/ApA values to SMA+ECC
        private void ConvertToSMAEcc(double PeA, double ApA, out double sma, out double ecc)
        {
            double PeR = mainBody.Radius + PeA;
            double ApR = mainBody.Radius + ApA;

            /* remap nonsense ApAs onto circular orbits */
            if ( ApA >= 0 && ApA < PeA )
                ApR = PeR;

            sma = (PeR + ApR) / 2;

            ecc = (ApR - PeR) / (ApR + PeR);
        }

        private void ConvertToVTRT(double sma, double ecc, double gamma, out double rt, out double vt)
        {
            // All this math comes from symbolic algebraic equation solving in Matlab:
            // f1 = h == cos(gamma) * r * v
            // f2 = v^2 == -mu*(1/a - 2/r)
            // f3 = h == sqrt(mu * a * ( 1 - e^ 2 ))
            // S = solve(f1, f2, f3)
            // and we pick the higher velocity root closer to the periapsis
            double h = Math.Sqrt( mainBody.gravParameter * sma * ( 1 - ecc * ecc ) );
            double cg = Math.Cos( gamma * UtilMath.Deg2Rad );
            double d1 = sma * ( ecc - 1 ) * ( ecc + 1 );
            double d2 = d1 * h * cg;
            double n1 = h * Math.Sqrt( ecc*ecc + cg*cg - 1 ) + h * cg; // FIXME: should check sqrt for imaginary numbers
            vt = - n1 / d1;
            rt = 2 * sma + n1 * sma * sma * ( 1 -  ecc * ecc) / d2;
        }

        private void setTarget()
        {
            double sma = 0;
            double ecc = 0;
            double vt = 0;
            double rt = 0;

            if ( autopilot.pvgTargetIdxPublic == pvgTargetType.KEPLER_HUMAN || autopilot.pvgTargetIdxPublic == pvgTargetType.FLIGHTANGLE_HUMAN )
            {
                if ( autopilot.desiredShapeMode == 1 && core.target.NormalTargetExists )
                    ConvertToSMAEcc(core.target.TargetOrbit.PeA, core.target.TargetOrbit.ApA, out sma, out ecc);
                else
                    ConvertToSMAEcc(autopilot.desiredPeriapsis, autopilot.desiredApoapsis, out sma, out ecc);
            }

            if ( autopilot.pvgTargetIdxPublic == pvgTargetType.KEPLER || autopilot.pvgTargetIdxPublic == pvgTargetType.FLIGHTANGLE )
            {
                if ( autopilot.desiredShapeMode == 1 && core.target.NormalTargetExists )
                {
                    sma = core.target.TargetOrbit.semiMajorAxis;
                    ecc = core.target.TargetOrbit.eccentricity;
                }
                else
                {
                    sma = autopilot.desiredSMA;
                    ecc = autopilot.desiredECC;
                }
            }

            if ( autopilot.pvgTargetIdxPublic == pvgTargetType.FLIGHTANGLE || autopilot.pvgTargetIdxPublic == pvgTargetType.FLIGHTANGLE_HUMAN )
            {
                ConvertToVTRT(sma, ecc, autopilot.desiredFPA, out rt, out vt);
            }

            double inclination = autopilot.desiredInclination;

            if ( autopilot.desiredIncMode == 1 && core.target.NormalTargetExists )
                inclination = core.target.TargetOrbit.inclination;

            if ( autopilot.desiredIncMode == 2 )
                inclination = vessel.orbit.inclination;

            double LAN = autopilot.desiredLAN;

            if ( autopilot.desiredLANMode == 1 && core.target.NormalTargetExists )
                LAN = core.target.TargetOrbit.LAN;

            double ArgP = autopilot.desiredArgP;

            if ( autopilot.desiredArgPMode == 1 && core.target.NormalTargetExists )
                ArgP = core.target.TargetOrbit.argumentOfPeriapsis;

            if ( autopilot.pvgTargetIdxPublic == pvgTargetType.KEPLER_HUMAN || autopilot.pvgTargetIdxPublic == pvgTargetType.KEPLER )
            {
                if ( autopilot.desiredLANMode == 2 ) // free
                {
                    if ( autopilot.desiredArgPMode == 2 ) // free
                    {
                        // 3 constraint -- LAN, ArgP, TA free
                        core.guidance.keplerian3constraint(sma, ecc, inclination, omitCoast, autopilot.desiredIncMode == 2);
                        // FIXME: push LAN + ArgP into autopilot
                    }
                    else
                    {
                        // 4 constraint -- LAN, TA free
                        core.guidance.keplerian4constraintLANfree(sma, ecc, inclination, ArgP, omitCoast, autopilot.desiredIncMode == 2);
                        // FIXME: push LAN into autopilot
                    }
                }
                else
                {
                    if ( autopilot.desiredArgPMode == 2 ) // free
                    {
                        // 4 constraint -- ArgP, TA free
                        core.guidance.keplerian4constraintArgPfree(sma, ecc, inclination, LAN, omitCoast, autopilot.desiredIncMode == 2);
                        // FIXME: push ArgP into autopilot
                    }
                    else
                    {
                        // 5 constraint -- TA free
                        core.guidance.keplerian5constraint(sma, ecc, inclination, LAN, ArgP, omitCoast, autopilot.desiredIncMode == 2);
                    }
                }
            }
            if ( autopilot.pvgTargetIdxPublic == pvgTargetType.FLIGHTANGLE_HUMAN || autopilot.pvgTargetIdxPublic == pvgTargetType.FLIGHTANGLE )
            {
                if ( autopilot.desiredLANMode == 2 ) // free
                {
                    // 4 constraint -- LAN and ArgP free
                    core.guidance.flightangle4constraint(rt, vt, inclination, autopilot.desiredFPA, sma, omitCoast, autopilot.desiredIncMode == 2);
                    // FIXME: push LAN into autopilot
                }
                else
                {
                    // 5 constraint -- ArgP free
                    core.guidance.flightangle5constraint(rt, vt, inclination, autopilot.desiredFPA, LAN, sma, omitCoast, autopilot.desiredIncMode == 2);
                }
            }
            if ( autopilot.pvgTargetIdxPublic == pvgTargetType.MAXNRG )
            {
                double rTm = autopilot.desiredAltitude + mainBody.Radius;
                sma = Math.Sqrt(mainBody.gravParameter / rTm);
                if ( autopilot.desiredLANMode == 2 ) // free
                {
                    core.guidance.flightangle3constraintMAXE(rTm, autopilot.desiredFPA, inclination, autopilot.maxStages, sma, omitCoast, autopilot.desiredIncMode == 2);
                }
                else
                {
                    core.guidance.flightangle4constraintMAXE(rTm, autopilot.desiredFPA, inclination, LAN, autopilot.maxStages, sma, omitCoast, autopilot.desiredIncMode == 2);
                }
            }
        }

        private double pitchStartTime;

        private void DriveVerticalAscent(FlightCtrlState s)
        {

            //during the vertical ascent we just thrust straight up at max throttle
            attitudeTo(90, core.guidance.heading);

            core.attitude.AxisControl(!vessel.Landed, !vessel.Landed, !vessel.Landed && (vesselState.altitudeBottom > 50));

            if (!vessel.LiftedOff() || vessel.Landed) {
                status = "Awaiting liftoff";
            }
            else
            {
                if (vesselState.surfaceVelocity.magnitude > pitchStartVelocity)
                {
                    mode = AscentMode.INITIATE_TURN;
                    pitchStartTime = autopilot.MET;
                    return;
                }
                double dv = pitchStartVelocity - vesselState.surfaceVelocity.magnitude;
                status = String.Format("Vertical ascent {0:F2} m/s to go", dv);
            }
        }

        private void DriveInitiateTurn(FlightCtrlState s)
        {
            double dt = autopilot.MET - pitchStartTime;
            double theta = dt * pitchRate;
            double pitch_program = 90 - theta;
            double pitch;

            if ( !mainBody.atmosphere )
            {
                mode = AscentMode.GUIDANCE;
                return;
            }

            if ( pitch_program > srfvelPitch() )
            {
                pitch = srfvelPitch();
                status = String.Format("Gravity Turn {0:F}° to guidance", pitch - core.guidance.pitch);
            }
            else
            {
                pitch = pitch_program;
                status = String.Format("Pitch program {0:F}° to guidance", pitch - core.guidance.pitch);
            }

            if ( pitch <= core.guidance.pitch && core.guidance.isStable() )
            {
                mode = AscentMode.GUIDANCE;
                return;
            }

            if ( (vesselState.maxDynamicPressure > 0) && (vesselState.maxDynamicPressure * 0.90 > vesselState.dynamicPressure) )
            {
                mode = AscentMode.GUIDANCE;
                return;
            }

            if (mainBody.atmosphere && vesselState.maxDynamicPressure > 0)
            {
                // from 95% to 90% of dynamic pressure apply a "fade" from the pitch selected above to the guidance pitch
                double fade = MuUtils.Clamp( (0.95 - vesselState.dynamicPressure / vesselState.maxDynamicPressure) * 20.0, 0.0, 1.0);
                pitch = fade * core.guidance.pitch + ( 1.0 - fade ) * pitch;
            }

            attitudeTo(pitch, core.guidance.heading);
        }

        private void DriveGuidance(FlightCtrlState s)
        {
            if ( core.guidance.status == PVGStatus.FINISHED )
            {
                mode = AscentMode.EXIT;
                return;
            }

            if ( !core.guidance.isStable() )
            {
                double pitch = Math.Min(Math.Min(90, srfvelPitch()), vesselState.vesselPitch);
                attitudeTo(pitch, srfvelHeading());
                status = "WARNING: Unstable Guidance";
            }
            else
            {

                status = "Stable Guidance";
                attitudeTo(core.guidance.pitch, core.guidance.heading);
            }
        }
    }
}
