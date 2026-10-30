using System;
using NumberRoad.Core;
using NumberRoad.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NumberRoad.UI
{
    /// <summary>Helpers that build the uGUI hierarchy from code (no prefabs, no TMP resources needed).</summary>
    public static class UIFactory
    {
        public static readonly Color Blue = new Color32(47, 116, 230, 255);
        public static readonly Color BlueDark = new Color32(26, 58, 128, 255);
        public static readonly Color Gold = new Color32(255, 194, 52, 255);
        public static readonly Color GoldDark = new Color32(196, 120, 20, 255);
        public static readonly Color Red = new Color32(226, 56, 47, 255);
        public static readonly Color Green = new Color32(62, 190, 96, 255);
        public static readonly Color Purple = new Color32(150, 84, 230, 255);
        public static readonly Color Cyan = new Color32(90, 205, 255, 255);
        public static readonly Color Gray = new Color32(150, 158, 172, 255);
        public static readonly Color Ink = new Color32(29, 36, 51, 255);
        public static readonly Color Card = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color Shade = new Color(0.04f, 0.07f, 0.16f, 0.62f);
        public static readonly Color OutlineDark = new Color(0.05f, 0.08f, 0.2f, 0.85f);

        /// <summary>Played by every button; wired up by UIRoot.</summary>
        public static Action ClickSound;

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder, bool interactive)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1700f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (interactive) go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Anchors at a single point of the parent (0..1), with the given pivot, offset and size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            RectTransform rt = Rect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }
            return image;
        }

        public static Text Text(Transform parent, string name, string value, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, Color? outline = null, float outlineDistance = 3f)
        {
            RectTransform rt = Rect(parent, name);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = GameAssets.Font;
            text.fontSize = size;
            // the bundled Japanese font is already bold; the fallback font gets bolded by Unity
            text.fontStyle = GameAssets.FontIsBold ? FontStyle.Normal : FontStyle.Bold;
            text.alignment = anchor;
            text.color = color;
            text.text = Loc.T(value);
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline.HasValue)
            {
                var effect = rt.gameObject.AddComponent<Outline>();
                effect.effectColor = outline.Value;
                effect.effectDistance = new Vector2(outlineDistance, -outlineDistance);
                effect.useGraphicAlpha = true;
            }
            return text;
        }

        /// <summary>Chunky rounded button with a darker base, a press animation and a click sound.</summary>
        public static Button Button(Transform parent, string name, string label, Color color, int fontSize, Action onClick, out Text text)
        {
            RectTransform root = Rect(parent, name);
            Image baseImage = Image(root, "Base", Color.Lerp(color, Color.black, 0.35f), GameAssets.RoundedSprite);
            Fill(baseImage.rectTransform);
            baseImage.rectTransform.offsetMax = new Vector2(0f, -10f);

            Image face = Image(root, "Face", color, GameAssets.RoundedSprite, true);
            Fill(face.rectTransform);
            face.rectTransform.offsetMin = new Vector2(0f, 10f);

            text = Text(face.rectTransform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter, OutlineDark, 3f);
            Fill(text.rectTransform);
            // a long label (or a longer language) shrinks to fit the button instead of spilling over its edges
            text.rectTransform.offsetMin = new Vector2(8f, 2f);
            text.rectTransform.offsetMax = new Vector2(-8f, -2f);
            text.gameObject.AddComponent<FitLine>();

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.85f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                ClickSound?.Invoke();
                onClick?.Invoke();
            });
            root.gameObject.AddComponent<PressScale>();
            return button;
        }

        public static void SetButtonColor(Button button, Color color)
        {
            var face = (Image)button.targetGraphic;
            face.color = color;
            Transform b = button.transform.Find("Base");
            if (b != null) b.GetComponent<Image>().color = Color.Lerp(color, Color.black, 0.35f);
        }

        /// <summary>A cyan gem (second currency).</summary>
        public static RectTransform Gem(Transform parent, float size)
        {
            Image outer = Image(parent, "Gem", new Color32(30, 110, 170, 255), GameAssets.GemSprite);
            outer.rectTransform.sizeDelta = new Vector2(size, size);
            Image inner = Image(outer.rectTransform, "Inner", Cyan, GameAssets.GemSprite);
            Fill(inner.rectTransform);
            inner.rectTransform.offsetMin = new Vector2(size * 0.14f, size * 0.14f);
            inner.rectTransform.offsetMax = new Vector2(-size * 0.14f, -size * 0.14f);
            Image shine = Image(outer.rectTransform, "Shine", new Color(1f, 1f, 1f, 0.6f), GameAssets.GemSprite);
            Place(shine.rectTransform, new Vector2(0.38f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.22f, size * 0.22f));
            return outer.rectTransform;
        }

        public static Image Star(Transform parent, float size, bool on)
        {
            Image star = Image(parent, "Star", on ? Gold : new Color(0f, 0f, 0f, 0.25f), GameAssets.StarSprite);
            star.rectTransform.sizeDelta = new Vector2(size, size);
            return star;
        }

        /// <summary>Small red counter in the top-right corner of a button (hidden at 0).</summary>
        public static Text Badge(RectTransform owner, out GameObject root)
        {
            Image dot = Image(owner, "Badge", Red, GameAssets.CircleSprite);
            Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-12f, -8f), new Vector2(56f, 56f));
            var outline = dot.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            Text text = Text(dot.rectTransform, "Count", "", 32, Color.white);
            Fill(text.rectTransform);
            root = dot.gameObject;
            return text;
        }

        /// <summary>
        /// A scrolling list: returns the content rect, which grows with its children (vertical or horizontal layout).
        /// </summary>
        public static RectTransform Scroll(RectTransform parent, string name, bool horizontal, float spacing, out ScrollRect scroll)
        {
            Image area = Image(parent, name, new Color(0f, 0f, 0f, 0f), null, true);
            scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;

            RectTransform viewport = Fill(Rect(area.rectTransform, "Viewport"));
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            RectTransform content = Rect(viewport, "Content");
            if (horizontal)
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = spacing;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(12, 12, 0, 0);
                content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = spacing;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(0, 0, 10, 10);
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            scroll.content = content;
            return content;
        }

        /// <summary>A fixed-size child for a layout list.</summary>
        public static RectTransform Item(RectTransform list, string name, Vector2 size)
        {
            RectTransform rt = Rect(list, name);
            rt.sizeDelta = size;
            var element = rt.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            return rt;
        }

        /// <summary>Gold coin made of two circles.</summary>
        public static RectTransform Coin(Transform parent, float size)
        {
            Image outer = Image(parent, "Coin", GoldDark, GameAssets.CircleSprite);
            outer.type = UnityEngine.UI.Image.Type.Simple;
            outer.rectTransform.sizeDelta = new Vector2(size, size);
            Image inner = Image(outer.rectTransform, "Inner", Gold, GameAssets.CircleSprite);
            inner.type = UnityEngine.UI.Image.Type.Simple;
            Fill(inner.rectTransform);
            inner.rectTransform.offsetMin = new Vector2(size * 0.12f, size * 0.12f);
            inner.rectTransform.offsetMax = new Vector2(-size * 0.12f, -size * 0.12f);
            return outer.rectTransform;
        }
    }

    /// <summary>
    /// Keeps a label inside its box: when the text is wider or taller than the box the font shrinks until it fits
    /// (never below <see cref="MinSize"/>). A font size set from code is taken as the new largest size.
    /// </summary>
    public sealed class FitLine : MonoBehaviour
    {
        public int MinSize = 14;
        Text _text;
        string _fitted;
        Vector2 _box;
        int _size;
        int _applied = -1;

        void Awake()
        {
            _text = GetComponent<Text>();
            _size = _text.fontSize;
        }

        void OnEnable() => Fit();

        void LateUpdate() => Fit();

        void Fit()
        {
            if (_text == null) return;
            if (_text.fontSize != _applied && _applied >= 0) _size = _text.fontSize;
            Vector2 box = ((RectTransform)transform).rect.size;
            if (ReferenceEquals(_text.text, _fitted) && box == _box && _text.fontSize == _applied) return;
            _fitted = _text.text;
            _box = box;
            _text.fontSize = _size;
            float width = _text.preferredWidth, height = _text.preferredHeight;
            float scale = 1f;
            if (width > box.x && width > 0f) scale = box.x / width;
            if (height > box.y && height > 0f) scale = Mathf.Min(scale, box.y / height);
            if (scale < 1f) _text.fontSize = Mathf.Max(MinSize, Mathf.FloorToInt(_size * scale));
            _applied = _text.fontSize;
        }
    }

    /// <summary>Reports whether any finger (or the mouse) is holding it down (DASH). Several fingers can be on it.</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        readonly System.Collections.Generic.HashSet<int> _pointers = new System.Collections.Generic.HashSet<int>();

        public bool Held => _pointers.Count > 0;

        public void OnPointerDown(PointerEventData eventData) => _pointers.Add(eventData.pointerId);
        public void OnPointerUp(PointerEventData eventData) => _pointers.Remove(eventData.pointerId);

        void OnDisable() => _pointers.Clear();
    }

    /// <summary>Squishes a button while it is held, using unscaled time (works while paused).</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float _target = 1f;
        float _scale = 1f;

        public void OnPointerDown(PointerEventData eventData) => _target = 0.93f;
        public void OnPointerUp(PointerEventData eventData) => _target = 1f;
        public void OnPointerExit(PointerEventData eventData) => _target = 1f;

        void OnDisable()
        {
            _target = 1f;
            _scale = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (Mathf.Approximately(_scale, _target)) return;
            _scale = Mathf.MoveTowards(_scale, _target, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }
    }

    /// <summary>Pops a panel in with an overshoot whenever it is enabled.</summary>
    public sealed class PopIn : MonoBehaviour
    {
        float _clock;

        void OnEnable()
        {
            _clock = 0f;
            transform.localScale = Vector3.one * 0.6f;
        }

        void Update()
        {
            if (_clock >= 1f) return;
            _clock = Mathf.Min(1f, _clock + Time.unscaledDeltaTime / 0.28f);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = _clock - 1f;
            float s = 1f + c3 * u * u * u + c1 * u * u;
            float scale = Mathf.Lerp(0.6f, 1f, s);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>Keeps a RectTransform inside Screen.safeArea (notches, home indicator, rounded corners).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _applied;
        Vector2Int _screen;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != _applied || _screen.x != Screen.width || _screen.y != Screen.height) Apply();
        }

        void Apply()
        {
            var rt = (RectTransform)transform;
            Rect safe = Screen.safeArea;
            _applied = safe;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var size = new Vector2(Screen.width, Screen.height);
            rt.anchorMin = safe.position / size;
            rt.anchorMax = (safe.position + safe.size) / size;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
