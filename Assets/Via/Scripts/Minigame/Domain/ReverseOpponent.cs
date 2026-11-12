using UnityEngine;

namespace Via.Minigame.Domain
{
    // Opponent logic: periodically requests an attractor placement to rebuild the "calm" eight shape,
    // and holds once the eight is formed. Plain C# so it can be verified directly.
    public sealed class ReverseOpponent
    {
        private readonly Vector2[] waypoints;
        private readonly float interval;
        private readonly float cornerAngle;
        private float timer;
        private int index;

        public bool HasGivenUp { get; private set; }

        public ReverseOpponent(Vector2[] waypoints, float interval, float cornerAngle)
        {
            this.waypoints = waypoints ?? System.Array.Empty<Vector2>();
            this.interval = Mathf.Max(0.05f, interval);
            this.cornerAngle = cornerAngle;
        }

        public void Reset()
        {
            timer = 0f;
            index = 0;
            HasGivenUp = false;
        }

        // Returns a position to place an attractor at, or null if it should wait / already holds.
        public Vector2? Tick(float deltaTime, NeuronNetwork network)
        {
            if (network == null || waypoints.Length == 0) return null;
            var matches = ParadigmEvaluator.Evaluate(network, cornerAngle);
            if (ParadigmEvaluator.Contains(matches, "eight"))
            {
                timer = interval;
                return null;
            }
            // A settled resistance shape is ready for the player to confirm. Do not let the
            // doctor's next placement overwrite that valid result before submission.
            if (ParadigmEvaluator.Contains(matches, "separated")) return null;
            timer -= deltaTime;
            if (timer > 0f) return null;
            timer = interval;
            Vector2 point = waypoints[index % waypoints.Length];
            index++;
            return point;
        }
    }
}
