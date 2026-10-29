using System;
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
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = color;
            text.text = value;
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
