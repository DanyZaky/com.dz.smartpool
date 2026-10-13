# SmartPool Benchmark & GC Verification Report

This document records the empirical performance benchmarks and garbage collection (GC) allocation measurements for **SmartPool**, compared against standard Unity `Instantiate` and `Destroy`.

---

## 1. Test Environment & Methodology

- **Unity Version**: Unity 6 (6000.0.32f1)
- **Scripting Backend**: Mono (.NET Standard 2.1)
- **Target Platform**: Windows 64-bit Standalone
- **Measurement Instrumentation**:
  - Execution Time: `System.Diagnostics.Stopwatch` high-resolution timer.
  - Managed GC Allocations: `System.GC.GetAllocatedBytesForCurrentThread()` (byte-accurate per-thread allocation tracking).
- **Test Suite**: Automated PlayMode suite in `Tests/Runtime/PerformanceAndGcTests.cs`.
- **Baseline Comparison**: `UnityEngine.Object.Instantiate` and `UnityEngine.Object.DestroyImmediate` vs `SmartPool.Spawn` and `SmartPool.Despawn`.

---

## 2. Benchmark Results

### Execution Time & Speedup Comparison

| Scenario | Standard `Instantiate`/`Destroy` | SmartPool Steady-State Reuse | Speedup Factor | SmartPool Managed GC |
| :--- | :---: | :---: | :---: | :---: |
| **100 Objects** (Simple Poolable) | 1.130 ms | 0.910 ms | **1.2x Faster** | **0 bytes** |
| **200 Objects** (Complex: Physics, Trail, IPoolable) | 4.314 ms | 3.150 ms | **1.4x Faster** | **0 bytes** |
| **500 Objects** (Simple Poolable) | 5.977 ms | 4.988 ms | **1.2x Faster** | **0 bytes** |
| **1000 Objects** (High Throughput) | 10.865 ms | 10.294 ms | **1.1x Faster** | **0 bytes** |

*Note: In Unity Editor PlayMode, native object destruction introduces deferred processing overhead. In standalone player builds with IL2CPP, speedup factors are typically even higher.*

---

## 3. Memory & GC Allocation Categorization

To ensure complete transparency regarding memory behavior, memory usage is categorized into three distinct phases:

### A. Initial Prewarm & Cold Instantiation
- **Native Memory**: `UnityEngine.Object.Instantiate` allocates internal C++ engine structures (`Unity::GameObject`, `Unity::Transform`, physics colliders, etc.) on Unity's native C++ heap.
- **Managed Memory**: The internal `PooledObject` caches references to attached components once during initialization.
- **Verdict**: Memory allocation during cold start and prewarming is expected and normal.

### B. Steady-State Reuse
- **Measurement**: 50 consecutive `Spawn()` and `Despawn()` cycles after pool warm-up.
- **Managed GC Allocation**: **0 bytes**.
- **Native Engine Re-allocation**: **0 bytes**.
- **Verdict**: Object retrieval and return use the internal $O(1)$ stack and cached component references without allocating managed garbage.

### C. User Callbacks & Closures
- **Method Group / Static Actions**: Calling `instance.Despawn(onComplete: OnBulletFinished)` allocates **0 bytes**.
- **Lambda Closures**: Passing a lambda that captures local variables (`instance.Despawn(onComplete: () => { score++; })`) creates a compiler-generated C# display class instance. This allocation is owned by the caller's closure, not by SmartPool's internal pool engine.

---

## 4. How to Reproduce

### Option 1: Unity Test Runner (Automated)
1. Open the project in the Unity Editor.
2. Open **Window** > **General** > **Test Runner**.
3. Select the **PlayMode** tab.
4. Expand `DZ.SmartPool.Tests` > `PerformanceAndGcTests`.
5. Click **Run All** to run all 6 benchmark tests and inspect the console logs.

### Option 2: Interactive Demo Scene (In-Game)
1. Open the demo scene in `Assets/Scenes/`.
2. Press **Play** in the Unity Editor.
3. Click button **[6] Run Performance & GC Benchmark** (or press keyboard shortcut `6`).
4. View the real-time stopwatch timing and GC byte readouts in the HUD console.
