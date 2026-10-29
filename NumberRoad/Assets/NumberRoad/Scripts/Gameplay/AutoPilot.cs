using NumberRoad.Core;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// Simple bot that steers like a sensible player: towards the better gate,
    /// onto "+n" strips and in front of crates. Used by the play-mode smoke test
    /// and handy for checking new levels hands-free (toggle with F1 in the editor).
    /// </summary>
    public static class AutoPilot
    {
        const float LookAhead = 26f;

        public static float SteerTowards(LevelSession session, float dt)
        {
            Squad squad = session.Squad;
            float target = ChooseX(session);
            float delta = target - squad.TargetX;
            return Mathf.Clamp(delta, -8f * dt, 8f * dt);
        }

        public static float ChooseX(LevelSession session)
        {
            Squad squad = session.Squad;
            var model = new PowerModel(squad.Count, squad.Weapon);

            GatePair gate = session.NextGate();
            if (gate != null && gate.Z - squad.Z < LookAhead)
            {
                if (gate.Left.Shootable)
                {
                    GateView best = gate.Left.MaxValue >= gate.Right.MaxValue ? gate.Left : gate.Right;
                    return best.CenterX;
                }
                return model.DpsAfter(gate.Left.Effect) >= model.DpsAfter(gate.Right.Effect) ? gate.Left.CenterX : gate.Right.CenterX;
            }

            StripTile strip = session.NextStrip();
            if (strip != null && strip.Z - squad.Z < LookAhead) return strip.X;

            Crate crate = session.NextCrate();
            if (crate != null && crate.Position.z - squad.Z < LookAhead)
            {
                // line up to shoot it, but step aside before running into it
                if (crate.Position.z - squad.Z > squad.HalfDepth + 2.5f) return crate.Position.x;
                return crate.Position.x > 0f ? crate.Position.x - 2.8f : crate.Position.x + 2.8f;
            }

            return 0f;
        }
    }
}
