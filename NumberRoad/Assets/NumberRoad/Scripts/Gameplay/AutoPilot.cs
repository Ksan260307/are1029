using NumberRoad.Core;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// Simple bot that plays like a sensible player: dodges enemy shots, takes the better gate, chases gold
    /// goblins, runs over "+n" strips, lines up with crates and fires VOLLEY when it is ready. Used by the
    /// play-mode tests and handy for checking new levels hands-free (toggle with F1 in the editor).
    /// </summary>
    public static class AutoPilot
    {
        const float LookAhead = 26f;

        public static float SteerTowards(LevelSession session, float dt)
        {
            if (session.SkillReady) session.UseSkill();
            Squad squad = session.Squad;
            float target = ChooseX(session);
            float delta = target - squad.TargetX;
            return Mathf.Clamp(delta, -8f * dt, 8f * dt);
        }

        public static float ChooseX(LevelSession session)
        {
            Squad squad = session.Squad;

            if (session.ShotThreat(out float landX))
            {
                float away = landX > squad.X ? landX - 3f : landX + 3f;
                return Mathf.Clamp(away, -Balance.SquadMaxX, Balance.SquadMaxX);
            }

            GateRow row = session.NextGateRow();
            if (row != null && row.Z - squad.Z < LookAhead)
            {
                // shoot a locked prize open while there is time, like a player would
                GateView locked = LockedGate(row);
                if (locked != null && row.Z - squad.Z > 7f) return locked.CenterX;
                return BestGate(row, squad).CenterX;
            }

            int gold = session.Swarm.Find(EnemyTrait.Runaway, squad.Z);
            if (gold >= 0 && session.Swarm.Agents[gold].Pos.z - squad.Z < 34f) return session.Swarm.Agents[gold].Pos.x;

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

        static GateView LockedGate(GateRow row)
        {
            foreach (GateView g in row.Gates)
            {
                if (g.Locked) return g;
            }
            return null;
        }

        static GateView BestGate(GateRow row, Squad squad)
        {
            GateView best = row.Gates[0];
            if (row.Shootable)
            {
                foreach (GateView g in row.Gates)
                {
                    if (g.MaxValue > best.MaxValue) best = g;
                }
                return best;
            }

            var model = new PowerModel(squad.Army.Clone(), squad.Weapon);
            float stay = model.ScoreAfter(GateEffect.Add(0));
            float bestScore = float.MinValue;
            foreach (GateView g in row.Gates)
            {
                // the bot cannot see behind a "?" and gains nothing from a gate that is still locked
                float score = g.Locked || g.Mystery ? stay * (g.Mystery ? 1.05f : 1f) : model.ScoreAfter(g.Effect);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = g;
                }
            }
            return best;
        }
    }
}
