using System;
using UnityEngine;

namespace DZ.SmartPool
{
    /// <summary>
    /// Internal helper component that manages delayed auto-despawn safely with zero GC allocations.
    /// Supports scaled and unscaled time, completion callbacks, and automatically cancels
    /// if the object is manually despawned, disabled, or reused before the timer expires.
    /// </summary>
    [AddComponentMenu("")]
    internal class AutoDespawnTimer : MonoBehaviour
    {
        private float _targetTime;
        private bool _useUnscaledTime;
        private bool _isRunning;
        private int _expectedToken;
        private Action _onComplete;
        private PooledObject _pooledObject;

        private void Awake()
        {
            _pooledObject = GetComponent<PooledObject>();
            enabled = false;
        }

        /// <summary>
        /// Schedules an auto-despawn after the given duration (in seconds).
        /// </summary>
        /// <param name="delay">Duration in seconds to wait before despawning.</param>
        /// <param name="token">Spawn generation token to guard against stale timer execution.</param>
        /// <param name="useUnscaledTime">If true, uses Time.unscaledTime (ignores Time.timeScale).</param>
        /// <param name="onComplete">Optional callback invoked right before despawn.</param>
        public void Schedule(float delay, int token, bool useUnscaledTime = false, Action onComplete = null)
        {
            _expectedToken = token;
            _useUnscaledTime = useUnscaledTime;
            _onComplete = onComplete;

            if (delay <= 0f)
            {
                Cancel();
                TriggerDespawn();
                return;
            }

            float currentTime = _useUnscaledTime ? Time.unscaledTime : Time.time;
            _targetTime = currentTime + delay;
            _isRunning = true;
            enabled = true;
        }

        /// <summary>
        /// Cancels any pending auto-despawn timer immediately.
        /// </summary>
        public void Cancel()
        {
            _isRunning = false;
            _onComplete = null;
            enabled = false;
        }

        private void Update()
        {
            if (!_isRunning) return;

            float currentTime = _useUnscaledTime ? Time.unscaledTime : Time.time;
            if (currentTime >= _targetTime)
            {
                _isRunning = false;
                enabled = false;
                TriggerDespawn();
            }
        }

        private void TriggerDespawn()
        {
            var callback = _onComplete;
            _onComplete = null;

            if (_pooledObject != null)
            {
                // Verify spawn token and active state to ensure timer belongs to current spawn cycle
                if (_pooledObject.IsSpawned && _pooledObject.SpawnToken == _expectedToken)
                {
                    callback?.Invoke();
                    SmartPool.Despawn(gameObject);
                }
            }
            else
            {
                // Fallback for non-pooled object
                callback?.Invoke();
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            // Safety guard: if object is disabled or despawned by other means, cancel running timer
            Cancel();
        }
    }
}
