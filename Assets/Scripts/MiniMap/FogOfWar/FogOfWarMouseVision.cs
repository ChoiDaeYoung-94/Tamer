using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FogOfWarMouseVision : FogOfWarVisionBase
{
    public override HashSet<Vector2Int> VisibleCells(HashSet<Vector2Int> blockedCells)
    {
        HashSet<Vector2Int> result = new HashSet<Vector2Int>();

        result.UnionWith(LineOfSight.TraceInRadius(FogOfWarGrid.WorldToGridPosition(transform.localPosition), VisibilityRadius, blockedCells));

        return result;
    }
}
