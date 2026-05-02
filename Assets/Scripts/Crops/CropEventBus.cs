using System;
using UnityEngine;

namespace EldritchFarm.Crops
{
    // =====================================================================
    // EVENT PAYLOADS
    // =====================================================================
    // Each event carries enough info for any subscriber to decide whether
    // and how to react, without needing to query the source crop directly.
    //
    // Add fields to these structs as needed — subscribers using named-field
    // access won't break. Avoid removing or renaming fields once shipped.
    // =====================================================================

    /// <summary>
    /// A crop made a loud, attention-grabbing noise.
    /// Examples: Sentry Corn screaming, a harvested Wailing Wheat shrieking.
    /// Skittish crops flee from this. Other Sentries become more alert.
    /// </summary>
    public readonly struct CropScreamEvent
    {
        public readonly CropBehavior Source;
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly float Intensity; // 0–1, for graded reactions

        public CropScreamEvent(CropBehavior source, Vector3 position, float radius, float intensity = 1f)
        {
            Source = source;
            Position = position;
            Radius = radius;
            Intensity = Mathf.Clamp01(intensity);
        }
    }

    /// <summary>
    /// A crop performed a physical attack (or threatening motion).
    /// Examples: Aggressive Pumpkin's stem swing.
    /// Witnessing crops may become alert, flinch, or retaliate.
    /// </summary>
    public readonly struct CropAttackEvent
    {
        public readonly CropBehavior Source;
        public readonly Vector3 Position;
        public readonly Vector3 Direction;
        public readonly float Radius;

        public CropAttackEvent(CropBehavior source, Vector3 position, Vector3 direction, float radius)
        {
            Source = source;
            Position = position;
            Direction = direction;
            Radius = radius;
        }
    }

    /// <summary>
    /// A crop bolted from its position.
    /// Examples: Skittish Tomato detaching and running.
    /// Chaotic motion — can collide with other crops, trigger pile-ons.
    /// </summary>
    public readonly struct CropFledEvent
    {
        public readonly CropBehavior Source;
        public readonly Vector3 Position;
        public readonly Vector3 Direction;

        public CropFledEvent(CropBehavior source, Vector3 position, Vector3 direction)
        {
            Source = source;
            Position = position;
            Direction = direction;
        }
    }

    /// <summary>
    /// A crop was successfully harvested.
    /// Mostly informational — useful for crops that react to neighbors
    /// being picked (mourning, retaliation, opportunistic growth).
    /// </summary>
    public readonly struct CropHarvestedEvent
    {
        public readonly CropBehavior Source;
        public readonly Vector3 Position;

        public CropHarvestedEvent(CropBehavior source, Vector3 position)
        {
            Source = source;
            Position = position;
        }
    }

    /// <summary>
    /// Generic "something is wrong here" baseline event.
    /// Use this when a more specific event doesn't fit but you still
    /// want neighbors to have an opportunity to chain-react.
    /// </summary>
    public readonly struct CropDistressEvent
    {
        public readonly CropBehavior Source;
        public readonly Vector3 Position;
        public readonly float Radius;

        public CropDistressEvent(CropBehavior source, Vector3 position, float radius)
        {
            Source = source;
            Position = position;
            Radius = radius;
        }
    }

    // =====================================================================
    // EVENT BUS
    // =====================================================================

    /// <summary>
    /// Global event bus for crop-to-crop and crop-to-system communication.
    /// Crops broadcast typed events here; other crops, visual systems, audio
    /// managers, and debug tools subscribe to whatever they care about.
    ///
    /// Static for simplicity. If we later need per-farm or per-scene scoping,
    /// refactor to a MonoBehaviour singleton — call sites won't change much.
    ///
    /// USAGE:
    ///   Subscribe:   CropEventBus.OnScream += HandleScream;
    ///   Unsubscribe: CropEventBus.OnScream -= HandleScream;
    ///   Raise:       CropEventBus.RaiseScream(new CropScreamEvent(this, pos, radius));
    /// </summary>
    public static class CropEventBus
    {
        // --- Behavioral events ---

        public static event Action<CropScreamEvent> OnScream;
        public static event Action<CropAttackEvent> OnAttack;
        public static event Action<CropFledEvent> OnFled;
        public static event Action<CropHarvestedEvent> OnHarvested;
        public static event Action<CropDistressEvent> OnDistress;

        // --- Lifecycle / state events ---

        /// <summary>
        /// Raised whenever any crop transitions between states.
        /// Useful for visuals, audio, debug HUDs, achievements.
        /// </summary>
        public static event Action<CropBehavior, CropState, CropState> OnCropStateChanged;

        // --- Raise methods ---

        public static void RaiseScream(CropScreamEvent evt) => OnScream?.Invoke(evt);
        public static void RaiseAttack(CropAttackEvent evt) => OnAttack?.Invoke(evt);
        public static void RaiseFled(CropFledEvent evt) => OnFled?.Invoke(evt);
        public static void RaiseHarvested(CropHarvestedEvent evt) => OnHarvested?.Invoke(evt);
        public static void RaiseDistress(CropDistressEvent evt) => OnDistress?.Invoke(evt);

        public static void RaiseCropStateChanged(CropBehavior source, CropState from, CropState to)
            => OnCropStateChanged?.Invoke(source, from, to);

        /// <summary>
        /// Clears all subscribers. Call when leaving play mode in editor
        /// or when reloading the scene to prevent stale references.
        /// </summary>
        public static void ClearAllSubscribers()
        {
            OnScream = null;
            OnAttack = null;
            OnFled = null;
            OnHarvested = null;
            OnDistress = null;
            OnCropStateChanged = null;
        }
    }
}