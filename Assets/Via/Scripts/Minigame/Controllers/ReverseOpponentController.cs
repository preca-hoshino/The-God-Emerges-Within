using UnityEngine;
using Via.Minigame.Domain;

namespace Via.Minigame.Runtime
{
    // Drives the reverse-opponent AI for levels that enable it.
    public sealed class ReverseOpponentController : MonoBehaviour
    {
        [SerializeField] private MinigameController minigame;
        [SerializeField, Min(0.05f)] private float intervalSeconds = 2.2f;
        [SerializeField] private Vector2[] waypoints =
        {
            new Vector2(2f, 0f), new Vector2(3f, 1.6f), new Vector2(2f, 3.2f), new Vector2(1f, 2.2f), new Vector2(0f, 0f),
            new Vector2(-2f, 0f), new Vector2(-3f, 1.6f), new Vector2(-2f, 3.2f), new Vector2(-1f, 2.2f), new Vector2(0f, 0f)
        };

        private ReverseOpponent opponent;

        private void OnEnable()
        {
            if (minigame == null)
            {
                Debug.LogError("Bind MinigameController for the reverse opponent.", this);
                enabled = false;
                return;
            }
            minigame.OnLevelStarted += Begin;
            minigame.OnLevelReset += ResetOpponent;
        }

        private void OnDisable()
        {
            if (minigame != null)
            {
                minigame.OnLevelStarted -= Begin;
                minigame.OnLevelReset -= ResetOpponent;
            }
        }

        private void Begin()
        {
            opponent = minigame.Configuration.HasOpponent
                ? new ReverseOpponent(waypoints, intervalSeconds, minigame.Configuration.CornerAngle)
                : null;
        }

        private void ResetOpponent() => opponent?.Reset();

        private void Update()
        {
            if (opponent == null || !minigame.IsOpen || minigame.IsGuided || minigame.IsPaused || minigame.IsCompleted) return;
            if (minigame.TryCompleteOpponent()) return;
            Vector2? placement = opponent.Tick(Time.deltaTime, minigame.Network);
            if (placement.HasValue) minigame.OpponentAttract(placement.Value);
        }
    }
}
