namespace DZ.SmartPool
{
    /// <summary>
    /// Lifecycle hook interface for objects managed by SmartPool.
    /// Implement this on any MonoBehaviour on the pooled GameObject to receive spawn and despawn callbacks.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called immediately when the object is retrieved from the pool and activated.
        /// Use this to initialize gameplay state, timers, health, or effects.
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// Called immediately before the object is deactivated and returned to the pool.
        /// Use this to clean up listeners, references, or ongoing tweens/effects.
        /// </summary>
        void OnDespawn();
    }
}
