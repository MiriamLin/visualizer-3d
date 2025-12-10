using UnityEngine;

// Set the symbol INPUT_SYSTEM_AVAILABLE (e.g., in Player Settings)
// when the new Input System package is installed and its assemblies exist.
#if INPUT_SYSTEM_AVAILABLE || UNITY_INPUT_SYSTEM
#define HAS_NEW_INPUT_SYSTEM
#endif

#if HAS_NEW_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TargetSelector : MonoBehaviour
{
    [Tooltip("The visual marker to place at the target position.")]
    public GameObject targetMarker;

#if HAS_NEW_INPUT_SYSTEM
    [Tooltip("Reference to the Input Action for selecting the target (e.g., controller's trigger button).")]
    public InputActionReference selectAction;
#else
    [Tooltip("Keyboard/mouse button used to select the target when the new Input System package is not installed.")]
    public KeyCode fallbackSelectKey = KeyCode.Mouse0;
#endif

    [Tooltip("The GuidanceSystem to notify when a new target is set.")]
    public GuidanceSystem guidanceSystem;

    [Tooltip("The maximum distance for the raycast.")]
    public float maxRaycastDistance = 100f;

    private void OnEnable()
    {
#if HAS_NEW_INPUT_SYSTEM
        if (selectAction != null)
        {
            selectAction.action.Enable();
            selectAction.action.performed += OnSelectPerformed;
        }
#endif
    }

    private void OnDisable()
    {
#if HAS_NEW_INPUT_SYSTEM
        if (selectAction != null)
        {
            selectAction.action.performed -= OnSelectPerformed;
            selectAction.action.Disable();
        }
#endif
    }

#if HAS_NEW_INPUT_SYSTEM
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        HandleSelection();
    }
#endif

    private void Update()
    {
#if !HAS_NEW_INPUT_SYSTEM
        if (Input.GetKeyDown(fallbackSelectKey))
        {
            HandleSelection();
        }
#endif
    }

    private void HandleSelection()
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

    private void Start()
    {
        // Ensure the marker is inactive at the start
        if (targetMarker != null)
        {
            targetMarker.SetActive(false);
        }
    }
}
