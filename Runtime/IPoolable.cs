namespace DZ.SmartPool
{
    /// <summary>
    /// Lifecycle hook interface for objects managed by SmartPool.
    /// Implement this on any MonoBehaviour on the pooled GameObject to receive spawn and despawn callbacks.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called immediately before the object is activated in the scene (while still inactive, right before OnEnable).
        /// Use this to initialize gameplay state, timers, health, or effects so that OnEnable reads fresh, clean data.
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// Called immediately before the object is deactivated and returned to the pool.
        /// Use this to clean up listeners, references, or ongoing tweens/effects.
        /// </summary>
        void OnDespawn();
    }
}
