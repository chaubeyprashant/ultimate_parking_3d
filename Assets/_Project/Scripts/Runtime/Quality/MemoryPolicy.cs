using System;

namespace UltimateCarParking.Quality
{
    public enum MemoryAction { None, CleanUpNow, CleanUpWhenSafe, HalveTextures }

    /// <summary>
    /// What to do when Android warns about low memory (pure logic, injectable time). First warning: free unused assets, but only at a
    /// moment where a hitch is invisible (not while the player is driving). Repeated warnings within five minutes: also switch to
    /// half-size textures, which cuts texture memory to a quarter, until the app restarts.
    /// </summary>
    public sealed class MemoryPolicy
    {
        public const double RepeatWindowSeconds = 300.0;
        public const int WarningsBeforeHalving = 2;

        private double firstInWindow = double.NegativeInfinity;
        private int warningsInWindow;
        public bool TexturesHalved { get; private set; }

        public MemoryAction OnLowMemory(double nowSeconds, bool playerIsDriving)
        {
            if (nowSeconds - firstInWindow > RepeatWindowSeconds) { firstInWindow = nowSeconds; warningsInWindow = 0; }
            warningsInWindow++;
            if (warningsInWindow >= WarningsBeforeHalving && !TexturesHalved && !playerIsDriving)
            {
                TexturesHalved = true;
                return MemoryAction.HalveTextures;
            }
            return playerIsDriving ? MemoryAction.CleanUpWhenSafe : MemoryAction.CleanUpNow;
        }

        /// <summary>Called when a scene finishes loading or the player is back in a menu: true if a deferred clean-up is due.</summary>
        public bool TakeDeferredCleanup(ref bool pending)
        {
            if (!pending) return false;
            pending = false;
            return true;
        }
    }
}
