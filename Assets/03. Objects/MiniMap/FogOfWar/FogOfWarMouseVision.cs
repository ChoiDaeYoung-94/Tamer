using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FogOfWarMouseVision : FogOfWarVisionBase
{
    public override HashSet<Vector2Int> VisibleCells(HashSet<Vector2Int> blockedCells)
    {
        HashSet<Vector2Int> result = new HashSet<Vector2Int>();

        Vector3 vec_position = new Vector3(transform.localPosition.x, 0, transform.localPosition.y);
        result.UnionWith(LineOfSight.TraceInRadius(FogOfWarGrid.WorldToGridPosition(vec_position), VisibilityRadius, blockedCells));

        return result;
    }
}
