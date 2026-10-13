using System;
using UnityEngine;

namespace DZ.SmartPool
{
    /// <summary>
    /// Fluent extension methods for GameObject and Component pooling with zero-setup ergonomics.
    /// Provides zero-boilerplate APIs for Spawn, Despawn, DespawnAfter, Prewarm, and Capacity settings.
    /// </summary>
    public static class SmartPoolExtensions
    {
        #region GameObject Spawn Extensions

        /// <summary>
        /// Spawns an instance of this prefab using its default position and rotation.
        /// </summary>
        public static GameObject Spawn(this GameObject prefab)
        {
            return SmartPool.Spawn(prefab, prefab.transform.position, prefab.transform.rotation, null);
        }

        /// <summary>
        /// Spawns an instance of this prefab at the specified position.
        /// </summary>
        public static GameObject Spawn(this GameObject prefab, Vector3 position)
        {
            return SmartPool.Spawn(prefab, position, prefab.transform.rotation, null);
        }

        /// <summary>
        /// Spawns an instance of this prefab at the specified position and rotation.
        /// </summary>
        public static GameObject Spawn(this GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return SmartPool.Spawn(prefab, position, rotation, null);
        }

        /// <summary>
        /// Spawns an instance of this prefab at the specified position, rotation, and parent transform.
        /// </summary>
        public static GameObject Spawn(this GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            return SmartPool.Spawn(prefab, position, rotation, parent);
        }

        /// <summary>
        /// Spawns an instance of this prefab attached to the specified parent.
        /// </summary>
        public static GameObject Spawn(this GameObject prefab, Transform parent)
        {
            return SmartPool.Spawn(prefab, prefab.transform.position, prefab.transform.rotation, parent);
        }

        /// <summary>
        /// Spawns an instance of this prefab and returns the specified component type directly.
        /// </summary>
        public static T Spawn<T>(this GameObject prefab) where T : Component
        {
            return SmartPool.Spawn<T>(prefab, prefab.transform.position, prefab.transform.rotation, null);
        }

        /// <summary>
        /// Spawns an instance of this prefab at the specified position and returns the component.
        /// </summary>
        public static T Spawn<T>(this GameObject prefab, Vector3 position) where T : Component
        {
            return SmartPool.Spawn<T>(prefab, position, prefab.transform.rotation, null);
        }

        /// <summary>
        /// Spawns an instance of this prefab at the specified position and rotation, and returns the component.
        /// </summary>
        public static T Spawn<T>(this GameObject prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            return SmartPool.Spawn<T>(prefab, position, rotation, null);
        }

        /// <summary>
        /// Spawns an instance of this prefab at the specified position, rotation, parent, and returns the component.
        /// </summary>
        public static T Spawn<T>(this GameObject prefab, Vector3 position, Quaternion rotation, Transform parent) where T : Component
        {
            return SmartPool.Spawn<T>(prefab, position, rotation, parent);
        }

        /// <summary>
        /// Spawns an instance of this prefab attached to parent and returns the component.
        /// </summary>
        public static T Spawn<T>(this GameObject prefab, Transform parent) where T : Component
        {
            return SmartPool.Spawn<T>(prefab, prefab.transform.position, prefab.transform.rotation, parent);
        }

        #endregion

        #region Component Spawn Extensions

        /// <summary>
        /// Spawns an instance from a component prefab using its default position and rotation.
        /// </summary>
        public static T Spawn<T>(this T prefab) where T : Component
        {
            return SmartPool.Spawn<T>(prefab.gameObject, prefab.transform.position, prefab.transform.rotation, null);
        }

        /// <summary>
        /// Spawns an instance from a component prefab at the specified position.
        /// </summary>
        public static T Spawn<T>(this T prefab, Vector3 position) where T : Component
        {
            return SmartPool.Spawn<T>(prefab.gameObject, position, prefab.transform.rotation, null);
        }

        /// <summary>
        /// Spawns an instance from a component prefab at the specified position and rotation.
        /// </summary>
        public static T Spawn<T>(this T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            return SmartPool.Spawn<T>(prefab.gameObject, position, rotation, null);
        }

        /// <summary>
        /// Spawns an instance from a component prefab at the specified position, rotation, and parent.
        /// </summary>
        public static T Spawn<T>(this T prefab, Vector3 position, Quaternion rotation, Transform parent) where T : Component
        {
            return SmartPool.Spawn<T>(prefab.gameObject, position, rotation, parent);
        }

        /// <summary>
        /// Spawns an instance from a component prefab attached to parent.
        /// </summary>
        public static T Spawn<T>(this T prefab, Transform parent) where T : Component
        {
            return SmartPool.Spawn<T>(prefab.gameObject, prefab.transform.position, prefab.transform.rotation, parent);
        }

        #endregion

        #region Despawn & Chaining Extensions

        /// <summary>
        /// Despawns this GameObject back into its pool.
        /// Supports an optional callback invoked immediately before deactivation.
        /// Fallback safety: If created via standard Instantiate, falls back to Destroy().
        /// Double-despawn guard: Safely ignores if already returned to pool.
        /// </summary>
        public static void Despawn(this GameObject instance, Action onComplete = null)
        {
            SmartPool.Despawn(instance, onComplete);
        }

