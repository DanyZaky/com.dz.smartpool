# SmartPool for Unity

Zero-setup, zero-garbage-collection (Zero GC), fluent extension-based object pooling for Unity.
Dirancang dengan ergonomi dan filosofi ala **DOTween** — memangkas boilerplate, tanpa setup manager manual di scene, dan berkinerja tinggi ($O(1)$ stack performance).

---

## ✨ Fitur Lengkap

### 1. Core API & Ergonomi (Gaya DOTween)
- **Extension Methods Langsung**:
  - `prefab.Spawn()` pengganti `Instantiate()`.
  - `instance.Despawn()` pengganti `Destroy()`.
- **Generics Support Penuh**:
  - `bulletPrefab.Spawn<Bullet>()` langsung mengembalikan tipe `Bullet` (tanpa `GetComponent` manual atau casting).
- **Overload Fleksibel**:
  - `prefab.Spawn()`, `prefab.Spawn(position)`, `prefab.Spawn(position, rotation)`, `prefab.Spawn(position, rotation, parent)`.
- **Zero-Setup Initialization**:
  - Tidak perlu pasang prefab manager di scene atau setup manual. Root manager `[SmartPool_Root]` dibuat otomatis di background dengan status `DontDestroyOnLoad`.

### 2. Auto Component Reset (Anti-State Leakage)
Saat objek diambil dari pool, sistem otomatis mengembalikan state komponen ke kondisi bersih:
- **Physics Reset**: `Rigidbody` & `Rigidbody2D` velocity dan angular velocity di-reset ke nol (kompatibel penuh Unity 2021.3 LTS s/d Unity 6).
- **Visual FX Reset**: `TrailRenderer` otomatis memanggil `Clear()` (mencegah efek trail menyilang layar dari posisi lama). `ParticleSystem` otomatis `Clear()` dan `Play()`.
- **Transform Consistency**: `localScale` dan rotasi dipastikan kembali ke nilai default bawaan prefab.

### 3. Lifecycle Hooks & Event Callbacks
- **Interface `IPoolable`**:
  - `OnSpawn()`: Dipanggil tepat setelah objek keluar dari pool dan aktif.
  - `OnDespawn()`: Dipanggil sebelum objek dinonaktifkan dan kembali ke pool.
- **Event Action Callback**:
  - Callback instan saat despawn tanpa perlu interface:
    `instance.Despawn(onComplete: () => { ... });`
    `instance.DespawnAfter(3.0f, onComplete: () => { ... });`

### 4. Smart Timers & Delayed Despawn
- **One-Liner Auto-Despawn**:
  - `prefab.Spawn(pos, rot).DespawnAfter(3.0f);`
- **Safe Timer Cancellation**:
  - Jika objek terkena peluru/rintangan sebelum durasi timer habis dan dipanggil `.Despawn()` manual, timer otomatis dibatalkan via generational token sehingga **tidak akan terjadi double-despawn**.
- **Support Unscaled Time**:
  - Opsi timer yang kebal terhadap `Time.timeScale` (misal untuk UI/menu jeda):
    `obj.DespawnAfter(3.0f, useUnscaledTime: true);`

### 5. Performance, Memory & Prewarming
- **Prewarm API**:
  - `bulletPrefab.Prewarm(100);` atau dengan batas kapasitas: `bulletPrefab.Prewarm(100, maxCapacity: 200);`
- **Stack-Based Architecture ($O(1)$ Zero-GC)**:
  - Menggunakan `Stack<GameObject>` internal. Semua komponen dicache saat instansiasi pertama, bebas GC allocations saat gameplay.
- **Pool Capacity Limiter (Anti-Memory Leak)**:
  - Batasi kapasitas maksimal per prefab: `bulletPrefab.SetPoolCapacity(200);`
  - Jika objek yang di-despawn melebihi batas kapasitas, objek ekstra otomatis di-`Destroy` biasa agar memori RAM tidak bengkak.
- **Pool Trimming**:
  - Bersihkan pool tertentu: `SmartPool.Clear(prefab);`
  - Bersihkan semua pool: `SmartPool.ClearAll();`

### 6. Safety & Debugging
- **Fallback Safety (Anti-Crash)**:
  - Jika user memanggil `.Despawn()` pada objek yang tidak sengaja dibuat lewat `Instantiate()` biasa, sistem otomatis melakukan fallback ke `Destroy()` biasa tanpa error.
- **Double-Despawn Guard**:
  - Mencegah objek dimasukkan ke pool dua kali secara tidak sengaja.
- **Editor Hierarchy Organization**:
  - Semua objek nonaktif diparkir rapi di bawah container `[SmartPool_Root]` -> `[Pool] <PrefabName>`.
  - **Hierarchy Visibility Toggle**:
    - Sembunyikan/tampilkan container di jendela Hierarchy Unity kapan saja:
      `SmartPool.ShowInHierarchy = false; // Menggunakan HideFlags.HideInHierarchy`

### 7. Modern Unity Integration
- **Assembly Definition (`DZ.SmartPool.Runtime.asmdef`)**:
  - Terisolasi rapi, tidak memperlambat kompilasi kode game utama.
- **Scene Unload Handling**:
  - Default: Pool persisten antar-scene (`DontDestroyOnLoad`).
  - Opsi global auto-clear saat ganti scene: `SmartPool.AutoClearOnSceneChange = true;`
  - Atau opsi per-prefab: `bulletPrefab.SetSceneLocal(true);`

---

## 🚀 Contoh Penggunaan

```csharp
using UnityEngine;
using DZ.SmartPool;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private Transform firePoint;

    private void Awake()
    {
        // 1. Prewarm pool di awal game dengan limit kapasitas
        bulletPrefab.Prewarm(50, maxCapacity: 100);

        // 2. Opsional: Sembunyikan container pool dari Hierarchy jika ingin tampilan bersih
        // SmartPool.ShowInHierarchy = false;
    }

    public void Shoot()
    {
        // 3. Spawn fluent chaining dengan auto-despawn & callback
        bulletPrefab
            .Spawn(firePoint.position, firePoint.rotation)
            .DespawnAfter(3.0f, onComplete: () =>
            {
                Debug.Log("Peluru otomatis habis masa aktifnya.");
            });
    }

    public void OnEnemyHit(Bullet bullet)
    {
        // 4. Despawn manual instan (timer DespawnAfter otomatis batal aman)
        bullet.Despawn(onComplete: () =>
        {
            Debug.Log("Peluru mengenai musuh!");
        });
    }
}
```

---

## 📦 Kompatibilitas
- Unity 2021.3 LTS ke atas (termasuk Unity 2022, Unity 2023, dan Unity 6).
- Bebas compiler warning (`linearVelocity` vs `velocity`).
