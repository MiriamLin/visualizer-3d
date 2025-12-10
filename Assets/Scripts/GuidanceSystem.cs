using UnityEngine;

public class GuidanceSystem : MonoBehaviour
{
    [Tooltip("Current target the guidance logic should use.")]
    public Transform currentTarget;

    /// <summary>
    /// Assigns a new target for guidance logic to follow or visualize.
    /// </summary>
    /// <param name="target">Transform of the selected target.</param>
    public void SetTarget(Transform target)
    {
        currentTarget = target;
    }
}
