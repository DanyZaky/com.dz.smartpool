using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DZ.SmartPool;

namespace DZ.SmartPool.Tests
{
    public class CapacityAndTrimmingTests
    {
        private GameObject _prefab;

        [SetUp]
        public void Setup()
        {
            _prefab = new GameObject("Test_CapacityPrefab");
            _prefab.AddComponent<CapacityTestPoolable>();
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
        public IEnumerator SetCapacity_WhenReduced_ImmediatelyTrimsExcessIdleObjects()
        {
            // 1. Prewarm 5 idle objects
            SmartPool.Prewarm(_prefab, 5);
            Assert.AreEqual(5, SmartPool.GetIdleCount(_prefab));
            Assert.AreEqual(-1, SmartPool.GetCapacity(_prefab));

            // 2. Reduce capacity to 2
            SmartPool.SetCapacity(_prefab, 2);

            // 3. Idle count must be immediately trimmed to 2
            Assert.AreEqual(2, SmartPool.GetIdleCount(_prefab));
            Assert.AreEqual(2, SmartPool.GetCapacity(_prefab));

            // 4. Wait 1 frame for Unity's deferred Destroy execution
            yield return null;

            // 5. Remaining 2 objects can be spawned from pool
            var obj1 = _prefab.Spawn();
            var obj2 = _prefab.Spawn();
            Assert.IsNotNull(obj1);
            Assert.IsNotNull(obj2);
            Assert.AreEqual(0, SmartPool.GetIdleCount(_prefab));

            obj1.Despawn();
            obj2.Despawn();
            Assert.AreEqual(2, SmartPool.GetIdleCount(_prefab));
        }

        [Test]
        public void SetCapacity_Unlimited_RetainsExistingIdleObjects()
        {
            // 1. Set capacity to 5 and prewarm 5
            SmartPool.SetCapacity(_prefab, 5);
            SmartPool.Prewarm(_prefab, 5);
            Assert.AreEqual(5, SmartPool.GetIdleCount(_prefab));
            Assert.AreEqual(5, SmartPool.GetCapacity(_prefab));

            // 2. Reset capacity to unlimited (-1)
            SmartPool.SetCapacity(_prefab, -1);
            Assert.AreEqual(-1, SmartPool.GetCapacity(_prefab));

            // 3. Existing 5 idle objects must be retained without being destroyed
            Assert.AreEqual(5, SmartPool.GetIdleCount(_prefab));

            // 4. Prewarm additional objects beyond original capacity of 5
            SmartPool.Prewarm(_prefab, 5);
            Assert.AreEqual(10, SmartPool.GetIdleCount(_prefab));
        }

        [UnityTest]
        public IEnumerator SetCapacity_Zero_DisablesCachingAndDestroysOnDespawn()
        {
            // 1. Set capacity to 0 (strict zero-cache mode)
            SmartPool.SetCapacity(_prefab, 0);
            Assert.AreEqual(0, SmartPool.GetCapacity(_prefab));

            // 2. Spawn an instance
            var instance = _prefab.Spawn();
            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.activeSelf);
            var poolable = instance.GetComponent<CapacityTestPoolable>();
            Assert.AreEqual(1, poolable.OnSpawnCount);

            bool callbackInvoked = false;

            // 3. Despawn instance with completion callback
            instance.Despawn(() => callbackInvoked = true);

            // 4. OnDespawn lifecycle and onComplete callback must execute per contract
            Assert.AreEqual(1, poolable.OnDespawnCount);
            Assert.IsTrue(callbackInvoked);

            // 5. Object must be deactivated immediately and must NOT enter idle stack
            Assert.IsFalse(instance.activeSelf, "Instance should be deactivated immediately upon despawn");
            Assert.AreEqual(0, SmartPool.GetIdleCount(_prefab), "Zero-capacity pool must hold 0 idle instances");

            // 6. Wait 1 frame for Unity's deferred Destroy execution
            yield return null;

            // 7. Verify instance is destroyed
            Assert.IsTrue(instance == null, "Instance should be destroyed after frame ends when capacity is 0");
        }

