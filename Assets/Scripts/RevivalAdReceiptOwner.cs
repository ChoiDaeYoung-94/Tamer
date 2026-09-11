#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
using UnityEngine;

/// <summary>Separate lifetime from the persistent harness counters and ad manager.</summary>
public sealed class RevivalAdReceiptOwner : MonoBehaviour { }
#endif
