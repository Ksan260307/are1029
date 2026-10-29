using UnityEngine;
using UnityEngine.EventSystems;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// Relative horizontal drag from touch, mouse or keyboard, converted to metres.
    /// Drags that start on a UI button are ignored so the pause button does not steer.
    /// </summary>
    public sealed class InputReader
    {
        /// <summary>Metres of sideways movement for a drag across the short side of the screen.</summary>
        public float Sensitivity = 10.5f;
        public float KeyboardSpeed = 9f;

        public float SteerDelta { get; private set; }
        public bool PointerDown { get; private set; }

        int _finger = -1;
        bool _mouseDragging;
        Vector2 _last;

        public void Update(float unscaledDt)
        {
            SteerDelta = 0f;
            float reference = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));

            if (Input.touchCount > 0)
            {
                _mouseDragging = false;
                ReadTouches(reference);
            }
            else
            {
                _finger = -1;
                ReadMouse(reference);
            }

            float axis = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(axis) > 0.01f) SteerDelta += axis * KeyboardSpeed * unscaledDt;
            PointerDown = _finger >= 0 || _mouseDragging;
        }

        void ReadTouches(float reference)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (_finger < 0 && touch.phase == TouchPhase.Began && !OverUi(touch.fingerId))
                {
                    _finger = touch.fingerId;
                    _last = touch.position;
                    continue;
                }
                if (touch.fingerId != _finger) continue;
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    SteerDelta += (touch.position.x - _last.x) / reference * Sensitivity;
                    _last = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _finger = -1;
                }
            }
        }

        void ReadMouse(float reference)
        {
            if (!Input.mousePresent) return;
            Vector2 position = Input.mousePosition;
            if (Input.GetMouseButtonDown(0) && !OverUi(-1))
            {
                _mouseDragging = true;
                _last = position;
            }
            else if (_mouseDragging && Input.GetMouseButton(0))
            {
                SteerDelta += (position.x - _last.x) / reference * Sensitivity;
                _last = position;
            }
            if (Input.GetMouseButtonUp(0)) _mouseDragging = false;
        }

        static bool OverUi(int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
        }

        public void Reset()
        {
            _finger = -1;
            _mouseDragging = false;
            SteerDelta = 0f;
        }
    }
}
