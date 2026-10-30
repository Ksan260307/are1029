using NumberRoad.Core;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// Simple bot that plays like a sensible player: dodges enemy shots and burning ground, takes the better
    /// gate, blocks lanes of enemies that would ram the war wagon, chases gold goblins, runs over "+n" strips, lines
    /// up with crates and picks the strongest buff card between waves. Used by the play-mode tests and handy for checking new
    /// levels hands-free (toggle with F1 in the editor).
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

            // a lane of enemies about to slip past: stand in its way when they would wreck the wagon, or when the
            // army easily outnumbers them; otherwise let them through and keep the soldiers
            float threat = session.BreachThreat(out float blockX, out int lane);
            bool wrecks = threat >= session.BaseHealth - 0.1f;
            bool easy = squad.Count >= lane * 3;
            if (threat > 0f && (wrecks || easy)) return Mathf.Clamp(blockX, -Balance.SquadMaxX, Balance.SquadMaxX);

            if (session.HazardAhead(out float hazardX))
            {
                // step around the flames on the side with more room
                float around = hazardX > 0f ? hazardX - 3.2f : hazardX + 3.2f;
                return Mathf.Clamp(around, -Balance.SquadMaxX, Balance.SquadMaxX);
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

        /// <summary>The card a sensible player would take: the biggest boost to the army's strength right now.</summary>
        public static int PickBuff(LevelSession session)
        {
            Buff[] options = session.Options;
            int best = 0;
            float bestValue = float.MinValue;
            for (int i = 0; i < options.Length; i++)
            {
                float v = Value(options[i], session);
                if (v > bestValue)
                {
                    bestValue = v;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Rough strength gain of a card (1 = no gain).</summary>
        public static float Value(Buff buff, LevelSession session)
        {
            Squad squad = session.Squad;
            float fallen = squad.Count > 0 ? squad.Fallen / (float)squad.Count : 0f;
            int wavesLeft = Mathf.Max(0, session.Waves - session.Wave - 1);
            switch (buff.Id)
            {
                case "sharp": return 1.2f;
                case "quick": return 1.15f;
                case "eagle": return 1.09f;
                case "deadly": return 1.06f;
                case "long": return 1.05f;
                case "extra": return 1.3f;
                case "heavy_hitter": return 1.35f;
                case "rapid": return 1.3f;
                case "drill": return 1.1f;
                case "combo": return 1.02f;
                case "wide": return 1.12f;
                case "training": return 1.21f;
                case "flame": return 1.25f;
                case "frost": return 1.12f;
                case "thunder": return 1.18f;
                case "venom": return 1.15f;
                case "blast": return 1.25f;
                case "pierce": return 1.15f;
                case "bounce": return 1.12f;
                case "seeker": return 1.08f;
                case "holy_rounds": return 1.06f;
                case "impact": return 1.06f;
                case "steal": return 1.1f;
                case "shards": return 1.14f;
                case "glass": return squad.Count >= 20 ? 1.45f : 1f;
                case "berserk": return 1.25f;
                case "heavy_ammo": return 1.35f;
                case "greed": return 0.95f;
                case "overdrive": return 1.4f * Mathf.Pow(0.95f, wavesLeft + 1);
                case "gamble": return 1.1f;
                case "bloodlust": return 1.2f;
                case "reckless": return 1.3f;
                case "medic": return 1f + fallen * 0.5f;
                case "regen": return 1.08f + Mathf.Min(0.3f, fallen);
                case "second": return 1.1f;
                case "ward": return 1.12f;
                case "repair": return 1f + (1f - session.BaseHealth) * 0.8f;
                case "plating": return session.BaseHealth < 0.7f ? 1.15f : 1.05f;
                case "conscription": return 1.3f;
                case "mercs": return 1.5f;
                case "promotion": return 1.45f;
                case "double": return 2f;
                case "golden": return 1f;
                case "blessing": return 1.12f;
                case "allin": return 1.8f;
                case "overflow": return 1.2f;
                case "slowtime": return 1.08f;
                case "veteran": return 1.2f;
                default: return buff.Kind == BuffKind.Troops ? 1.25f : 1.1f;
            }
        }

        static float Average(PowerModel model, GateEffect[] faces)
        {
            float sum = 0f;
            foreach (GateEffect e in faces) sum += model.ScoreAfter(e);
            return sum / faces.Length;
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
                // the bot cannot see behind a "?", gains nothing from a gate that is still locked and cannot time a
                // roulette, so it counts on the roulette's average
                float score = g.Locked || g.Mystery ? stay * (g.Mystery ? 1.05f : 1f)
                    : g.Spec.roulette != null ? Average(model, g.Spec.roulette)
                    : model.ScoreAfter(g.Effect);
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
