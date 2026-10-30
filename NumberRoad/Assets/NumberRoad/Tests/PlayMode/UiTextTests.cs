using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NumberRoad.Core;
using NumberRoad.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NumberRoad.Tests
{
    /// <summary>
    /// Opens every screen in both languages - the title, each panel, the HUD, the buff cards, the pause menu and the
    /// result - and checks that no text needs more room than its box (it would spill out or be cut off), and that
    /// nothing is left in English while Japanese is on.
    /// </summary>
    public class UiTextTests
    {
        static readonly string[] Panels = { "Settings", "LevelPick", "Units", "Enemies", "Ranking", "Stats", "Daily", "Lucky", "Challenge", "Endless", "Awards" };
        // words that stay in Latin letters on purpose
        static readonly HashSet<string> Allowed = new HashSet<string> { "LANGUAGE", "ENGLISH", "III" };
        static readonly Regex Word = new Regex("[A-Za-z]{3,}");

        GameRoot _root;
        Language _before;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = GameRoot.Instance != null ? GameRoot.Instance : new GameObject("Game").AddComponent<GameRoot>();
            _before = (Language)_root.Save.language;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null)
            {
                if (_root.State == GameState.Title) _root.SetLanguage(_before);
                _root.TimeScale = 1f;
                Object.Destroy(_root.gameObject);
            }
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator EveryScreen_TextFitsItsBox([Values(Language.Japanese, Language.English)] Language language)
        {
            var problems = new List<string>();
            _root.SetLanguage(language);
            _root.UI.CloseTopPanel();
            yield return null;
            Assert.AreEqual(language, Loc.Language);
            Check("title", language, problems);

            foreach (string name in Panels)
            {
                Click(name);
                yield return null;
                yield return null;
                Check(name, language, problems);
                _root.UI.CloseTopPanel();
                yield return null;
            }

            // the HUD, then the buff cards at the end of the first wave
            _root.AutoPlay = true;
            _root.Play();
            LevelSession s = _root.Session;
            _root.TimeScale = 4f;
            float clock = 0f;
            while (s.Countdown > -0.5f) yield return null;
            Check("hud", language, problems);
            while (!s.ChoosingBuff && _root.State == GameState.Playing && clock < 60f)
            {
                _root.AutoPlay = s.Squad.Z < s.Def.waveEnds[0] - 3f;
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            for (int i = 0; i < 20; i++) yield return null;
            if (s.ChoosingBuff) Check("buff cards", language, problems);
            else problems.Add("never reached the buff cards");

            _root.Pause();
            yield return null;
            Check("pause", language, problems);
            _root.Resume();
            if (s.ChoosingBuff) _root.ChooseBuff(0);

            // lose on purpose for the result screen
            s.Squad.Lose(s.Squad.Count, s.Squad.Position, false);
            clock = 0f;
            while (_root.State != GameState.Result && clock < 10f)
            {
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            for (int i = 0; i < 10; i++) yield return null;
            Check("result", language, problems);
            _root.Home();
            yield return null;

            Assert.IsEmpty(problems, language + ":\n" + string.Join("\n", problems));
        }

        void Click(string name)
        {
            foreach (Button b in _root.UI.GetComponentsInChildren<Button>(false))
            {
                if (b.name != name) continue;
                b.onClick.Invoke();
                return;
            }
            Assert.Fail("no button " + name);
        }

        static void Check(string screen, Language language, List<string> problems)
        {
            Canvas.ForceUpdateCanvases();
            int measured = 0;
            foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // world labels (numbers over gates and enemies) float over the scene by design
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text) || t.canvas == null || t.canvas.rootCanvas.name == "Labels") continue;
                if (t.GetComponentInParent<CanvasGroup>() is CanvasGroup g && g.alpha <= 0.01f) continue;
                string where = $"[{screen}] {Path(t.transform)} \"{t.text.Replace("\n", "\\n")}\"";

                Rect box = t.rectTransform.rect;
                bool wraps = t.horizontalOverflow == HorizontalWrapMode.Wrap;
                TextGenerationSettings settings = t.GetGenerationSettings(new Vector2(wraps ? box.width : 0f, 0f));
                if (t.resizeTextForBestFit)
                {
                    // best fit shrinks the text, but it has to fit at its smallest size
                    settings.resizeTextForBestFit = false;
                    settings.fontSize = t.resizeTextMinSize;
                }
                float unit = 1f / t.pixelsPerUnit;
                float width = t.cachedTextGeneratorForLayout.GetPreferredWidth(t.text, settings) * unit;
                float height = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) * unit;
                if (width > 0f) measured++;
                if (!wraps && width > box.width + 4f) problems.Add($"{where} needs {width:0} wide, box {box.width:0}");
                if (height > box.height + 4f) problems.Add($"{where} needs {height:0} high, box {box.height:0}");

                if (language == Language.Japanese)
                {
                    foreach (Match m in Word.Matches(t.text))
                    {
                        if (!Allowed.Contains(m.Value.ToUpperInvariant())) problems.Add($"{where} is not translated ({m.Value})");
                    }
                }
            }
            if (measured == 0) problems.Add($"[{screen}] no text could be measured");
        }

        static string Path(Transform t)
        {
            string path = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
