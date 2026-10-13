using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using DZ.SmartPool;
using Object = UnityEngine.Object;

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
                UnityEngine.Object.DestroyImmediate(_prefab);
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
                UnityEngine.Object.DestroyImmediate(activePrefab);
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
                UnityEngine.Object.DestroyImmediate(reentrantPrefab);
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
                UnityEngine.Object.DestroyImmediate(throwingPrefab);
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
                if (prefabA != null) UnityEngine.Object.DestroyImmediate(prefabA);
                if (prefabB != null) UnityEngine.Object.DestroyImmediate(prefabB);
            }
        }

        [Test]
        public void ExternalDestroy_DoesNotBreakSubsequentSpawn()
        {
            // 1. Prewarm pool
            SmartPool.Prewarm(_prefab, 3);

            // 2. Spawn one instance and destroy it externally
            var activeInstance = _prefab.Spawn();
            UnityEngine.Object.DestroyImmediate(activeInstance);

            // 3. Despawning destroyed object should safely be ignored
            Assert.DoesNotThrow(() =>
            {
                SmartPool.Despawn(activeInstance);
            });

            // 4. Destroy an idle instance that was returned to the pool
            var idleInstance = _prefab.Spawn();
            idleInstance.Despawn();
            UnityEngine.Object.DestroyImmediate(idleInstance);

            // 5. Subsequent spawn should skip the null instance and return a valid active object
            var nextInstance = _prefab.Spawn();
            Assert.IsNotNull(nextInstance, "Pool should skip destroyed idle instances and provide a fresh one");
            Assert.IsTrue(nextInstance.activeSelf);

            nextInstance.Despawn();
        }

        [Test]
        public void Spawn_LifecycleOrder_AwakeBeforeOnSpawnBeforeOnEnable()
        {
            // Create a realistic prefab with physics, collider, and child sub-component
            var realisticPrefab = new GameObject("Realistic_Prefab");
            realisticPrefab.AddComponent<Rigidbody>();
            realisticPrefab.AddComponent<BoxCollider>();
            realisticPrefab.AddComponent<TargetingSubComponent>();
            var tracker = realisticPrefab.AddComponent<RealisticLifecycleOrderTracker>();

            try
            {
                // First spawn: must execute Awake -> OnSpawn -> OnEnable in that exact order
                var instance = realisticPrefab.Spawn();
                var instanceTracker = instance.GetComponent<RealisticLifecycleOrderTracker>();

                Assert.AreEqual(1, instanceTracker.AwakeCount, "Awake should run exactly once on new instance");
                Assert.AreEqual(1, instanceTracker.OnSpawnCount, "OnSpawn should run on first spawn");
                Assert.AreEqual(1, instanceTracker.OnEnableCount, "OnEnable should run on first spawn");
                Assert.IsNotNull(instanceTracker.CachedSubComponent, "SubComponent cached in Awake must be available");
                Assert.IsTrue(instanceTracker.SubComponentSawResetInOnSpawn, "SubComponent must be valid and reset inside OnSpawn");
                Assert.IsTrue(instanceTracker.OnEnableSawCleanState, "OnEnable must observe clean reset state");

                // Verify exact event ordering on first spawn: Awake -> OnSpawn -> OnEnable
                CollectionAssert.AreEqual(
                    new[] { "Awake", "OnSpawn", "OnEnable" },
                    instanceTracker.ExecutionLog,
                    "Execution order on first spawn must be Awake -> OnSpawn -> OnEnable"
                );

                // Despawn
                instance.Despawn();
                Assert.AreEqual("OnDespawn", instanceTracker.ExecutionLog[instanceTracker.ExecutionLog.Count - 1]);

                // Second spawn (reuse from pool)
                var reused = realisticPrefab.Spawn();
                var reusedTracker = reused.GetComponent<RealisticLifecycleOrderTracker>();

                Assert.AreEqual(1, reusedTracker.AwakeCount, "Awake must not run again on reused instance");
                Assert.AreEqual(2, reusedTracker.OnSpawnCount, "OnSpawn runs on second spawn");
                Assert.AreEqual(2, reusedTracker.OnEnableCount, "OnEnable runs on second spawn");

                // Verify cumulative order: Awake -> OnSpawn -> OnEnable -> OnDespawn -> OnSpawn -> OnEnable
                CollectionAssert.AreEqual(
                    new[] { "Awake", "OnSpawn", "OnEnable", "OnDespawn", "OnSpawn", "OnEnable" },
                    reusedTracker.ExecutionLog,
                    "Reused instance must execute OnSpawn -> OnEnable without duplicate Awake"
                );

                reused.Despawn();
            }
            finally
            {
                SmartPool.Clear(realisticPrefab);
                UnityEngine.Object.DestroyImmediate(realisticPrefab);
            }
        }

        [UnityTest]
        public IEnumerator DespawnAfter_UnscaledTime_DespawnsWhenTimeScaleIsZero()
        {
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;

                var instance = _prefab.Spawn();
                Assert.IsTrue(instance.activeSelf);

                // Schedule unscaled despawn after 0.1s
                instance.DespawnAfter(0.1f, useUnscaledTime: true);

                // Wait in real time using unscaled delta time
                float elapsed = 0f;
                while (elapsed < 0.35f && instance != null && instance.activeSelf)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                }

                Assert.IsFalse(instance.activeSelf, "Object should be despawned even when Time.timeScale is 0");
            }
            finally
            {
                // Always restore Time.timeScale even if assertions fail
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator AutoClear_OnSceneUnloaded_ClearsSceneLocalPools()
        {
            var prefabLocal = new GameObject("Prefab_SceneLocal");
            var prefabPersistent = new GameObject("Prefab_Persistent");
            prefabLocal.SetActive(false);
            prefabPersistent.SetActive(false);

            try
            {
                SmartPool.SetSceneLocal(prefabLocal, true);
                SmartPool.SetSceneLocal(prefabPersistent, false);

                var instanceLocal = prefabLocal.Spawn();
                var instancePersistent = prefabPersistent.Spawn();

                Assert.IsTrue(instanceLocal.activeSelf);
                Assert.IsTrue(instancePersistent.activeSelf);

                // Simulate scene unload
                SmartPoolManager.Instance.OnSceneUnloadedInternal(default);

                // Active object from local pool should safely destroy on Despawn
                Assert.DoesNotThrow(() =>
                {
                    instanceLocal.Despawn();
                });
                Assert.IsFalse(instanceLocal.activeSelf);

                // Active object from persistent pool should return to pool normally
                Assert.DoesNotThrow(() =>
                {
                    instancePersistent.Despawn();
                });
                Assert.IsFalse(instancePersistent.activeSelf);

                // Persistent pool can spawn again
                var nextPersistent = prefabPersistent.Spawn();
                Assert.IsNotNull(nextPersistent);
                Assert.IsTrue(nextPersistent.activeSelf);
                nextPersistent.Despawn();

                yield return null;

                // Local instance should be destroyed by Unity's deferred destroy
                Assert.IsTrue(instanceLocal == null, "instanceLocal should be destroyed because its pool was cleared");
            }
            finally
            {
                if (prefabLocal != null) UnityEngine.Object.DestroyImmediate(prefabLocal);
                if (prefabPersistent != null) UnityEngine.Object.DestroyImmediate(prefabPersistent);
            }
        }

        [Test]
        public void Spawn_PrefabWithInactiveChild_RunsAwakeBeforeOnSpawnAndPreservesInactiveState()
        {
            // 1. Construct prefab with active root and inactive child
            var prefab = new GameObject("PrefabWithInactiveChild");
            var childGo = new GameObject("InactiveChild");
            childGo.transform.SetParent(prefab.transform, false);
            childGo.AddComponent<ChildDataComponent>();
            childGo.AddComponent<InactiveChildTracker>();
            childGo.SetActive(false); // Inactive child on prefab

            try
            {
                // 2. Spawn instance
                var instance = prefab.Spawn();
                var instanceChild = instance.transform.Find("InactiveChild").gameObject;
                var instanceTracker = instanceChild.GetComponent<InactiveChildTracker>();

                // Awake must have run for child during Awake Pre-pass
                Assert.AreEqual(1, instanceTracker.AwakeCount, "Inactive child component must have Awake() executed before spawn");
                Assert.IsNotNull(instanceTracker.CachedData, "CachedData inside Awake() must not be null");
                Assert.IsTrue(instanceTracker.OnSpawnSawValidCachedData, "OnSpawn must have observed initialized data from Awake");

                // Child must still remain inactive until gameplay activates it
                Assert.IsFalse(instanceChild.activeSelf, "Child GameObject must preserve its inactive state on spawn");
                Assert.AreEqual(0, instanceTracker.OnEnableCount, "OnEnable must NOT run while child GameObject is still inactive");

                // 3. Gameplay activates the child
                instanceChild.SetActive(true);
                Assert.AreEqual(1, instanceTracker.OnEnableCount, "OnEnable should run when child is explicitly activated in gameplay");
                Assert.AreEqual(1, instanceTracker.AwakeCount, "Awake should not run again on child activation");

                // 4. Despawn
                instance.Despawn();

                // 5. Spawn reused
                var reused = prefab.Spawn();
                var reusedChild = reused.transform.Find("InactiveChild").gameObject;
                var reusedTracker = reusedChild.GetComponent<InactiveChildTracker>();

                Assert.AreEqual(1, reusedTracker.AwakeCount, "Awake must never run again on reused instance");
                Assert.AreEqual(2, reusedTracker.OnSpawnCount, "OnSpawn runs on second cycle");

                reused.Despawn();
            }
            finally
            {
                SmartPool.Clear(prefab);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [UnityTest]
        public IEnumerator AutoClear_RealSceneUnloaded_ClearsSceneLocalPools()
        {
            // Create a temporary scene dynamically in PlayMode
            var tempScene = SceneManager.CreateScene("TestRealScene_" + Guid.NewGuid().ToString("N"));

            var prefabLocal = new GameObject("Prefab_RealSceneLocal");
            var prefabPersistent = new GameObject("Prefab_RealScenePersistent");
            prefabLocal.SetActive(false);
            prefabPersistent.SetActive(false);

            try
            {
                SmartPool.SetSceneLocal(prefabLocal, true);
                SmartPool.SetSceneLocal(prefabPersistent, false);

                var instanceLocal = prefabLocal.Spawn();
                var instancePersistent = prefabPersistent.Spawn();

                Assert.IsTrue(instanceLocal.activeSelf);
                Assert.IsTrue(instancePersistent.activeSelf);

                // Unload the scene for real using Unity's native SceneManager
                var unloadOp = SceneManager.UnloadSceneAsync(tempScene);
                while (!unloadOp.isDone)
                {
                    yield return null;
                }

                // Active object from local pool should safely destroy on Despawn
                Assert.DoesNotThrow(() =>
                {
                    instanceLocal.Despawn();
                });
                Assert.IsFalse(instanceLocal.activeSelf);

                // Active object from persistent pool should return to pool normally
                Assert.DoesNotThrow(() =>
                {
                    instancePersistent.Despawn();
                });
                Assert.IsFalse(instancePersistent.activeSelf);

                // Persistent pool can spawn again
                var nextPersistent = prefabPersistent.Spawn();
                Assert.IsNotNull(nextPersistent);
                Assert.IsTrue(nextPersistent.activeSelf);
                nextPersistent.Despawn();

                yield return null;

                // Local instance should be destroyed by Unity's deferred destroy
                Assert.IsTrue(instanceLocal == null, "instanceLocal should be destroyed because its pool was cleared");
            }
            finally
            {
                if (prefabLocal != null) UnityEngine.Object.DestroyImmediate(prefabLocal);
                if (prefabPersistent != null) UnityEngine.Object.DestroyImmediate(prefabPersistent);
            }
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

        private class TargetingSubComponent : MonoBehaviour
        {
            public bool WasReset { get; private set; }

            public void ResetTarget()
            {
                WasReset = true;
            }

            public void SetDirty()
            {
                WasReset = false;
            }
        }

        private class RealisticLifecycleOrderTracker : MonoBehaviour, IPoolable
        {
            public List<string> ExecutionLog { get; } = new List<string>();
            public TargetingSubComponent CachedSubComponent { get; private set; }
            public int AwakeCount { get; private set; }
            public int OnSpawnCount { get; private set; }
            public int OnEnableCount { get; private set; }
            public bool SubComponentSawResetInOnSpawn { get; private set; }
            public bool OnEnableSawCleanState { get; private set; }

            private void Awake()
            {
                AwakeCount++;
                ExecutionLog.Add("Awake");
                CachedSubComponent = GetComponent<TargetingSubComponent>();
            }

            public void OnSpawn()
            {
                OnSpawnCount++;
                ExecutionLog.Add("OnSpawn");
                if (CachedSubComponent != null)
                {
                    CachedSubComponent.ResetTarget();
                    SubComponentSawResetInOnSpawn = true;
                }
            }

            private void OnEnable()
            {
                OnEnableCount++;
                ExecutionLog.Add("OnEnable");
                if (CachedSubComponent != null && CachedSubComponent.WasReset)
                {
                    OnEnableSawCleanState = true;
                }
            }

            public void OnDespawn()
            {
                ExecutionLog.Add("OnDespawn");
                if (CachedSubComponent != null)
                {
                    CachedSubComponent.SetDirty();
                }
            }
        }

        private class ChildDataComponent : MonoBehaviour
        {
            public int Value = 42;
        }

        private class InactiveChildTracker : MonoBehaviour, IPoolable
        {
            public ChildDataComponent CachedData { get; private set; }
            public int AwakeCount { get; private set; }
            public int OnSpawnCount { get; private set; }
            public int OnEnableCount { get; private set; }
            public bool OnSpawnSawValidCachedData { get; private set; }

            private void Awake()
            {
                AwakeCount++;
                CachedData = GetComponent<ChildDataComponent>();
            }

            public void OnSpawn()
            {
                OnSpawnCount++;
                if (CachedData != null && CachedData.Value == 42)
                {
                    OnSpawnSawValidCachedData = true;
                }
            }

            private void OnEnable()
            {
                OnEnableCount++;
            }

            public void OnDespawn()
            {
            }
        }
    }
}
