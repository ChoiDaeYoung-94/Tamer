using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Extension
{
    public static T GetComponent_<T>(this GameObject go) where T : Component
    {
        return AD.Utils.GetComponent_<T>(go);
    }
}
