# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-13

### Added
- Initial release of **SmartPool** for Unity.
- Fluent drop-in extension methods: `prefab.Spawn()`, `instance.Despawn()`, and generic `prefab.Spawn<T>()`.
- Zero-setup auto-created root manager (`[SmartPool_Root]`) with `DontDestroyOnLoad`.
- Zero-GC component caching (`PooledObject`) for physics, particle systems, trail renderers, and lifecycle hooks.
- Auto component reset for `Rigidbody`, `Rigidbody2D`, `TrailRenderer`, `ParticleSystem`, and `Transform` (supporting Unity 2021.3 LTS through Unity 6).
- Lifecycle hook interface `IPoolable` (`OnSpawn`, `OnDespawn`).
- Delayed auto-despawn chaining: `.DespawnAfter(delay)` with unscaled time support and safe cancellation.
- Completion action callbacks on `Despawn` and `DespawnAfter`.
- Stack-based ($O(1)$) internal pooling architecture.
- Prewarming API: `prefab.Prewarm(count, maxCapacity)` with capacity slot clamping.
- Pool capacity limiter with dynamic trimming: immediately destroys excess idle instances when capacity is reduced.
- Zero-cache mode (`MaxCapacity = 0`): strictly disables caching while maintaining full lifecycle contracts.
- Query APIs: `GetIdleCount()` and `GetCapacity()` on `SmartPool` and fluent prefab extensions.
- Safety fallback to `Destroy()` for non-pooled objects and double-despawn guards.
- Hierarchy visibility toggle via `SmartPool.ShowInHierarchy`.
- Scene unload handling: persistent pools by default, optional scene-local pools via `SetSceneLocal`, and global auto-clear via `SmartPool.AutoClearOnSceneChange`.
- Assembly Definition (`DZ.SmartPool.Runtime.asmdef`) for isolated and fast compilation.
