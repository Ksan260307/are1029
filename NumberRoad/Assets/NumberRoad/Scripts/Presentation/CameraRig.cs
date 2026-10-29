using UnityEngine;

namespace NumberRoad.Presentation
{
    /// <summary>
    /// Third-person camera behind the squad. The distance adapts to the screen
    /// aspect so the whole road width is visible on tall phones as well as on
    /// landscape iPads, and it carries a small trauma-based screen shake.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public const float Fov = 56f;
        public const float Pitch = 46f;
        const float MinDistance = 14.5f;
        const float VisibleHalfWidth = 4.0f;
        const float PlayLead = 5.5f;
        const float TitleLead = 0.5f;

        public Camera Camera { get; private set; }

        Vector3 _focus;
        float _lead = PlayLead;
        float _leadTarget = PlayLead;
        float _distance;
        float _trauma;
        float _extraDistance;
        float _extraTarget;
        float _seed;

        public void Init(Color background)
        {
            Camera = gameObject.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = background;
            Camera.fieldOfView = Fov;
            Camera.nearClipPlane = 0.5f;
            Camera.farClipPlane = 180f;
            Camera.allowHDR = false;
            Camera.allowMSAA = true;
            gameObject.AddComponent<AudioListener>();
            gameObject.tag = "MainCamera";
            _seed = Random.value * 100f;
        }

        /// <summary>Distance needed for the road to fit horizontally at the focus point.</summary>
        public static float DistanceForAspect(float aspect)
        {
            float tanHalfV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float needed = VisibleHalfWidth / (tanHalfV * Mathf.Max(0.1f, aspect)) + 3f;
            return Mathf.Max(MinDistance, needed);
        }

        public void SetBossView(bool boss) => _extraTarget = boss ? 3.5f : 0f;

        /// <summary>Title screen centres the squad (the menu covers the bottom); play looks further ahead.</summary>
        public void SetTitleView(bool title) => _leadTarget = title ? TitleLead : PlayLead;

        public void Shake(float amount) => _trauma = Mathf.Min(1f, _trauma + amount);

        public void Snap(Vector3 squadPosition)
        {
            _lead = _leadTarget;
            _focus = FocusFor(squadPosition);
            _distance = DistanceForAspect(Camera.aspect);
            _extraDistance = _extraTarget;
            Place(0f);
        }

        public void Tick(Vector3 squadPosition, float dt)
        {
            _lead = Mathf.Lerp(_lead, _leadTarget, 1f - Mathf.Exp(-2.5f * dt));
            Vector3 target = FocusFor(squadPosition);
            float k = 1f - Mathf.Exp(-6f * dt);
            _focus = Vector3.Lerp(_focus, target, k);
            _distance = Mathf.Lerp(_distance, DistanceForAspect(Camera.aspect), k);
            _extraDistance = Mathf.Lerp(_extraDistance, _extraTarget, 1f - Mathf.Exp(-2f * dt));
            _trauma = Mathf.Max(0f, _trauma - dt * 1.6f);
            Place(Time.unscaledTime);
        }

        Vector3 FocusFor(Vector3 squad) => new Vector3(squad.x * 0.22f, 0f, squad.z + _lead);

        void Place(float time)
        {
            Quaternion rotation = Quaternion.Euler(Pitch, 0f, 0f);
            float distance = _distance + _extraDistance;
            Vector3 position = _focus - rotation * Vector3.forward * distance;
            float shake = _trauma * _trauma;
            if (shake > 0f)
            {
                position += new Vector3(
                    (Mathf.PerlinNoise(_seed, time * 25f) - 0.5f) * 0.9f,
                    (Mathf.PerlinNoise(_seed + 7f, time * 25f) - 0.5f) * 0.9f,
                    0f) * shake;
                rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(_seed + 13f, time * 20f) - 0.5f) * 6f * shake);
            }
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