        /// <summary>
        /// Despawns this component's GameObject back into its pool.
        /// Supports an optional callback invoked immediately before deactivation.
        /// Fallback safety: If created via standard Instantiate, falls back to Destroy().
        /// </summary>
        public static void Despawn(this Component component, Action onComplete = null)
        {
            SmartPool.Despawn(component, onComplete);
        }

        /// <summary>
        /// Schedules auto-despawn after the specified delay in seconds.
        /// Supports unscaled time and completion callbacks.
        /// Returns this GameObject for fluent chaining.
        /// </summary>
        public static GameObject DespawnAfter(this GameObject instance, float delay, bool useUnscaledTime = false, Action onComplete = null)
        {
            SmartPool.DespawnAfter(instance, delay, useUnscaledTime, onComplete);
            return instance;
        }

        /// <summary>
        /// Schedules auto-despawn after the specified delay in seconds with a completion callback.
        /// Returns this GameObject for fluent chaining.
        /// </summary>
        public static GameObject DespawnAfter(this GameObject instance, float delay, Action onComplete)
        {
            SmartPool.DespawnAfter(instance, delay, false, onComplete);
            return instance;
        }

        /// <summary>
        /// Schedules auto-despawn after the specified delay in seconds.
        /// Supports unscaled time and completion callbacks.
        /// Returns this component for fluent chaining.
        /// </summary>
        public static T DespawnAfter<T>(this T component, float delay, bool useUnscaledTime = false, Action onComplete = null) where T : Component
        {
            SmartPool.DespawnAfter(component, delay, useUnscaledTime, onComplete);
            return component;
        }

        /// <summary>
        /// Schedules auto-despawn after the specified delay in seconds with a completion callback.
        /// Returns this component for fluent chaining.
        /// </summary>
        public static T DespawnAfter<T>(this T component, float delay, Action onComplete) where T : Component
        {
            SmartPool.DespawnAfter(component, delay, false, onComplete);
            return component;
        }

        #endregion

        #region Prewarm & Configuration Extensions

        /// <summary>
        /// Prewarms the pool for this GameObject prefab by creating the given count ahead of time.
        /// Optionally sets the maximum pool capacity.
        /// Returns the prefab for fluent chaining.
        /// </summary>
        public static GameObject Prewarm(this GameObject prefab, int count, int maxCapacity = -1)
        {
            SmartPool.Prewarm(prefab, count, maxCapacity);
            return prefab;
        }

        /// <summary>
        /// Prewarms the pool for this component prefab by creating the given count ahead of time.
        /// Optionally sets the maximum pool capacity.
        /// Returns the prefab for fluent chaining.
        /// </summary>
        public static T Prewarm<T>(this T prefab, int count, int maxCapacity = -1) where T : Component
        {
            SmartPool.Prewarm(prefab, count, maxCapacity);
            return prefab;
        }

        /// <summary>
        /// Sets maximum capacity for this prefab's pool. Extra objects beyond this limit are destroyed when despawned.
        /// Returns the prefab for fluent chaining.
        /// </summary>
        public static GameObject SetPoolCapacity(this GameObject prefab, int maxCapacity)
        {
            SmartPool.SetCapacity(prefab, maxCapacity);
            return prefab;
        }

        /// <summary>
        /// Sets maximum capacity for this component prefab's pool.
        /// Returns the prefab for fluent chaining.
        /// </summary>
        public static T SetPoolCapacity<T>(this T prefab, int maxCapacity) where T : Component
        {
            SmartPool.SetCapacity(prefab, maxCapacity);
            return prefab;
        }

        /// <summary>
        /// Marks whether this prefab's pool should be automatically cleared when a scene unloads.
        /// Returns the prefab for fluent chaining.
        /// </summary>
        public static GameObject SetSceneLocal(this GameObject prefab, bool isSceneLocal = true)
        {
            SmartPool.SetSceneLocal(prefab, isSceneLocal);
            return prefab;
        }

        /// <summary>
        /// Marks whether this component prefab's pool should be automatically cleared when a scene unloads.
        /// Returns the prefab for fluent chaining.
        /// </summary>
        public static T SetSceneLocal<T>(this T prefab, bool isSceneLocal = true) where T : Component
        {
            SmartPool.SetSceneLocal(prefab, isSceneLocal);
            return prefab;
        }

        /// <summary>
        /// Gets the number of currently idle (inactive) instances cached in the pool for this GameObject prefab.
        /// </summary>
        public static int GetPoolIdleCount(this GameObject prefab)
        {
            return SmartPool.GetIdleCount(prefab);
        }

        /// <summary>
        /// Gets the number of currently idle (inactive) instances cached in the pool for this component prefab.
        /// </summary>
        public static int GetPoolIdleCount<T>(this T prefab) where T : Component
        {
            return SmartPool.GetIdleCount(prefab);
        }

        /// <summary>
        /// Gets the maximum capacity of idle instances for this GameObject prefab's pool.
        /// Returns -1 if unlimited or if no pool exists yet.
        /// </summary>
        public static int GetPoolCapacity(this GameObject prefab)
        {
            return SmartPool.GetCapacity(prefab);
        }

        /// <summary>
        /// Gets the maximum capacity of idle instances for this component prefab's pool.
        /// Returns -1 if unlimited or if no pool exists yet.
        /// </summary>
        public static int GetPoolCapacity<T>(this T prefab) where T : Component
        {
            return SmartPool.GetCapacity(prefab);
        }

        #endregion
    }
}
