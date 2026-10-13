# SmartPool for Unity

A lightning-fast, zero-setup, zero-garbage-collection (Zero GC) object pooling solution for Unity.

Designed for maximum developer ergonomics: no tedious scene setup, no manual manager prefabs to drag into scenes, and zero boilerplate. Just drop it in and use fluent C# extension methods right from your prefabs and instances.

---

## ✨ Key Features

### 1. Core API & Ergonomics
- **Drop-in Extension Methods**:
  - `prefab.Spawn()` — seamless replacement for `Instantiate()`.
  - `instance.Despawn()` — seamless replacement for `Destroy()`.
- **Full Generic Support**:
  - `bulletPrefab.Spawn<Bullet>()` returns the `Bullet` component directly without type casting or manual `GetComponent` calls.
- **Flexible Overloads**:
  - `prefab.Spawn()`, `prefab.Spawn(position)`, `prefab.Spawn(position, rotation)`, and `prefab.Spawn(position, rotation, parent)`.
- **Zero-Setup Out-of-the-Box**:
  - No manager prefabs to drag into your scenes and no singleton scripts to wire up. The root manager `[SmartPool_Root]` is automatically created behind the scenes on first use with `DontDestroyOnLoad`.

### 2. Automatic Component Reset (Anti-State Leakage)
When an object is retrieved from the pool, SmartPool automatically restores its default state to prevent stale data bugs:
- **Physics Reset**: `Rigidbody` and `Rigidbody2D` linear/angular velocities are reset to zero (fully compatible from Unity 2021.3 LTS through Unity 6 without obsolete warnings).
- **Visual FX Reset**: `TrailRenderer` automatically calls `Clear()` to prevent trails from streaking across the screen from their previous positions. `ParticleSystem` automatically calls `Clear()` and `Play()`.
- **Transform Consistency**: Automatically restores `localScale` and rotation to the original prefab defaults.

### 3. Lifecycle Hooks & Event Callbacks
- **`IPoolable` Interface**:
  - `OnSpawn()`: Triggered immediately when retrieved from pool and activated (ideal for resetting health, re-initializing AI, etc.).
  - `OnDespawn()`: Triggered immediately before returning to the pool (ideal for detaching targets or unhooking events).
- **Inline Action Callbacks**:
  - Hook into despawn events directly without creating custom scripts or interfaces:
    ```csharp
    instance.Despawn(onComplete: () => { /* custom cleanup */ });
    ```

### 4. Smart Timers & Delayed Despawn
- **Fluent One-Liner Chaining**:
  - `prefab.Spawn(pos, rot).DespawnAfter(3.0f);`
- **Safe Timer Cancellation**:
  - If an object hits an obstacle or gets manually despawned before the countdown expires, the scheduled timer is automatically cancelled via generational tokens. No accidental double-despawn bugs.
- **Unscaled Time Support**:
  - Support for timers that ignore `Time.timeScale` (e.g., UI elements or pause menu effects):
    ```csharp
    effect.DespawnAfter(2.0f, useUnscaledTime: true);
    ```

### 5. High Performance, Memory & Prewarming
- **Zero GC Allocations**:
  - Components (`IPoolable`, `Rigidbody`, `TrailRenderer`, `ParticleSystem`) are cached once upon initial instantiation. No runtime `GetComponent` or LINQ allocations during gameplay.
- **Stack-Based Architecture ($O(1)$)**:
  - Internal `Stack<GameObject>` guarantees optimal push/pop performance.
- **Prewarming API**:
  - Instantiate objects ahead of time to avoid gameplay hitches:
    ```csharp
    bulletPrefab.Prewarm(100);
    ```
- **Pool Capacity Limiter (Anti-Memory Leak)**:
  - Cap maximum idle objects in the pool:
    ```csharp
    bulletPrefab.SetPoolCapacity(150);
    ```
  - When returning an object to a pool that is already at capacity, excess instances are destroyed cleanly instead of hoarding memory.
- **Pool Trimming**:
  - Clear a specific pool: `SmartPool.Clear(prefab);`
  - Clear all pools: `SmartPool.ClearAll();`