        [Test]
        public void Prewarm_RespectsMaxCapacityLimit()
        {
            // Prewarm 10 with maxCapacity = 3
            SmartPool.Prewarm(_prefab, 10, 3);

            Assert.AreEqual(3, SmartPool.GetCapacity(_prefab));
            Assert.AreEqual(3, SmartPool.GetIdleCount(_prefab));
        }

        [Test]
        public void Prewarm_WhenCapacityIsZero_CreatesNoInstances()
        {
            // Calling Prewarm with maxCapacity = 0 should create zero instances
            SmartPool.Prewarm(_prefab, 5, 0);

            Assert.AreEqual(0, SmartPool.GetCapacity(_prefab));
            Assert.AreEqual(0, SmartPool.GetIdleCount(_prefab));
        }

        [Test]
        public void Prewarm_WhenPoolAlreadyPartiallyFilled_CreatesOnlyRemainingCapacity()
        {
            // 1. Set capacity to 5
            SmartPool.SetCapacity(_prefab, 5);

            // 2. Prewarm 3 objects
            SmartPool.Prewarm(_prefab, 3);
            Assert.AreEqual(3, SmartPool.GetIdleCount(_prefab));

            // 3. Prewarm 10 more objects: with capacity 5 and 3 already idle, only 2 should be created
            SmartPool.Prewarm(_prefab, 10);
            Assert.AreEqual(5, SmartPool.GetIdleCount(_prefab));

            // 4. Subsequent Prewarm when pool is full should create 0 objects
            SmartPool.Prewarm(_prefab, 5);
            Assert.AreEqual(5, SmartPool.GetIdleCount(_prefab));
        }

        [UnityTest]
        public IEnumerator Despawn_WhenPoolFull_DestroysExcessInstancesCleanly()
        {
            // 1. Set capacity to 2
            SmartPool.SetCapacity(_prefab, 2);

            // 2. Spawn 4 instances
            var obj1 = _prefab.Spawn();
            var obj2 = _prefab.Spawn();
            var obj3 = _prefab.Spawn();
            var obj4 = _prefab.Spawn();

            Assert.AreEqual(0, SmartPool.GetIdleCount(_prefab));

            // 3. Despawn obj1 and obj2 -> fills pool to capacity (2)
            obj1.Despawn();
            obj2.Despawn();
            Assert.AreEqual(2, SmartPool.GetIdleCount(_prefab));

            bool obj3CallbackInvoked = false;
            var poolable3 = obj3.GetComponent<CapacityTestPoolable>();

            // 4. Despawn obj3 -> pool already full, so obj3 runs lifecycle, is deactivated, and destroyed
            obj3.Despawn(() => obj3CallbackInvoked = true);
            Assert.AreEqual(1, poolable3.OnDespawnCount);
            Assert.IsTrue(obj3CallbackInvoked);
            Assert.IsFalse(obj3.activeSelf);
            Assert.AreEqual(2, SmartPool.GetIdleCount(_prefab));

            // 5. Despawn obj4 -> also excess
            obj4.Despawn();
            Assert.IsFalse(obj4.activeSelf);
            Assert.AreEqual(2, SmartPool.GetIdleCount(_prefab));

            // 6. Wait 1 frame for Unity's deferred Destroy execution
            yield return null;

            Assert.IsTrue(obj1 != null);
            Assert.IsTrue(obj2 != null);
            Assert.IsTrue(obj3 == null);
            Assert.IsTrue(obj4 == null);
        }

