using System.Collections.Generic;
using UnityEngine;

internal static class WeatherMechanismPhysics
{
    private const float EdgeTolerance = 0.001f;

    internal static bool HasClearSky(
        Vector2 position,
        LayerMask solidLayers,
        Collider2D ownCollider,
        List<RaycastHit2D> hits)
    {
        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(solidLayers);
        Physics2D.Raycast(position, Vector2.up, filter, hits);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != ownCollider)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool HasRoomFor(
        BoxCollider2D target,
        LayerMask blockingLayers,
        Collider2D currentCollider,
        List<Collider2D> overlaps)
    {
        return HasRoomForBox(target.transform, target.offset, target.size, blockingLayers, currentCollider, overlaps);
    }

    internal static bool HasRoomForBox(
        Transform targetTransform,
        Vector2 localCenter,
        Vector2 localSize,
        LayerMask blockingLayers,
        Collider2D currentCollider,
        List<Collider2D> overlaps)
    {
        Vector2 center = targetTransform.TransformPoint(localCenter);
        Vector2 right = targetTransform.TransformVector(Vector3.right);
        Vector2 up = targetTransform.TransformVector(Vector3.up);
        Vector2 size = new Vector2(
            Mathf.Max(0f, localSize.x * right.magnitude - EdgeTolerance),
            Mathf.Max(0f, localSize.y * up.magnitude - EdgeTolerance));
        float angle = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;

        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(blockingLayers);
        Physics2D.OverlapBox(center, size, angle, filter, overlaps);

        foreach (Collider2D overlap in overlaps)
        {
            if (overlap != currentCollider)
            {
                return false;
            }
        }

        return true;
    }
}