### 6. Safety & Hierarchy Organization
- **Crash-Proof Fallback**:
  - If `.Despawn()` is mistakenly called on an object created via regular `Instantiate()`, SmartPool gracefully falls back to `Destroy()` without throwing errors.
- **Double-Despawn Guard**:
  - Prevents returning an already-despawned object back into the pool.
- **Clean Hierarchy by Default**:
  - Inactive objects are neatly organized under `[SmartPool_Root] -> [Pool] <PrefabName>`.
- **Hierarchy Visibility Toggle**:
  - Hide or show pool containers in the Unity Hierarchy window with a single line of code:
    ```csharp
    SmartPool.ShowInHierarchy = false; // Uses HideFlags.HideInHierarchy
    ```

### 7. Scene Unload Policies
- **Persistent by Default**:
  - Pools persist across scene loads (`DontDestroyOnLoad`).
- **Scene-Local Pools**:
  - Automatically clear specific pools when unloading a level:
    ```csharp
    levelBossMinionPrefab.SetSceneLocal(true);
    ```
- **Global Scene Change Cleanup**:
  - Clear all pools on scene change:
    ```csharp
    SmartPool.AutoClearOnSceneChange = true;
    ```

---

## 🚀 Quick Start & Usage Examples

### 1. Spawning Objects
```csharp
using UnityEngine;
using DZ.SmartPool;

public class Gun : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Bullet bulletComponentPrefab;
    [SerializeField] private Transform firePoint;

    public void Shoot()
    {
        // Basic spawn
        GameObject bullet1 = bulletPrefab.Spawn();

        // Spawn with position & rotation
        GameObject bullet2 = bulletPrefab.Spawn(firePoint.position, firePoint.rotation);

        // Generic spawn returning the exact Component type
        Bullet bullet3 = bulletPrefab.Spawn<Bullet>(firePoint.position, firePoint.rotation);

        // Spawn directly from a Component prefab reference
        Bullet bullet4 = bulletComponentPrefab.Spawn(firePoint.position, firePoint.rotation);
    }
}
```

### 2. Despawning & Chaining
```csharp
// Instant manual despawn
bullet.Despawn();

// Fluent delayed auto-despawn (1 line)
bulletPrefab
    .Spawn(firePoint.position, firePoint.rotation)
    .DespawnAfter(3.0f);

// Delayed despawn with completion callback & unscaled time
bulletComponentPrefab
    .Spawn(firePoint.position, firePoint.rotation)
    .DespawnAfter(3.0f, useUnscaledTime: true, onComplete: () =>
    {
        Debug.Log("Bullet expired and returned to pool!");
    });
```

### 3. Implementing Lifecycle Hooks (`IPoolable`)
```csharp
using UnityEngine;
using DZ.SmartPool;

public class Bullet : MonoBehaviour, IPoolable
{
    private float _speed = 25f;

    public void OnSpawn()
    {
        // Called whenever the object is fetched from the pool and enabled
        Debug.Log("Bullet spawned and initialized!");
    }

    public void OnDespawn()
    {
        // Called right before returning to the pool and being disabled
        Debug.Log("Bullet cleaned up!");
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * (_speed * Time.deltaTime));
    }
}
```

### 4. Prewarming & Pool Management
```csharp
void Start()
{
    // Prewarm 100 instances ahead of time, capping max idle capacity at 150
    bulletPrefab.Prewarm(100, maxCapacity: 150);

    // Optional: Keep Unity hierarchy clean by hiding pool roots
    SmartPool.ShowInHierarchy = false;
}

void OnDestroy()
{
    // Clean up memory when level finishes
    SmartPool.Clear(bulletPrefab);
}
```

---

## 📦 Compatibility
- **Unity Versions**: Compatible with Unity 2021.3 LTS, 2022 LTS, 2023, and Unity 6 (6000.x+).
- **Physics APIs**: Clean cross-version compatibility for `velocity` and `linearVelocity`.
- **Platforms**: Supports all Unity platforms (PC, Mac, Linux, iOS, Android, WebGL, Consoles).

## 📄 License
This project is licensed under the MIT License — see the [LICENSE.md](LICENSE.md) file for details.
