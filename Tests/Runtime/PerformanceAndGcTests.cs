using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DZ.SmartPool;
using Debug = UnityEngine.Debug;

namespace DZ.SmartPool.Tests
{
    public class PerformanceAndGcTests
    {
        private GameObject _simplePrefab;
        private GameObject _complexPrefab;

        [SetUp]
        public void Setup()
        {
            // Simple prefab with IPoolable
            _simplePrefab = new GameObject("Test_BenchmarkSimple");
            _simplePrefab.AddComponent<SimpleTestPoolable>();
            _simplePrefab.SetActive(false);

            // Complex prefab with physics, trail, and IPoolable
            _complexPrefab = new GameObject("Test_BenchmarkComplex");
            _complexPrefab.AddComponent<Rigidbody>();
            _complexPrefab.AddComponent<TrailRenderer>();
            _complexPrefab.AddComponent<ComplexTestPoolable>();
            _complexPrefab.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            SmartPool.ClearAll();
            if (_simplePrefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_simplePrefab);
            }
            if (_complexPrefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_complexPrefab);
            }
        }

        [Test]
        public void GC_SteadyStateReuse_AllocatesZeroBytes()
        {
            const int poolSize = 50;

            // 1. Prewarm pool
            SmartPool.Prewarm(_simplePrefab, poolSize);
            Assert.AreEqual(poolSize, SmartPool.GetIdleCount(_simplePrefab));

            // 2. Warm-up pass to ensure JIT compilation / static initialization is complete
            for (int i = 0; i < 5; i++)
            {
                var temp = _simplePrefab.Spawn();
                temp.Despawn();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 3. Measure GC allocations during 50 steady-state spawn & despawn cycles
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < poolSize; i++)
            {
                var instance = _simplePrefab.Spawn();
                instance.Despawn();
            }

            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
            long diff = allocAfter - allocBefore;

            Debug.Log($"[GC Verification] {poolSize} steady-state Spawn/Despawn cycles allocated: {diff} bytes GC.");

            // 4. Assert strictly zero managed GC allocations during steady state reuse
            Assert.AreEqual(0, diff, $"Steady-state reuse must allocate exactly 0 bytes of managed GC, but allocated {diff} bytes.");
        }

        [Test]
        public void GC_CategorizedLifecycle_SeparatesPrewarmFromReuse()
        {
            const int count = 20;

            // 1. Measure initial prewarm (cold instantiation naturally allocates Unity native & managed memory)
            GC.Collect();
            long allocBeforePrewarm = GC.GetAllocatedBytesForCurrentThread();
            SmartPool.Prewarm(_complexPrefab, count);
            long prewarmBytes = GC.GetAllocatedBytesForCurrentThread() - allocBeforePrewarm;

            Debug.Log($"[GC Categorization] Prewarm ({count} complex instances): {prewarmBytes:N0} bytes allocated.");
            Assert.IsTrue(prewarmBytes > 0, "Prewarming must allocate memory for initial GameObject instantiation.");

            // 2. Warm-up pass
            var warm = _complexPrefab.Spawn();
            warm.Despawn();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 3. Measure steady-state reuse (must be 0 bytes)
            long allocBeforeReuse = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < count; i++)
            {
                var instance = _complexPrefab.Spawn();
                instance.Despawn();
            }
            long reuseBytes = GC.GetAllocatedBytesForCurrentThread() - allocBeforeReuse;

            Debug.Log($"[GC Categorization] Steady-state reuse ({count} complex instances): {reuseBytes} bytes allocated.");
            Assert.AreEqual(0, reuseBytes, $"Steady-state reuse must produce 0 bytes GC, but allocated {reuseBytes} bytes.");

            // 4. Measure user callback with closure (proves closure allocations are caller-owned)
            int callerCapturedVariable = 0;
            long allocBeforeClosure = GC.GetAllocatedBytesForCurrentThread();
            var closureObj = _complexPrefab.Spawn();
            closureObj.Despawn(() =>
            {
                callerCapturedVariable++;
            });
            long closureBytes = GC.GetAllocatedBytesForCurrentThread() - allocBeforeClosure;

            Debug.Log($"[GC Categorization] User lambda closure despawn: {closureBytes} bytes allocated (caller-owned closure state).");
            Assert.AreEqual(1, callerCapturedVariable);
        }

        [Test]
        public void Benchmark_ExecutionTime_Comparison_100_Objects()
        {
            RunBenchmarkComparison(100);
        }

        [Test]
        public void Benchmark_ExecutionTime_Comparison_500_Objects()
        {
            RunBenchmarkComparison(500);
        }

        [Test]
        public void Benchmark_ExecutionTime_Comparison_1000_Objects()
        {
            RunBenchmarkComparison(1000);
        }

        [Test]
        public void Benchmark_ComplexPrefab_PhysicsAndVFX_Comparison()
        {
            const int count = 200;

            // 1. Standard Instantiate + Destroy for Complex Prefab
            var swStandard = Stopwatch.StartNew();
            long gcBeforeStandard = GC.GetAllocatedBytesForCurrentThread();
            var stdList = new List<GameObject>(count);
            for (int i = 0; i < count; i++)
            {
                stdList.Add(UnityEngine.Object.Instantiate(_complexPrefab));
            }
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Object.DestroyImmediate(stdList[i]);
            }
            swStandard.Stop();
            long gcStandard = GC.GetAllocatedBytesForCurrentThread() - gcBeforeStandard;

            // 2. SmartPool Prewarm + Steady-State Reuse for Complex Prefab
            SmartPool.Prewarm(_complexPrefab, count);

            // Warm-up pass
            var warm = _complexPrefab.Spawn();
            warm.Despawn();

            var swPool = Stopwatch.StartNew();
            long gcBeforePool = GC.GetAllocatedBytesForCurrentThread();
            var poolList = new List<GameObject>(count);
            for (int i = 0; i < count; i++)
            {
                poolList.Add(_complexPrefab.Spawn());
            }
            for (int i = 0; i < count; i++)
            {
                poolList[i].Despawn();
            }
            swPool.Stop();
            long gcPool = GC.GetAllocatedBytesForCurrentThread() - gcBeforePool;

            float speedup = (float)swStandard.Elapsed.TotalMilliseconds / Mathf.Max(0.0001f, (float)swPool.Elapsed.TotalMilliseconds);

            Debug.Log($"========================================================================================\n" +
                      $"[Benchmark Complex Prefab: {count} Objects with Rigidbody, TrailRenderer, IPoolable]\n" +
                      $" - Standard Instantiate/Destroy : {swStandard.Elapsed.TotalMilliseconds:F3} ms | GC: {gcStandard:N0} bytes\n" +
                      $" - SmartPool Spawn/Despawn      : {swPool.Elapsed.TotalMilliseconds:F3} ms | GC: {gcPool:N0} bytes\n" +
                      $" - Performance Gain             : {speedup:F1}x FASTER\n" +
                      $"========================================================================================");

            Assert.IsTrue(swPool.ElapsedTicks < swStandard.ElapsedTicks, "SmartPool reuse must be faster than standard Instantiate/Destroy.");
            Assert.AreEqual(0, gcPool, "SmartPool steady-state reuse must allocate 0 bytes GC.");
        }

        private void RunBenchmarkComparison(int count)
        {
            // 1. Benchmark Standard Instantiate / Destroy
            var swStandard = Stopwatch.StartNew();
            long gcBeforeStandard = GC.GetAllocatedBytesForCurrentThread();
            var stdInstances = new List<GameObject>(count);
            for (int i = 0; i < count; i++)
            {
                stdInstances.Add(UnityEngine.Object.Instantiate(_simplePrefab));
            }
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Object.DestroyImmediate(stdInstances[i]);
            }
            swStandard.Stop();
            long gcStandard = GC.GetAllocatedBytesForCurrentThread() - gcBeforeStandard;

            // 2. Benchmark SmartPool (Prewarmed Steady-State)
            SmartPool.Prewarm(_simplePrefab, count);

            // JIT warm-up
            var warm = _simplePrefab.Spawn();
            warm.Despawn();

            var swPool = Stopwatch.StartNew();
            long gcBeforePool = GC.GetAllocatedBytesForCurrentThread();
            var poolInstances = new List<GameObject>(count);
            for (int i = 0; i < count; i++)
            {
                poolInstances.Add(_simplePrefab.Spawn());
            }
            for (int i = 0; i < count; i++)
            {
                poolInstances[i].Despawn();
            }
            swPool.Stop();
            long gcPool = GC.GetAllocatedBytesForCurrentThread() - gcBeforePool;

            float speedup = (float)swStandard.Elapsed.TotalMilliseconds / Mathf.Max(0.0001f, (float)swPool.Elapsed.TotalMilliseconds);

            Debug.Log($"========================================================================================\n" +
                      $"[Benchmark Load: {count} Objects]\n" +
                      $" - Standard Instantiate/Destroy : {swStandard.Elapsed.TotalMilliseconds:F3} ms | GC: {gcStandard:N0} bytes\n" +
                      $" - SmartPool Spawn/Despawn      : {swPool.Elapsed.TotalMilliseconds:F3} ms | GC: {gcPool:N0} bytes\n" +
                      $" - Performance Gain             : {speedup:F1}x FASTER\n" +
                      $"========================================================================================");

            Assert.IsTrue(swPool.ElapsedTicks < swStandard.ElapsedTicks, $"SmartPool should be faster than Instantiate/Destroy for {count} objects.");
            Assert.AreEqual(0, gcPool, $"SmartPool steady-state reuse must allocate 0 bytes GC for {count} objects.");
        }

        private class SimpleTestPoolable : MonoBehaviour, IPoolable
        {
            public int SpawnCount { get; private set; }
            public int DespawnCount { get; private set; }

            public void OnSpawn()
            {
                SpawnCount++;
            }

            public void OnDespawn()
            {
                DespawnCount++;
            }
        }

        private class ComplexTestPoolable : MonoBehaviour, IPoolable
        {
            public Vector3 Velocity { get; set; }

            public void OnSpawn()
            {
                Velocity = Vector3.forward * 10f;
            }

            public void OnDespawn()
            {
                Velocity = Vector3.zero;
            }
        }
    }
}
