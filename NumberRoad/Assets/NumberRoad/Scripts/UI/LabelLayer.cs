using System;
using System.Collections.Generic;
using NumberRoad.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRoad.UI
{
    public enum LabelStyle
    {
        SquadBadge,
        EnemyHp,
        BossHp,
        Gate,
        GateWord,
        GateSmall,
        Crate,
        Reward,
        Popup,
    }

    /// <summary>
    /// World-anchored text (gate numbers, squad count, HP, popups) drawn on one
    /// screen-space canvas. Labels are projected every frame and sized by real
    /// perspective (metres of text height), with a minimum so far ones stay readable.
    /// </summary>
    public sealed class LabelLayer
    {
        public sealed class Label
        {
            internal RectTransform Rect;
            internal CanvasGroup Group;
            internal Text Text;
            internal Image Badge;
            internal LabelStyle Style;
            internal float WorldHeight;
            internal float MinHeight;
            internal int FontSize;
            internal bool InUse;
            internal float Punch;
            string _value;

            public Vector3 World;
            public float Alpha = 1f;
            public float Scale = 1f;

            public void SetText(string value)
            {
                if (_value == value) return;
                _value = value;
                Text.text = value;
                if (Badge != null)
                {
                    Badge.rectTransform.sizeDelta = new Vector2(Mathf.Max(FontSize * 1.6f, Text.preferredWidth + FontSize * 0.9f), FontSize * 1.35f);
                }
            }

            public void SetColor(Color color) => Text.color = color;

            public void PunchScale(float amount) => Punch = Mathf.Max(Punch, amount);
        }

        readonly struct Spec
        {
            public readonly int FontSize;
            public readonly float WorldHeight;
            public readonly float MinHeight;
            public readonly Color TextColor;
            public readonly Color? BadgeColor;

            public Spec(int fontSize, float worldHeight, float minHeight, Color textColor, Color? badgeColor = null)
            {
                FontSize = fontSize;
                WorldHeight = worldHeight;
                MinHeight = minHeight;
                TextColor = textColor;
                BadgeColor = badgeColor;
            }
        }

        sealed class PopupState
        {
            public Label Label;
            public Vector3 Origin;
            public float Clock;
            public float Duration;
            public float BaseScale;
        }

        static Spec SpecFor(LabelStyle style)
        {
            switch (style)
            {
                case LabelStyle.SquadBadge: return new Spec(54, 0.5f, 44f, Color.white, UIFactory.Blue);
                case LabelStyle.EnemyHp: return new Spec(52, 0.55f, 36f, Color.white);
                case LabelStyle.BossHp: return new Spec(66, 0.85f, 50f, Color.white, UIFactory.Red);
                case LabelStyle.Gate: return new Spec(120, 1.05f, 40f, Color.white);
                case LabelStyle.GateWord: return new Spec(84, 0.6f, 28f, Color.white);
                case LabelStyle.GateSmall: return new Spec(64, 0.5f, 26f, Color.white);
                case LabelStyle.Crate: return new Spec(64, 0.62f, 32f, Color.white);
                case LabelStyle.Reward: return new Spec(44, 0.36f, 24f, UIFactory.Gold);
                default: return new Spec(64, 0.55f, 40f, Color.white);
            }
        }

        /// <summary>Hides everything except the squad badge (title screen: keeps the logo area clean).</summary>
        public bool OnlySquadBadge;

        readonly RectTransform _root;
        readonly Func<Camera> _camera;
        readonly List<Label> _active = new List<Label>();
        readonly Dictionary<LabelStyle, Stack<Label>> _pool = new Dictionary<LabelStyle, Stack<Label>>();
        readonly List<PopupState> _popups = new List<PopupState>();

        public LabelLayer(RectTransform root, Func<Camera> camera)
        {
            _root = root;
            _camera = camera;
        }

        public Label Create(LabelStyle style, string text = "")
        {
            Label label = null;
            if (_pool.TryGetValue(style, out Stack<Label> stack) && stack.Count > 0) label = stack.Pop();
            if (label == null) label = Build(style);
            label.InUse = true;
            label.Alpha = 1f;
            label.Scale = 1f;
            label.Punch = 0f;
            label.World = new Vector3(0f, -1000f, 0f);
            label.Text.color = SpecFor(style).TextColor;
            label.Group.alpha = 0f;
            label.Rect.gameObject.SetActive(true);
            label.SetText(text);
            _active.Add(label);
            return label;
        }

        public void Release(Label label)
        {
            if (label == null || !label.InUse) return;
            label.InUse = false;
            label.Rect.gameObject.SetActive(false);
            _active.Remove(label);
            if (!_pool.TryGetValue(label.Style, out Stack<Label> stack))
            {
                stack = new Stack<Label>();
                _pool[label.Style] = stack;
            }
            stack.Push(label);
        }

        public void Popup(string text, Vector3 world, Color color, float scale = 1f, float duration = 0.9f)
        {
            if (_popups.Count >= 40)
            {
                Release(_popups[0].Label);
                _popups.RemoveAt(0);
            }
            Label label = Create(LabelStyle.Popup, text);
            label.SetColor(color);
            label.World = world;
            _popups.Add(new PopupState { Label = label, Origin = world, Duration = duration, BaseScale = scale });
        }

        public void Clear()
        {
            foreach (PopupState p in _popups) Release(p.Label);
            _popups.Clear();
            for (int i = _active.Count - 1; i >= 0; i--) Release(_active[i]);
        }

        Label Build(LabelStyle style)
        {
            Spec spec = SpecFor(style);
            RectTransform rt = UIFactory.Rect(_root, "Label_" + style);
            UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(spec.FontSize * 4f, spec.FontSize * 1.4f));
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var label = new Label
            {
                Rect = rt,
                Group = group,
                Style = style,
                FontSize = spec.FontSize,
                WorldHeight = spec.WorldHeight,
                MinHeight = spec.MinHeight,
            };

            if (spec.BadgeColor.HasValue)
            {
                label.Badge = UIFactory.Image(rt, "Badge", spec.BadgeColor.Value, GameAssets.RoundedSprite);
                UIFactory.Place(label.Badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(spec.FontSize * 1.6f, spec.FontSize * 1.35f));
                var outline = label.Badge.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
                outline.effectDistance = new Vector2(4f, -4f);
            }

            float outlineDistance = Mathf.Max(3f, spec.FontSize * 0.05f);
            label.Text = UIFactory.Text(rt, "Text", string.Empty, spec.FontSize, spec.TextColor, TextAnchor.MiddleCenter,
                UIFactory.OutlineDark, outlineDistance);
            UIFactory.Fill(label.Text.rectTransform);
            return label;
        }

        public void LateTick(float dt)
        {
            TickPopups(dt);
            Camera cam = _camera();
            if (cam == null) return;
            Rect rect = _root.rect;
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

            for (int i = 0; i < _active.Count; i++)
            {
                Label l = _active[i];
                l.Punch = Mathf.Max(0f, l.Punch - dt * 3f);
                Vector3 vp = cam.WorldToViewportPoint(l.World);
                bool hidden = OnlySquadBadge && l.Style != LabelStyle.SquadBadge;
                if (vp.z <= 0.5f || l.Alpha <= 0.001f || hidden)
                {
                    l.Group.alpha = 0f;
                    continue;
                }
                float unitsPerMeter = rect.height / (2f * vp.z * tanHalf);
                float height = Mathf.Max(l.WorldHeight * unitsPerMeter, l.MinHeight);
                float scale = height / l.FontSize * l.Scale * (1f + l.Punch);
                l.Rect.localScale = new Vector3(scale, scale, 1f);
                l.Rect.anchoredPosition = new Vector2((vp.x - 0.5f) * rect.width, (vp.y - 0.5f) * rect.height);
                float fog = 1f - Mathf.Clamp01((vp.z - 60f) / 25f);
                l.Group.alpha = l.Alpha * fog;
            }
        }

        void TickPopups(float dt)
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                PopupState p = _popups[i];
                p.Clock += dt;
                float t = p.Clock / p.Duration;
                if (t >= 1f || !p.Label.InUse)
                {
                    Release(p.Label);
                    _popups.RemoveAt(i);
                    continue;
                }
                float rise = 1f - (1f - t) * (1f - t);
                p.Label.World = p.Origin + Vector3.up * (1.3f * rise);
                p.Label.Alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                float pop = t < 0.15f ? Mathf.Lerp(0.4f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.15f) / 0.15f));
                p.Label.Scale = p.BaseScale * pop;
            }
        }
    }
}
