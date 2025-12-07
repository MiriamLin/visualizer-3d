using UnityEngine;
using UnityEngine.InputSystem;

public class TargetSelector : MonoBehaviour
{
    [Tooltip("The visual marker to place at the target position.")]
    public GameObject targetMarker;

    [Tooltip("Reference to the Input Action for selecting the target (e.g., controller's trigger button).")]
    public InputActionReference selectAction;

    [Tooltip("The GuidanceSystem to notify when a new target is set.")]
    public GuidanceSystem guidanceSystem;

    [Tooltip("The maximum distance for the raycast.")]
    public float maxRaycastDistance = 100f;

    private void OnEnable()
    {
        selectAction.action.Enable();
        selectAction.action.performed += OnSelectPerformed;
    }

    private void OnDisable()
    {
        selectAction.action.performed -= OnSelectPerformed;
        selectAction.action.Disable();
    }

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        // Raycast from the controller's position and orientation
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, maxRaycastDistance))
        {
            // Move the marker to the point of collision
            if (targetMarker != null)
            {
                targetMarker.transform.position = hit.point;
                targetMarker.SetActive(true);

                // Notify the guidance system of the new target
                if (guidanceSystem != null)
                {
                    guidanceSystem.SetTarget(targetMarker.transform);
                }
            }
            else
            {
                Debug.LogWarning("Target Marker is not assigned in the TargetSelector script.", this);
            }
        }
    }

    void Start()
    {
        // Ensure the marker is inactive at the start
        if (targetMarker != null)
        {
            targetMarker.SetActive(false);
        }
    }
}
