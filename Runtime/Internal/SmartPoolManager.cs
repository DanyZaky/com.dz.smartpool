using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DZ.SmartPool.Tests")]

namespace DZ.SmartPool
{
    /// <summary>
    /// Static facade providing high-level pool controls: Prewarm, Clear, ClearAll, Spawn, Despawn, Capacity, and Visibility settings.
    /// </summary>
    public static class SmartPool
    {
        private static bool _showInHierarchy = true;
        private static bool _autoClearOnSceneChange = false;

        /// <summary>
        /// Gets or sets whether pool root containers and inactive objects are visible in the Unity Hierarchy window.
        /// When false, sets HideFlags.HideInHierarchy on pool root containers.
        /// </summary>
        public static bool ShowInHierarchy
        {
            get => _showInHierarchy;
            set
            {
                _showInHierarchy = value;
                if (SmartPoolManager.HasInstance)
                {
                    SmartPoolManager.Instance.UpdateHierarchyVisibility(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether all pools are automatically cleared when a scene is unloaded.
        /// Defaults to false (pools persist across scenes with DontDestroyOnLoad).
        /// </summary>
        public static bool AutoClearOnSceneChange
        {
            get => _autoClearOnSceneChange;
            set => _autoClearOnSceneChange = value;
        }

        #region Prewarm & Capacity

        /// <summary>
        /// Prewarms a pool by instantiating the specified number of objects ahead of time.
        /// Optionally sets the maximum pool capacity.
        /// </summary>
        public static void Prewarm(GameObject prefab, int count, int maxCapacity = -1)
        {
            if (prefab == null || count <= 0) return;
            var pool = SmartPoolManager.Instance.GetOrCreatePool(prefab);
            if (maxCapacity >= 0)
            {
                pool.MaxCapacity = maxCapacity;
            }
            pool.Prewarm(count);
        }

        /// <summary>
        /// Prewarms a pool for a component prefab.
        /// </summary>
        public static void Prewarm<T>(T prefab, int count, int maxCapacity = -1) where T : Component
        {
            if (prefab == null) return;
            Prewarm(prefab.gameObject, count, maxCapacity);
        }

        /// <summary>
        /// Sets the maximum capacity for a prefab's pool.
        /// When despawned, any objects beyond this limit are safely destroyed to prevent memory bloat.
        /// </summary>
        public static void SetCapacity(GameObject prefab, int maxCapacity)
        {
            if (prefab == null) return;
            SmartPoolManager.Instance.GetOrCreatePool(prefab).MaxCapacity = maxCapacity;
        }

        /// <summary>
        /// Sets the maximum capacity for a component prefab's pool.
        /// </summary>
        public static void SetCapacity<T>(T prefab, int maxCapacity) where T : Component
        {
            if (prefab == null) return;
            SetCapacity(prefab.gameObject, maxCapacity);
        }

        /// <summary>
        /// Sets whether a specific pool is scene-local (automatically cleared when the scene unloads).
        /// </summary>
        public static void SetSceneLocal(GameObject prefab, bool isSceneLocal)
        {
            if (prefab == null) return;
            SmartPoolManager.Instance.GetOrCreatePool(prefab).IsSceneLocal = isSceneLocal;
        }

        /// <summary>
        /// Sets whether a component prefab's pool is scene-local.
        /// </summary>
        public static void SetSceneLocal<T>(T prefab, bool isSceneLocal) where T : Component
        {
            if (prefab == null) return;
            SetSceneLocal(prefab.gameObject, isSceneLocal);
        }

        /// <summary>
        /// Gets the number of currently idle (inactive) instances cached in the pool for the given prefab.
        /// Returns 0 if no pool exists for this prefab.
        /// </summary>
        public static int GetIdleCount(GameObject prefab)
        {
            if (prefab == null || !SmartPoolManager.HasInstance) return 0;
            return SmartPoolManager.Instance.GetIdleCount(prefab);
        }

        /// <summary>
        /// Gets the number of currently idle (inactive) instances cached in the pool for the given component prefab.
        /// Returns 0 if no pool exists for this prefab.
        /// </summary>
        public static int GetIdleCount<T>(T prefab) where T : Component
        {
            if (prefab == null) return 0;
            return GetIdleCount(prefab.gameObject);
        }

        /// <summary>
        /// Gets the maximum capacity of idle instances for the given prefab's pool.
        /// Returns -1 if unlimited or if no pool exists yet.
        /// </summary>
        public static int GetCapacity(GameObject prefab)
        {
            if (prefab == null || !SmartPoolManager.HasInstance) return -1;
            return SmartPoolManager.Instance.GetCapacity(prefab);
        }

        /// <summary>
        /// Gets the maximum capacity of idle instances for the given component prefab's pool.
        /// Returns -1 if unlimited or if no pool exists yet.
        /// </summary>
        public static int GetCapacity<T>(T prefab) where T : Component
        {
            if (prefab == null) return -1;
            return GetCapacity(prefab.gameObject);
        }

        #endregion

        #region Spawn

        /// <summary>
        /// Spawns an instance from the pool.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[SmartPool] Cannot spawn from a null prefab.");
                return null;
            }

            return SmartPoolManager.Instance.GetOrCreatePool(prefab).Spawn(position, rotation, parent);
        }

        /// <summary>
        /// Spawns an instance and returns the specified component type directly.
        /// </summary>
        public static T Spawn<T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            var instance = Spawn(prefab, position, rotation, parent);
            if (instance == null) return null;

            var component = instance.GetComponent<T>();
            if (component == null)
            {
                Debug.LogWarning($"[SmartPool] Spawned object '{instance.name}' does not contain requested component '{typeof(T).Name}'.", instance);
            }
            return component;
        }

        #endregion

        #region Despawn

        /// <summary>
        /// Despawns an instance back into its pool.
        /// Supports an optional completion callback invoked immediately before deactivation.
        /// Fallback safety: If created via standard Instantiate, falls back to Destroy().
        /// Double-despawn guard: Safely ignores if already in pool.
        /// </summary>
        public static void Despawn(GameObject instance, Action onComplete = null)
        {
            if (instance == null) return;

            var pooledObj = instance.GetComponent<PooledObject>();
            if (pooledObj == null)
            {
                // Fallback safety: non-pooled object is destroyed without throwing errors
                InvokeOnCompleteSafe(onComplete);
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                return;
            }

            if (!pooledObj.IsSpawned || pooledObj.IsDespawning)
            {
                // Double-despawn and re-entrancy guard
                return;
            }

            if (pooledObj.Pool == null || pooledObj.Pool.IsDisposed)
            {
                pooledObj.OnDespawn();
                InvokeOnCompleteSafe(onComplete);
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                return;
            }

            pooledObj.Pool.Despawn(instance, pooledObj, onComplete);
        }

        internal static void InvokeOnCompleteSafe(Action onComplete)
        {
            if (onComplete == null) return;
            try
            {
                onComplete();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Despawns a component's GameObject back to its pool.
        /// </summary>
        public static void Despawn(Component component, Action onComplete = null)
        {
            if (component != null)
            {
                Despawn(component.gameObject, onComplete);
            }
        }

        /// <summary>
        /// Schedules delayed despawn for an instance.
        /// Supports unscaled time and completion callbacks.
        /// </summary>
        public static void DespawnAfter(GameObject instance, float delay, bool useUnscaledTime = false, Action onComplete = null)
        {
            if (instance == null) return;

            var pooledObj = instance.GetComponent<PooledObject>();
            if (pooledObj != null)
            {
                pooledObj.DespawnAfter(delay, useUnscaledTime, onComplete);
            }
            else
            {
                // Fallback safety for non-pooled objects via AutoDespawnTimer helper
                var timer = instance.GetComponent<AutoDespawnTimer>();
                if (timer == null)
                {
                    timer = instance.AddComponent<AutoDespawnTimer>();
                }
                timer.Schedule(delay, 0, useUnscaledTime, onComplete);
            }
        }

        /// <summary>
        /// Schedules delayed despawn for a component's GameObject.
        /// </summary>
        public static void DespawnAfter(Component component, float delay, bool useUnscaledTime = false, Action onComplete = null)
        {
            if (component != null)
            {
                DespawnAfter(component.gameObject, delay, useUnscaledTime, onComplete);
            }
        }

        #endregion

        #region Clear

        /// <summary>
        /// Clears all pooled instances for the given prefab and removes its pool container.
        /// </summary>
        public static void Clear(GameObject prefab)
        {
            if (prefab == null) return;
            SmartPoolManager.Instance.ClearPool(prefab);
        }

        /// <summary>
        /// Clears all pooled instances for the given component prefab.
        /// </summary>
        public static void Clear<T>(T prefab) where T : Component
        {
            if (prefab == null) return;
            Clear(prefab.gameObject);
        }

        /// <summary>
        /// Clears all pools and destroys all pooled objects.
        /// </summary>
        public static void ClearAll()
        {
            SmartPoolManager.Instance.ClearAllPools();
        }

        #endregion
    }

    /// <summary>
    /// Core internal manager that maintains pool dictionaries, scene hierarchy, and pool containers.
    /// Auto-created at runtime with DontDestroyOnLoad.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    internal class SmartPoolManager : MonoBehaviour
    {
        private static SmartPoolManager _instance;
        private static bool _isQuitting;

        private readonly Dictionary<int, ObjectPool> _pools = new Dictionary<int, ObjectPool>();
        private Transform _stagingContainer;

        /// <summary>
        /// Dedicated inactive staging transform used during Instantiate() to prevent premature Awake / OnEnable on active prefabs.
        /// </summary>
        public Transform StagingContainer
        {
            get
            {
                if (_stagingContainer == null)
                {
                    var stagingGo = new GameObject("[SmartPool_Staging]");
                    stagingGo.transform.SetParent(transform, false);
                    stagingGo.SetActive(false);
                    _stagingContainer = stagingGo.transform;
                }
                return _stagingContainer;
            }
        }

        /// <summary>
        /// Returns true if an active instance exists without triggering instantiation.
        /// </summary>
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// Global singleton instance. Created lazily if not present in the scene.
        /// </summary>
        public static SmartPoolManager Instance
        {
            get
            {
                if (_isQuitting) return null;

                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                    _instance = UnityEngine.Object.FindFirstObjectByType<SmartPoolManager>();
#else
                    _instance = UnityEngine.Object.FindObjectOfType<SmartPoolManager>();
#endif
                    if (_instance == null)
                    {
                        var managerGo = new GameObject("[SmartPool_Root]");
                        _instance = managerGo.AddComponent<SmartPoolManager>();
                        DontDestroyOnLoad(managerGo);
                        _instance.UpdateHierarchyVisibility(SmartPool.ShowInHierarchy);
                    }
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                gameObject.name = "[SmartPool_Root]";
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                UpdateHierarchyVisibility(SmartPool.ShowInHierarchy);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
            }
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            OnSceneUnloadedInternal(scene);
        }

        internal void OnSceneUnloadedInternal(Scene scene)
        {
            var poolsToRemove = new List<int>();

            foreach (var kvp in _pools)
            {
                var pool = kvp.Value;
                if (SmartPool.AutoClearOnSceneChange || pool.IsSceneLocal)
                {
                    pool.Clear();
                    poolsToRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < poolsToRemove.Count; i++)
            {
                _pools.Remove(poolsToRemove[i]);
            }
        }

        /// <summary>
        /// Updates hierarchy visibility for root and all active sub-pool containers.
        /// </summary>
        public void UpdateHierarchyVisibility(bool visible)
        {
            var flags = visible ? HideFlags.None : HideFlags.HideInHierarchy;
            gameObject.hideFlags = flags;

            foreach (var pool in _pools.Values)
            {
                if (pool.RootContainer != null)
                {
                    pool.RootContainer.gameObject.hideFlags = flags;
                }
            }
        }

        /// <summary>
        /// Gets an existing pool or creates a new one for the given prefab.
        /// </summary>
        public ObjectPool GetOrCreatePool(GameObject prefab)
        {
            int prefabId = prefab.GetInstanceID();
            if (!_pools.TryGetValue(prefabId, out var pool))
            {
                pool = new ObjectPool(prefab, transform);
                if (!SmartPool.ShowInHierarchy && pool.RootContainer != null)
                {
                    pool.RootContainer.gameObject.hideFlags = HideFlags.HideInHierarchy;
                }
                _pools.Add(prefabId, pool);
            }
            return pool;
        }

        /// <summary>
        /// Clears and removes a specific prefab's pool.
        /// </summary>
        public void ClearPool(GameObject prefab)
        {
            if (prefab == null) return;
            int prefabId = prefab.GetInstanceID();
            if (_pools.TryGetValue(prefabId, out var pool))
            {
                _pools.Remove(prefabId);
                pool.Clear();
            }
        }

        /// <summary>
        /// Clears all pools and destroys all pooled objects.
        /// </summary>
        public void ClearAllPools()
        {
            var poolList = new List<ObjectPool>(_pools.Values);
            _pools.Clear();
            for (int i = 0; i < poolList.Count; i++)
            {
                poolList[i].Clear();
            }
        }

        /// <summary>
        /// Gets the count of idle instances currently waiting in stack for the given prefab.
        /// </summary>
        public int GetIdleCount(GameObject prefab)
        {
            if (prefab == null) return 0;
            int prefabId = prefab.GetInstanceID();
            if (_pools.TryGetValue(prefabId, out var pool))
            {
                return pool.IdleCount;
            }
            return 0;
        }

        /// <summary>
        /// Gets the maximum capacity limit for the given prefab's pool.
        /// Returns -1 if unlimited or if pool does not exist.
        /// </summary>
        public int GetCapacity(GameObject prefab)
        {
            if (prefab == null) return -1;
            int prefabId = prefab.GetInstanceID();
            if (_pools.TryGetValue(prefabId, out var pool))
            {
                return pool.MaxCapacity;
            }
            return -1;
        }

        /// <summary>
        /// Internal pool structure managing a Stack of inactive instances for O(1) retrieval.
        /// </summary>
        internal class ObjectPool
        {
            public GameObject Prefab { get; }
            public int PrefabId { get; }
            public Transform RootContainer { get; }
            public Vector3 DefaultScale { get; }
            public Quaternion DefaultRotation { get; }

            private int _maxCapacity = -1;

            /// <summary>
            /// Maximum number of inactive instances held in pool. -1 means unlimited.
            /// When reduced, immediately trims excess idle instances from the stack.
            /// </summary>
            public int MaxCapacity
            {
                get => _maxCapacity;
                set
                {
                    _maxCapacity = value < 0 ? -1 : value;
                    TrimExcess();
                }
            }

            /// <summary>
            /// Number of idle instances currently held in the pool stack.
            /// </summary>
            public int IdleCount => _stack.Count;

            /// <summary>
            /// When true, this pool is cleared when any scene is unloaded.
            /// </summary>
            public bool IsSceneLocal { get; set; } = false;

            /// <summary>
            /// Indicates whether this pool has been disposed and cleared from registry.
            /// </summary>
            public bool IsDisposed { get; private set; }

            private readonly Stack<GameObject> _stack = new Stack<GameObject>();

            public ObjectPool(GameObject prefab, Transform parentRoot)
            {
                Prefab = prefab;
                PrefabId = prefab.GetInstanceID();
                DefaultScale = prefab.transform.localScale;
                DefaultRotation = prefab.transform.localRotation;
                IsDisposed = false;

                var containerGo = new GameObject($"[Pool] {prefab.name}");
                containerGo.transform.SetParent(parentRoot, false);
                containerGo.SetActive(false);
                RootContainer = containerGo.transform;
            }

            /// <summary>
            /// Trims excess idle instances from the stack if current idle count exceeds MaxCapacity.
            /// </summary>
            private void TrimExcess()
            {
                if (_maxCapacity < 0) return;

                while (_stack.Count > _maxCapacity)
                {
                    var instance = _stack.Pop();
                    if (instance != null)
                    {
                        instance.SetActive(false);
                        UnityEngine.Object.Destroy(instance);
                    }
                }
            }

            /// <summary>
            /// Prewarms pool by creating count inactive instances up to MaxCapacity.
            /// </summary>
            public void Prewarm(int count)
            {
                if (IsDisposed || count <= 0) return;
                if (_maxCapacity == 0) return;

                int targetCount = count;
                if (_maxCapacity > 0)
                {
                    int availableSlots = _maxCapacity - _stack.Count;
                    if (availableSlots <= 0) return;
                    if (targetCount > availableSlots)
                    {
                        targetCount = availableSlots;
                    }
                }

                for (int i = 0; i < targetCount; i++)
                {
                    var instance = CreateNewInstance();
                    instance.SetActive(false);
                    _stack.Push(instance);
                }
            }

            /// <summary>
            /// Retrieves an instance from stack or instantiates a new one if stack is empty.
            /// </summary>
            public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent)
            {
                if (IsDisposed)
                {
                    Debug.LogError($"[SmartPool] Cannot spawn from disposed pool for prefab '{Prefab.name}'.");
                    return null;
                }

                GameObject instance = null;

                // Pop until we find a valid instance (in case an object was destroyed externally)
                while (_stack.Count > 0)
                {
                    instance = _stack.Pop();
                    if (instance != null) break;
                }

                if (instance == null)
                {
                    instance = CreateNewInstance();
                }

                var instanceTransform = instance.transform;
                if (parent != null)
                {
                    instanceTransform.SetParent(parent, false);
                }
                else
                {
                    instanceTransform.SetParent(null, false);
                }

                instanceTransform.position = position;
                instanceTransform.rotation = rotation;

                // Reset and notify lifecycle while instance is still inactive
                var pooledObj = instance.GetComponent<PooledObject>();
                if (pooledObj != null)
                {
                    pooledObj.OnSpawn();
                    // Re-apply caller requested rotation in case ResetComponents restored DefaultLocalRotation
                    instanceTransform.rotation = rotation;
                }

                // Activate instance: Unity triggers Awake / OnEnable with completely fresh and reset state
                instance.SetActive(true);

                return instance;
            }

            /// <summary>
            /// Returns an instance to the pool stack.
            /// If pool is disposed, destroys the instance cleanly without touching the destroyed root container.
            /// If pool exceeds MaxCapacity, safely destroys extra instance to prevent memory bloat.
            /// </summary>
            public void Despawn(GameObject instance, PooledObject pooledObj, Action onComplete = null)
            {
                if (instance == null) return;

                if (IsDisposed)
                {
                    if (pooledObj != null && pooledObj.IsSpawned && !pooledObj.IsDespawning)
                    {
                        pooledObj.OnDespawn();
                    }
                    SmartPool.InvokeOnCompleteSafe(onComplete);
                    instance.SetActive(false);
                    UnityEngine.Object.Destroy(instance);
                    return;
                }

                pooledObj.OnDespawn();
                SmartPool.InvokeOnCompleteSafe(onComplete);

                // Capacity limit check: destroy extra instance if pool is already at capacity (or MaxCapacity == 0)
                if (_maxCapacity >= 0 && _stack.Count >= _maxCapacity)
                {
                    instance.SetActive(false);
                    UnityEngine.Object.Destroy(instance);
                    return;
                }

                if (RootContainer != null)
                {
                    instance.transform.SetParent(RootContainer, false);
                }
                instance.SetActive(false);

                _stack.Push(instance);
            }

            /// <summary>
            /// Destroys all pooled instances in this pool and removes container.
            /// Strictly idempotent.
            /// </summary>
            public void Clear()
            {
                if (IsDisposed) return;
                IsDisposed = true;

                while (_stack.Count > 0)
                {
                    var instance = _stack.Pop();
                    if (instance != null)
                    {
                        UnityEngine.Object.Destroy(instance);
                    }
                }

                if (RootContainer != null)
                {
                    UnityEngine.Object.Destroy(RootContainer.gameObject);
                }
            }

            private GameObject CreateNewInstance()
            {
                var staging = SmartPoolManager.Instance.StagingContainer;
                var instance = UnityEngine.Object.Instantiate(Prefab, staging);
                instance.name = Prefab.name;

                // Awake Pre-pass:
                // Ensure all MonoBehaviours in the entire hierarchy (including inactive child GameObjects)
                // have Awake() executed once so component caching (e.g. GetComponent) is complete BEFORE
                // any IPoolable.OnSpawn() is called, while strictly suppressing premature OnEnable().

                // 1. Record originally inactive child GameObjects and temporarily activate them
                var allTransforms = instance.GetComponentsInChildren<Transform>(true);
                var originallyInactiveChildren = new List<GameObject>();
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    var childGo = allTransforms[i].gameObject;
                    if (childGo != instance && !childGo.activeSelf)
                    {
                        originallyInactiveChildren.Add(childGo);
                        childGo.SetActive(true);
                    }
                }

                // 2. Record originally enabled MonoBehaviours and temporarily disable them
                var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
                var originallyEnabled = new bool[behaviours.Length];
                for (int i = 0; i < behaviours.Length; i++)
                {
                    var mb = behaviours[i];
                    if (mb != null)
                    {
                        originallyEnabled[i] = mb.enabled;
                        mb.enabled = false;
                    }
                }

                // 3. Temporarily parent to the active SmartPoolManager root and activate GameObject.
                // In Unity, when an active GameObject is in an active hierarchy:
                // - Unity calls Awake() on all attached MonoBehaviours in root and all children (even if enabled == false).
                // - Unity DOES NOT call OnEnable() because enabled == false.
                instance.transform.SetParent(SmartPoolManager.Instance.transform, false);
                instance.SetActive(true);

                // Silencing any native components that might react to activation:
                var audioSources = instance.GetComponentsInChildren<AudioSource>(true);
                for (int i = 0; i < audioSources.Length; i++)
                {
                    if (audioSources[i] != null && audioSources[i].isPlaying)
                    {
                        audioSources[i].Stop();
                    }
                }

                var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particleSystems.Length; i++)
                {
                    if (particleSystems[i] != null)
                    {
                        particleSystems[i].Clear(true);
                        particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }

                // 4. Deactivate instance while in hierarchy before moving to RootContainer
                instance.SetActive(false);

                // 5. Restore originally inactive child GameObjects to inactive state
                for (int i = 0; i < originallyInactiveChildren.Count; i++)
                {
                    if (originallyInactiveChildren[i] != null)
                    {
                        originallyInactiveChildren[i].SetActive(false);
                    }
                }

                if (RootContainer != null)
                {
                    instance.transform.SetParent(RootContainer, false);
                }

                // 6. Restore original enabled states while instance is safely inactive in RootContainer.
                // Because activeSelf is false, restoring enabled does not trigger OnEnable.
                for (int i = 0; i < behaviours.Length; i++)
                {
                    var mb = behaviours[i];
                    if (mb != null)
                    {
                        mb.enabled = originallyEnabled[i];
                    }
                }

                var pooledObj = instance.GetComponent<PooledObject>();
                if (pooledObj == null)
                {
                    pooledObj = instance.AddComponent<PooledObject>();
                }
                pooledObj.Initialize(PrefabId, DefaultScale, DefaultRotation, this);

                return instance;
            }
        }
    }
}
