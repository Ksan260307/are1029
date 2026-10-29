using NumberRoad.Core;
using UnityEngine;

namespace NumberRoad.Presentation
{
    /// <summary>Ambient particles around the camera for each region: snow, embers, fireflies, petals, sparkles.</summary>
    public sealed class WeatherFx
    {
        const int Count = 140;
        static readonly Vector3 Box = new Vector3(22f, 12f, 46f);

        readonly Vector3[] _pos = new Vector3[Count];
        readonly float[] _seed = new float[Count];
        readonly InstancedBatch _batch = new InstancedBatch();
        Weather _weather = Weather.None;
        float _time;

        public float Density = 1f;

        public void Set(Weather weather)
        {
            _weather = weather;
            for (int i = 0; i < Count; i++)
            {
                _pos[i] = new Vector3(Random.Range(-Box.x, Box.x) * 0.5f, Random.Range(0f, Box.y), Random.Range(-Box.z, Box.z) * 0.5f);
                _seed[i] = Random.value * 100f;
            }
        }

        public void Tick(float dt, Vector3 focus)
        {
            if (_weather == Weather.None) return;
            _time += dt;
            for (int i = 0; i < Count; i++)
            {
                Vector3 p = _pos[i];
                float s = _seed[i];
                switch (_weather)
                {
                    case Weather.Snow:
                        p += new Vector3(Mathf.Sin(_time * 0.8f + s) * 0.4f, -1.4f, 0f) * dt;
                        break;
                    case Weather.Embers:
                        p += new Vector3(Mathf.Sin(_time * 1.7f + s) * 0.6f, 1.6f, 0.3f) * dt;
                        break;
                    case Weather.Fireflies:
                        p += new Vector3(Mathf.Sin(_time * 0.9f + s), Mathf.Sin(_time * 1.3f + s * 2f) * 0.6f, Mathf.Cos(_time * 0.7f + s)) * (0.8f * dt);
                        break;
                    case Weather.Petals:
                        p += new Vector3(1.1f + Mathf.Sin(_time + s) * 0.5f, -0.8f, Mathf.Cos(_time * 0.6f + s) * 0.4f) * dt;
                        break;
                    default:
                        p += new Vector3(0f, Mathf.Sin(_time * 0.5f + s) * 0.2f, 0f) * dt;
                        break;
                }
                // wrap inside a box that travels with the squad
                Vector3 rel = p - new Vector3(0f, 0f, focus.z + 10f);
                rel.x = Wrap(rel.x, Box.x);
                rel.y = Wrap(rel.y - Box.y * 0.5f, Box.y) + Box.y * 0.5f;
                rel.z = Wrap(rel.z, Box.z);
                _pos[i] = rel + new Vector3(0f, 0f, focus.z + 10f);
            }
        }

        static float Wrap(float v, float size)
        {
            float half = size * 0.5f;
            return Mathf.Repeat(v + half, size) - half;
        }

        public void Render()
        {
            if (_weather == Weather.None || Density <= 0f) return;
            bool glow = _weather == Weather.Embers || _weather == Weather.Fireflies || _weather == Weather.Sparkles;
            _batch.Begin(_weather == Weather.Petals || _weather == Weather.Snow ? GameAssets.Cube : GameAssets.Orb, GameAssets.Transparent);
            int n = Mathf.RoundToInt(Count * Density);
            for (int i = 0; i < n; i++)
            {
                float s = _seed[i];
                float twinkle = glow ? 0.55f + 0.45f * Mathf.Sin(_time * 3f + s * 5f) : 1f;
                Color c;
                float size;
                switch (_weather)
                {
                    case Weather.Snow: c = new Color(1f, 1f, 1f, 0.9f); size = 0.09f; break;
                    case Weather.Embers: c = new Color(1f, 0.55f, 0.15f, twinkle); size = 0.08f; break;
                    case Weather.Fireflies: c = new Color(0.8f, 1f, 0.4f, twinkle); size = 0.1f; break;
                    case Weather.Petals: c = new Color(1f, 0.75f, 0.85f, 0.95f); size = 0.12f; break;
                    default: c = new Color(1f, 0.95f, 0.7f, twinkle * 0.9f); size = 0.07f; break;
                }
                Quaternion rot = _weather == Weather.Petals || _weather == Weather.Snow ? Quaternion.Euler(_time * 90f + s * 50f, s * 40f, _time * 60f) : Quaternion.identity;
                Vector3 scale = _weather == Weather.Petals ? new Vector3(size, size * 0.15f, size * 0.7f) : Vector3.one * size;
                _batch.Add(Matrix4x4.TRS(_pos[i], rot, scale), c);
            }
            _batch.End();
        }
    }
}
