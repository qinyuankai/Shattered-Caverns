using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10)]
public sealed class PlayerFreezeController : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerChunkTracker chunkTracker;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private LayerMask solidLayers;
    [SerializeField, Min(0f)] private float freezeSeconds = 0.95f;

    private readonly List<RaycastHit2D> sunlightHits = new List<RaycastHit2D>(1);
    private float shadowElapsedSeconds;
    private bool frozePlayer;

    private void OnEnable()
    {
        if (playerMovement == null || chunkTracker == null || bodyCollider == null || bodyCollider.isTrigger)
        {
            Debug.LogError("PlayerFreezeController requires PlayerMovement, PlayerChunkTracker and the player's solid collider.", this);
            enabled = false;
            return;
        }

        playerMovement.Respawned += ClearFreezeProgress;
    }

    private void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.Respawned -= ClearFreezeProgress;
        }

        ClearWeatherFreeze();
    }

    private void ClearWeatherFreeze()
    {
        if (frozePlayer && playerMovement != null)
        {
            playerMovement.SetFrozen(false);
        }

        ClearFreezeProgress();
    }

    private void ClearFreezeProgress()
    {
        shadowElapsedSeconds = 0f;
        frozePlayer = false;
    }

    private void FixedUpdate()
    {
        ChunkController currentChunk = chunkTracker.CurrentChunk;
        ChunkWeather weather = currentChunk != null ? currentChunk.Weather : null;
        if (weather == null || !weather.CanFreezePlayer)
        {
            ClearWeatherFreeze();
            return;
        }

        Bounds bodyBounds = bodyCollider.bounds;
        Vector2 head = new Vector2(bodyBounds.center.x, bodyBounds.max.y);
        bool isInShadow = !weather.HasSunlight
            || !WeatherMechanismPhysics.HasClearSky(head, solidLayers, null, sunlightHits);

        if (!isInShadow)
        {
            ClearFreezeProgress();

            if (playerMovement.IsFrozen)
            {
                playerMovement.SetFrozen(false);
            }

            return;
        }

        if (playerMovement.IsFrozen || !playerMovement.HasReceivedMovementInput)
        {
            return;
        }

        shadowElapsedSeconds = Mathf.Min(shadowElapsedSeconds + Time.fixedDeltaTime, freezeSeconds);

        if (shadowElapsedSeconds >= freezeSeconds && playerMovement.HasGroundSupport)
        {
            playerMovement.SetFrozen(true);
            frozePlayer = true;
        }
    }
}
