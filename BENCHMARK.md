# SmartPool Benchmark & GC Verification Report

This document records the empirical performance benchmarks and garbage collection (GC) allocation measurements for **SmartPool**, compared against standard Unity `Instantiate` and `Destroy`.

---

## 1. Test Environment & Methodology

- **Unity Version**: Unity 6 (6000.0.32f1)
- **Scripting Backend**: Mono (.NET Standard 2.1)
- **Target Platform**: Windows 64-bit Standalone
- **Measurement Instrumentation**:
  - Execution Time: `System.Diagnostics.Stopwatch` high-resolution timer.
  - Managed GC Allocations: `System.GC.GetAllocatedBytesForCurrentThread()` (tracks byte-accurate managed C# heap allocations on the executing thread).
- **Test Suite**: Automated PlayMode suite in `Tests/Runtime/PerformanceAndGcTests.cs`.
- **Baseline Comparison**: `UnityEngine.Object.Instantiate` and `UnityEngine.Object.DestroyImmediate` vs `SmartPool.Spawn` and `SmartPool.Despawn`.

### Methodology & Cost Comparison Notes
The two approaches evaluated represent distinct architectural trade-offs:
- **Standard Lifecycle (`Instantiate` / `DestroyImmediate`)**:
  - Performs full native C++ object instantiation, component graph construction, hierarchy binding, and `Awake`/`OnEnable` callbacks on creation.
  - Destruction in this benchmark uses `DestroyImmediate()` to enforce complete synchronous cleanup within a single test iteration. In real gameplay, standard `Destroy()` defers cleanup to the end of the frame, which defers CPU costs but accumulates native and managed deallocation overhead over time.
- **SmartPool Lifecycle (`Spawn` / `Despawn`)**:
  - Prewarmed instances are preserved in memory inside an $O(1)$ stack.
  - Retrieval pops an instance, reparents, updates position and rotation, resets cached components (`Rigidbody` linear/angular velocities, `TrailRenderer.Clear()`, `ParticleSystem.Clear()`), fires `IPoolable.OnSpawn()`, and activates the object (`SetActive(true)`).
  - Despawn fires `IPoolable.OnDespawn()`, deactivates the object, reparents under the pool container, and pushes it back into the stack.
- **Architectural Trade-Off**:
  - SmartPool trades a persistent memory footprint (holding idle objects in pool capacity) in exchange for zero runtime instantiate/destroy hitches and zero managed GC churn.
  - The standard approach releases memory when objects are destroyed, but introduces CPU spikes, allocation churn, and potential GC collection pauses during active gameplay.

---

## 2. Benchmark Results

### Execution Time & Speedup Comparison

| Scenario | Standard `Instantiate`/`DestroyImmediate` | SmartPool Steady-State Reuse | Speedup Factor | SmartPool Managed GC (`GC.Alloc`) |
| :--- | :---: | :---: | :---: | :---: |
| **100 Objects** (Simple Poolable) | 1.130 ms | 0.910 ms | **1.2x Faster** | **0 bytes** |
| **200 Objects** (Complex: Physics, Trail, IPoolable) | 4.314 ms | 3.150 ms | **1.4x Faster** | **0 bytes** |
| **500 Objects** (Simple Poolable) | 5.977 ms | 4.988 ms | **1.2x Faster** | **0 bytes** |
| **1000 Objects** (High Throughput) | 10.865 ms | 10.294 ms | **1.1x Faster** | **0 bytes** |

*Note: Results reflect the tested machine and configuration in Unity 6 Editor PlayMode. Standalone player builds with IL2CPP and burst optimizations may exhibit different timings.*

---

## 3. Memory & GC Allocation Categorization

To maintain technical accuracy, memory measurements are categorized specifically between managed C# heap allocations and native engine behavior:

### A. Initial Prewarm & Cold Instantiation
- **Native Engine Memory**: `UnityEngine.Object.Instantiate` allocates internal C++ engine structures (`Unity::GameObject`, `Unity::Transform`, native physics and rendering representations) on Unity's native C++ heap.
- **Managed Memory**: The internal `PooledObject` component caches references to attached components during first-time initialization.
- **Verdict**: Memory allocation during cold start and prewarming is expected and normal.

### B. Steady-State Reuse
- **Measurement**: 50 consecutive `Spawn()` and `Despawn()` cycles after pool warm-up, tracked via `System.GC.GetAllocatedBytesForCurrentThread()`.
- **Managed GC Allocation (`GC.Alloc`)**: **0 bytes**.
- **Native Engine Behavior**: Because objects are deactivated and reparented rather than created or destroyed, no calls to native `Instantiate` or `Destroy` occur during steady-state reuse. (Note: `GC.GetAllocatedBytesForCurrentThread()` strictly measures managed C# heap allocations; native memory tracking requires Unity Memory Profiler snapshots).
- **Verdict**: Object retrieval and return operate with zero managed garbage collection allocations during gameplay steady state.

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