        [UnityTest]
        public IEnumerator SetCapacity_BelowActiveCount_DestroysExcessOnSubsequentDespawn()
        {
            // 1. Spawn 4 active objects with unlimited capacity
            var obj1 = _prefab.Spawn();
            var obj2 = _prefab.Spawn();
            var obj3 = _prefab.Spawn();
            var obj4 = _prefab.Spawn();

            Assert.AreEqual(0, SmartPool.GetIdleCount(_prefab));

            // 2. Lower capacity to 1 while all 4 objects are still active in scene
            SmartPool.SetCapacity(_prefab, 1);
            Assert.AreEqual(1, SmartPool.GetCapacity(_prefab));

            // 3. Active objects must NOT be touched or destroyed by SetCapacity
            Assert.IsTrue(obj1 != null && obj1.activeSelf, "obj1 should remain active");
            Assert.IsTrue(obj2 != null && obj2.activeSelf, "obj2 should remain active");
            Assert.IsTrue(obj3 != null && obj3.activeSelf, "obj3 should remain active");
            Assert.IsTrue(obj4 != null && obj4.activeSelf, "obj4 should remain active");

            // 4. Despawn obj1 -> enters pool, pool is now full (1/1)
            obj1.Despawn();
            Assert.AreEqual(1, SmartPool.GetIdleCount(_prefab));

            // 5. Subsequent despawns (obj2, obj3, obj4) exceed capacity of 1, so they are destroyed
            obj2.Despawn();
            obj3.Despawn();
            obj4.Despawn();

            Assert.AreEqual(1, SmartPool.GetIdleCount(_prefab));
            Assert.IsFalse(obj2.activeSelf);
            Assert.IsFalse(obj3.activeSelf);
            Assert.IsFalse(obj4.activeSelf);

            // 6. Wait 1 frame for Unity's deferred Destroy execution
            yield return null;

            Assert.IsTrue(obj1 != null, "obj1 was preserved in pool");
            Assert.IsTrue(obj2 == null, "obj2 was destroyed on despawn");
            Assert.IsTrue(obj3 == null, "obj3 was destroyed on despawn");
            Assert.IsTrue(obj4 == null, "obj4 was destroyed on despawn");

            // 7. obj1 in pool can be re-spawned cleanly
            var respawned = _prefab.Spawn();
            Assert.AreSame(obj1, respawned);
            respawned.Despawn();
        }

        [Test]
        public void GetIdleCount_AccuratelyReflectsAvailableStackCount()
        {
            // 1. Before any pool interaction
            Assert.AreEqual(0, SmartPool.GetIdleCount(_prefab));
            Assert.AreEqual(-1, SmartPool.GetCapacity(_prefab));

            // Fluent extension parity check
            Assert.AreEqual(0, _prefab.GetPoolIdleCount());
            Assert.AreEqual(-1, _prefab.GetPoolCapacity());

            var componentPrefab = _prefab.GetComponent<CapacityTestPoolable>();
            Assert.AreEqual(0, SmartPool.GetIdleCount(componentPrefab));
            Assert.AreEqual(-1, SmartPool.GetCapacity(componentPrefab));
            Assert.AreEqual(0, componentPrefab.GetPoolIdleCount());
            Assert.AreEqual(-1, componentPrefab.GetPoolCapacity());

            // 2. Spawn 3 instances
            var a = _prefab.Spawn();
            var b = _prefab.Spawn();
            var c = _prefab.Spawn();
            Assert.AreEqual(0, SmartPool.GetIdleCount(_prefab));

            // 3. Despawn one by one
            a.Despawn();
            Assert.AreEqual(1, SmartPool.GetIdleCount(_prefab));
            Assert.AreEqual(1, _prefab.GetPoolIdleCount());

            b.Despawn();
            Assert.AreEqual(2, SmartPool.GetIdleCount(_prefab));

            c.Despawn();
            Assert.AreEqual(3, SmartPool.GetIdleCount(_prefab));

            // 4. Spawn one back
            var reused = _prefab.Spawn();
            Assert.AreEqual(2, SmartPool.GetIdleCount(_prefab));

            reused.Despawn();
            Assert.AreEqual(3, SmartPool.GetIdleCount(_prefab));

            // 5. Clear pool
            SmartPool.Clear(_prefab);
            Assert.AreEqual(0, SmartPool.GetIdleCount(_prefab));
        }

        private class CapacityTestPoolable : MonoBehaviour, IPoolable
        {
            public int OnSpawnCount { get; private set; }
            public int OnDespawnCount { get; private set; }

            public void OnSpawn()
            {
                OnSpawnCount++;
            }

            public void OnDespawn()
            {
                OnDespawnCount++;
            }
        }
    }
}
