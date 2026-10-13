using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DZ.SmartPool;

namespace DZ.SmartPool.Tests
{
    public class LifecycleAndClearTests
    {
        private GameObject _prefab;

        [SetUp]
        public void Setup()
        {
            _prefab = new GameObject("Test_Prefab");
            _prefab.AddComponent<TestLifecycleObserver>();
            _prefab.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            SmartPool.ClearAll();
            if (_prefab != null)
            {
                Object.DestroyImmediate(_prefab);
            }
        }

        [UnityTest]
        public IEnumerator Clear_WithActiveObjects_SafelyDestroysOnDespawnWithoutErrors()
        {
            // 1. Spawn object from pool
            var instance = _prefab.Spawn(Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(instance);

            // 2. Clear pool while object is active in scene
            SmartPool.Clear(_prefab);

            // 3. Despawn active object: must not throw exception
            Assert.DoesNotThrow(() =>
            {
                instance.Despawn();
            });

            // 4. Object must be deactivated immediately on Despawn
            Assert.IsFalse(instance.activeSelf, "Object should be deactivated immediately upon Despawn");

            // 5. Wait 1 frame for Unity's deferred Destroy execution
            yield return null;

            // 6. Verify object is destroyed
            Assert.IsTrue(instance == null, "Object should be destroyed by Unity after frame ends");
        }

        [Test]
        public void Spawn_LifecycleOrder_ResetsStateBeforeOnEnableRuns()
        {
            // 1. First spawn
            var instance = _prefab.Spawn();
            var observer = instance.GetComponent<TestLifecycleObserver>();
            Assert.IsTrue(observer.OnEnableSawCleanHealth, "First spawn should see clean state in OnEnable");

            // 2. Despawn sets dirty state
            instance.Despawn();
            Assert.AreEqual(0, observer.Health, "Despawn should set health to dirty state");

            // 3. Spawn again: OnSpawn must reset health to 100 BEFORE OnEnable runs
            var reused = _prefab.Spawn();
            var reusedObserver = reused.GetComponent<TestLifecycleObserver>();
            Assert.IsTrue(reusedObserver.OnEnableSawCleanHealth, "Reused instance OnEnable must read reset state, not dirty state!");

            reused.Despawn();
        }

        [Test]
        public void Despawn_CalledTwice_DoesNotCorruptStackOrDuplicate()
        {
            var instance = _prefab.Spawn();
            Assert.IsNotNull(instance);

            // First despawn
            instance.Despawn();

            // Second despawn on already despawned object: must not throw or push duplicate
            Assert.DoesNotThrow(() =>
            {
                instance.Despawn();
            });

            // Spawn should yield the instance cleanly
            var next = _prefab.Spawn();
            Assert.IsNotNull(next);
            next.Despawn();
        }

        [UnityTest]
        public IEnumerator AutoDespawnTimer_CancelledWhenManuallyDespawnedAndReused()
        {
            // 1. Spawn and schedule timer for 0.15s
            var instance = _prefab.Spawn();
            instance.DespawnAfter(0.15f);

            // 2. Manually despawn immediately before timer expires
            instance.Despawn();

            // 3. Re-spawn immediately (generation token increases)
            var reused = _prefab.Spawn();
            Assert.IsTrue(reused.activeSelf);

            // 4. Wait 0.25s (longer than the original 0.15s timer)
            yield return new WaitForSeconds(0.25f);

            // 5. Stale timer from cycle #1 must NOT have despawned reused instance
            Assert.IsTrue(reused != null && reused.activeSelf, "Stale timer must be cancelled and not despawn reused object!");

            reused.Despawn();
        }

        private class TestLifecycleObserver : MonoBehaviour, IPoolable
        {
            public int Health { get; private set; } = 0;
            public bool OnEnableSawCleanHealth { get; private set; } = false;

            public void OnSpawn()
            {
                // Reset to clean state while still inactive
                Health = 100;
            }

            private void OnEnable()
            {
                OnEnableSawCleanHealth = (Health == 100);
            }

            public void OnDespawn()
            {
                // Dirty state
                Health = 0;
            }
        }
    }
}
