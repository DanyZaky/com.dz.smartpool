using System;
using System.Collections;
using System.Text.RegularExpressions;
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

        [Test]
        public void Spawn_ActivePrefab_DoesNotRunOnEnableBeforeInitialization()
        {
            // Active prefab created with activeSelf == true
            var activePrefab = new GameObject("Active_Prefab");
            var tracker = activePrefab.AddComponent<ActivePrefabLifecycleTracker>();
            Assert.IsTrue(activePrefab.activeSelf, "Prefab should be active to reproduce active prefab scenario");

            try
            {
                // First spawn: must run Awake once, OnSpawn once, OnEnable once
                var instance = activePrefab.Spawn();
                var instanceTracker = instance.GetComponent<ActivePrefabLifecycleTracker>();

                Assert.AreEqual(1, instanceTracker.AwakeCallCount, "Awake should be called exactly once on first spawn");
                Assert.AreEqual(1, instanceTracker.OnSpawnCallCount, "OnSpawn should be called exactly once on first spawn");
                Assert.AreEqual(1, instanceTracker.OnEnableCallCount, "OnEnable should be called exactly once on first spawn");
                Assert.IsTrue(instanceTracker.OnEnableAlwaysSawInitializedState, "OnEnable must never run before OnSpawn has initialized the state");

                // Despawn
                instance.Despawn();

                // Second spawn: Awake is not called again, OnSpawn and OnEnable run again
                var reused = activePrefab.Spawn();
                var reusedTracker = reused.GetComponent<ActivePrefabLifecycleTracker>();

                Assert.AreEqual(1, reusedTracker.AwakeCallCount, "Awake should not run again on reused instance");
                Assert.AreEqual(2, reusedTracker.OnSpawnCallCount, "OnSpawn should run for the second time");
                Assert.AreEqual(2, reusedTracker.OnEnableCallCount, "OnEnable should run for the second time");
                Assert.IsTrue(reusedTracker.OnEnableAlwaysSawInitializedState, "OnEnable on reused instance must also see clean initialized state");

                reused.Despawn();
            }
            finally
            {
                SmartPool.Clear(activePrefab);
                Object.DestroyImmediate(activePrefab);
            }
        }

        [Test]
        public void Despawn_ReentrantFromOnDespawn_DoesNotRecurse()
        {
            var reentrantPrefab = new GameObject("Reentrant_Prefab");
            reentrantPrefab.AddComponent<ReentrantPoolable>();
            reentrantPrefab.SetActive(false);

            try
            {
                var instance = reentrantPrefab.Spawn();
                var poolable = instance.GetComponent<ReentrantPoolable>();

                // Trigger despawn: the callback inside will attempt to call Despawn() re-entrantly
                Assert.DoesNotThrow(() =>
                {
                    instance.Despawn();
                });

                Assert.AreEqual(1, poolable.DespawnCallbackCount, "OnDespawn should only be invoked once even if called re-entrantly");
                Assert.IsFalse(instance.activeSelf, "Instance should be deactivated");

                // Pool should be healthy and yield the instance on next spawn
                var reused = reentrantPrefab.Spawn();
                Assert.IsNotNull(reused);
                Assert.IsTrue(reused.activeSelf);
                reused.Despawn();
            }
            finally
            {
                SmartPool.Clear(reentrantPrefab);
                Object.DestroyImmediate(reentrantPrefab);
            }
        }

        [Test]
        public void Despawn_CallbackThrows_PoolRemainsConsistent()
        {
            var throwingPrefab = new GameObject("Throwing_Prefab");
            throwingPrefab.AddComponent<ThrowingPoolable>();
            throwingPrefab.SetActive(false);

            try
            {
                var instance = throwingPrefab.Spawn();

                // Expect the exceptions logged by SmartPool
                LogAssert.Expect(LogType.Exception, new Regex("Simulated exception in OnDespawn"));
                LogAssert.Expect(LogType.Exception, new Regex("Simulated exception in onComplete"));

                // Despawn with throwing callback and throwing onComplete
                Assert.DoesNotThrow(() =>
                {
                    instance.Despawn(() => throw new InvalidOperationException("Simulated exception in onComplete"));
                });

                // Object should still be deactivated cleanly despite exceptions
                Assert.IsFalse(instance.activeSelf, "Object should be deactivated despite callback exceptions");

                // Pool remains healthy and functional
                var reused = throwingPrefab.Spawn();
                Assert.IsNotNull(reused);
                Assert.IsTrue(reused.activeSelf);

                // Despawn reused cleanly (expect the OnDespawn exception again)
                LogAssert.Expect(LogType.Exception, new Regex("Simulated exception in OnDespawn"));
                reused.Despawn();
            }
            finally
            {
                SmartPool.Clear(throwingPrefab);
                Object.DestroyImmediate(throwingPrefab);
            }
        }

        [UnityTest]
        public IEnumerator ClearAll_WithActiveObjects_SafelyDestroysOnDespawn()
        {
            var prefabA = new GameObject("Prefab_A");
            var prefabB = new GameObject("Prefab_B");
            prefabA.SetActive(false);
            prefabB.SetActive(false);

            try
            {
                var instanceA = prefabA.Spawn();
                var instanceB = prefabB.Spawn();

                Assert.IsTrue(instanceA.activeSelf);
                Assert.IsTrue(instanceB.activeSelf);

                // Clear all pools while instances are active
                SmartPool.ClearAll();

                // Despawn active instances: must not throw
                Assert.DoesNotThrow(() =>
                {
                    instanceA.Despawn();
                    instanceB.Despawn();
                });

                // Must be immediately deactivated
                Assert.IsFalse(instanceA.activeSelf);
                Assert.IsFalse(instanceB.activeSelf);

                // Wait 1 frame for deferred Destroy
                yield return null;

                Assert.IsTrue(instanceA == null, "instanceA should be destroyed after frame ends");
                Assert.IsTrue(instanceB == null, "instanceB should be destroyed after frame ends");
            }
            finally
            {
                if (prefabA != null) Object.DestroyImmediate(prefabA);
                if (prefabB != null) Object.DestroyImmediate(prefabB);
            }
        }

        [Test]
        public void ExternalDestroy_DoesNotBreakSubsequentSpawn()
        {
            // 1. Prewarm pool
            SmartPool.Prewarm(_prefab, 3);

            // 2. Spawn one instance and destroy it externally
            var activeInstance = _prefab.Spawn();
            Object.DestroyImmediate(activeInstance);

            // 3. Despawning destroyed object should safely be ignored
            Assert.DoesNotThrow(() =>
            {
                SmartPool.Despawn(activeInstance);
            });

            // 4. Destroy an idle instance that was returned to the pool
            var idleInstance = _prefab.Spawn();
            idleInstance.Despawn();
            Object.DestroyImmediate(idleInstance);

            // 5. Subsequent spawn should skip the null instance and return a valid active object
            var nextInstance = _prefab.Spawn();
            Assert.IsNotNull(nextInstance, "Pool should skip destroyed idle instances and provide a fresh one");
            Assert.IsTrue(nextInstance.activeSelf);

            nextInstance.Despawn();
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

        private class ActivePrefabLifecycleTracker : MonoBehaviour, IPoolable
        {
            public int AwakeCallCount { get; private set; }
            public int OnEnableCallCount { get; private set; }
            public int OnSpawnCallCount { get; private set; }
            public bool OnEnableAlwaysSawInitializedState { get; private set; } = true;
            public bool IsInitializedByOnSpawn { get; private set; }

            private void Awake()
            {
                AwakeCallCount++;
            }

            public void OnSpawn()
            {
                OnSpawnCallCount++;
                IsInitializedByOnSpawn = true;
            }

            private void OnEnable()
            {
                OnEnableCallCount++;
                if (!IsInitializedByOnSpawn)
                {
                    OnEnableAlwaysSawInitializedState = false;
                }
            }

            public void OnDespawn()
            {
                IsInitializedByOnSpawn = false;
            }
        }

        private class ReentrantPoolable : MonoBehaviour, IPoolable
        {
            public int DespawnCallbackCount { get; private set; }

            public void OnSpawn() { }

            public void OnDespawn()
            {
                DespawnCallbackCount++;
                gameObject.Despawn();
            }
        }

        private class ThrowingPoolable : MonoBehaviour, IPoolable
        {
            public void OnSpawn() { }

            public void OnDespawn()
            {
                throw new InvalidOperationException("Simulated exception in OnDespawn");
            }
        }
    }
}
