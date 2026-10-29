using System.Collections;
using NumberRoad.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NumberRoad.Tests
{
    /// <summary>Boots the real game, lets the auto pilot play for a while and checks nothing breaks.</summary>
    public class SmokeTests
    {
        GameRoot _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = GameRoot.Instance != null ? GameRoot.Instance : new GameObject("Game").AddComponent<GameRoot>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (_root != null) Object.Destroy(_root.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Game_BootsToTitle_ThenAutoPilotAdvancesTheSquad()
        {
            Assert.AreEqual(GameState.Title, _root.State);
            Assert.NotNull(_root.Session);

            _root.AutoPlay = true;
            _root.Play();
            Assert.AreEqual(GameState.Playing, _root.State);

            float startZ = _root.Session.Squad.Z;
            Time.timeScale = 2f;
            float elapsed = 0f;
            while (elapsed < 6f && _root.State == GameState.Playing)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Time.timeScale = 1f;

            Assert.Greater(_root.Session.Squad.Z, startZ + 5f, "the squad should march forward");
            Assert.GreaterOrEqual(_root.Session.Squad.Count, 0);
        }

        [UnityTest]
        public IEnumerator PauseAndResume_FreezeTime()
        {
            _root.Play();
            yield return null;
            _root.Pause();
            Assert.AreEqual(GameState.Paused, _root.State);
            Assert.AreEqual(0f, Time.timeScale);
            float z = _root.Session.Squad.Z;
            yield return null;
            yield return null;
            Assert.AreEqual(z, _root.Session.Squad.Z, "nothing moves while paused");
            _root.Resume();
            Assert.AreEqual(GameState.Playing, _root.State);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
