using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FogOfWar/UnitGroup", fileName = "FogOfWarUnitGroup")]
public class FogOfWarUnitGroup : ScriptableObject
{
    public List<FogOfWarEntity> FogOfWarEntities { get; set; } = new List<FogOfWarEntity>();
    public List<FogOfWarVisionBase> FogOfWarVisions { get; set; } = new List<FogOfWarVisionBase>();

    public void Clear()
    {
        FogOfWarEntities.Clear();
        FogOfWarVisions.Clear();
    }
}
