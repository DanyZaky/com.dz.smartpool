using System;
using UnityEngine;

namespace DZ.SmartPool
{
    /// <summary>
    /// Internal component attached to pooled instances.
    /// Caches components on initialization to ensure zero GC during runtime,
    /// performs automatic component resets, handles lifecycle callbacks, and guards against double-despawn.
    /// </summary>
    [AddComponentMenu("")]
    internal class PooledObject : MonoBehaviour
    {
        public int PrefabId { get; private set; }
        public Vector3 DefaultLocalScale { get; private set; }
        public Quaternion DefaultLocalRotation { get; private set; }
        public bool IsSpawned { get; private set; }
        public int SpawnToken { get; private set; }
        public SmartPoolManager.ObjectPool Pool { get; private set; }

        private IPoolable[] _poolables;
        private Rigidbody[] _rigidbodies;
        private Rigidbody2D[] _rigidbodies2D;
        private TrailRenderer[] _trails;
        private ParticleSystem[] _particles;
        private AutoDespawnTimer _timer;

        /// <summary>
        /// Initializes the pooled object, caches components once, and stores default transform values.
        /// </summary>
        public void Initialize(int prefabId, Vector3 defaultScale, Quaternion defaultRotation, SmartPoolManager.ObjectPool pool)
        {
            PrefabId = prefabId;
            DefaultLocalScale = defaultScale;
            DefaultLocalRotation = defaultRotation;
            Pool = pool;
            IsSpawned = false;
            SpawnToken = 0;

            // Cache all lifecycle and resettable components once to guarantee zero GC during gameplay
            _poolables = GetComponentsInChildren<IPoolable>(true);
            _rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            _rigidbodies2D = GetComponentsInChildren<Rigidbody2D>(true);
            _trails = GetComponentsInChildren<TrailRenderer>(true);
            _particles = GetComponentsInChildren<ParticleSystem>(true);

            _timer = GetComponent<AutoDespawnTimer>();
            if (_timer == null)
            {
                _timer = gameObject.AddComponent<AutoDespawnTimer>();
            }
        }

        /// <summary>
        /// Called when the object is retrieved from the pool.
        /// Resets physics, visual effects, local scale, and triggers IPoolable.OnSpawn.
        /// </summary>
        public void OnSpawn()
        {
            IsSpawned = true;
            SpawnToken++;

            ResetComponents();

            // Trigger IPoolable lifecycle hooks
            for (int i = 0; i < _poolables.Length; i++)
            {
                if (_poolables[i] != null)
                {
                    _poolables[i].OnSpawn();
                }
            }
        }

        /// <summary>
        /// Called immediately before returning to pool.
        /// Triggers IPoolable.OnDespawn and cancels any pending auto-despawn timer.
        /// </summary>
        public void OnDespawn()
        {
            if (_timer != null)
            {
                _timer.Cancel();
            }

            for (int i = 0; i < _poolables.Length; i++)
            {
                if (_poolables[i] != null)
                {
                    _poolables[i].OnDespawn();
                }
            }

            IsSpawned = false;
        }

        /// <summary>
        /// Schedules delayed auto-despawn.
        /// </summary>
        public void DespawnAfter(float delay, bool useUnscaledTime = false, Action onComplete = null)
        {
            if (_timer != null)
            {
                _timer.Schedule(delay, SpawnToken, useUnscaledTime, onComplete);
            }
        }

        /// <summary>
        /// Resets default Unity components to their clean state.
        /// </summary>
        public void ResetComponents()
        {
            // Reset local scale to prefab default
            transform.localScale = DefaultLocalScale;

            // Reset 3D physics velocities (supporting Unity 2021.3 LTS through Unity 6)
            for (int i = 0; i < _rigidbodies.Length; i++)
            {
                var rb = _rigidbodies[i];
                if (rb != null)
                {
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector3.zero;
#else
                    rb.velocity = Vector3.zero;
#endif
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // Reset 2D physics velocities
            for (int i = 0; i < _rigidbodies2D.Length; i++)
            {
                var rb2d = _rigidbodies2D[i];
                if (rb2d != null)
                {
#if UNITY_6000_0_OR_NEWER
                    rb2d.linearVelocity = Vector2.zero;
#else
                    rb2d.velocity = Vector2.zero;
#endif
                    rb2d.angularVelocity = 0f;
                }
            }

            // Reset TrailRenderers to avoid visual teleport streaks
            for (int i = 0; i < _trails.Length; i++)
            {
                var trail = _trails[i];
                if (trail != null)
                {
                    trail.Clear();
                }
            }

            // Reset and restart ParticleSystems
            for (int i = 0; i < _particles.Length; i++)
            {
                var ps = _particles[i];
                if (ps != null)
                {
                    ps.Clear(true);
                    ps.Play(true);
                }
            }
        }
    }
}
