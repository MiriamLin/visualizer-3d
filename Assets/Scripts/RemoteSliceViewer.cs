using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

#if UNITY_INPUT_SYSTEM || ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.XR;

// Shows XZ (left) and YZ (right) slices by pulling PNGs from the Flask server.
// Use thumbstick to change slice index: up/down -> Y (XZ plane), left/right -> X (YZ plane).
public class RemoteSliceViewer : MonoBehaviour
{
    [Header("Server (http://<IP>:8080)")]
    public string serverBase = "http://192.168.0.2:8080";
    public string filename = "your.tif";

    [Header("Targets")]
    public Renderer xzRenderer;
    public Renderer yzRenderer;

    [Header("Slice Indices")]
    public int yIndex = 0; // XZ uses Y index
    public int xIndex = 0; // YZ uses X index
    public int maxY = 128;
    public int maxX = 128;

    [Header("Input")]
    public float deadzone = 0.25f;
    public float repeatDelay = 0.2f;

    private float nextAllowedInput;

    private void Start()
    {
        StartCoroutine(LoadXZ());
        StartCoroutine(LoadYZ());
    }

    private void Update()
    {
        Vector2 stick = ReadStick();

        if (Time.time < nextAllowedInput)
            return;

        if (Mathf.Abs(stick.y) > deadzone)
        {
            yIndex = Mathf.Clamp(yIndex + (stick.y > 0 ? 1 : -1), 0, maxY - 1);
            Debug.Log($"[RemoteSliceViewer] Y index -> {yIndex}");
            StartCoroutine(LoadXZ());
            nextAllowedInput = Time.time + repeatDelay;
        }
        else if (Mathf.Abs(stick.x) > deadzone)
        {
            xIndex = Mathf.Clamp(xIndex + (stick.x > 0 ? 1 : -1), 0, maxX - 1);
            Debug.Log($"[RemoteSliceViewer] X index -> {xIndex}");
            StartCoroutine(LoadYZ());
            nextAllowedInput = Time.time + repeatDelay;
        }
    }

    // Reads controller stick if Oculus Integration is present; otherwise falls back to Input System or old Input axes.
    private Vector2 ReadStick()
    {
#if OCULUS_INTEGRATION
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        if (stick == Vector2.zero)
        {
            stick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        }
        return stick;
#elif UNITY_INPUT_SYSTEM || ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            return Gamepad.current.leftStick.ReadValue();
        }
        // XR (new Input System) fallback via legacy XR API
        Vector2 xrAxis = ReadXRPrimary2DAxis();
        if (xrAxis != Vector2.zero)
        {
            return xrAxis;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Allow keyboard fallback in editor when no gamepad connected.
        if (Keyboard.current != null)
        {
            float x = 0f;
            float y = 0f;
            if (Keyboard.current.leftArrowKey.isPressed) x = -1f;
            if (Keyboard.current.rightArrowKey.isPressed) x = 1f;
            if (Keyboard.current.downArrowKey.isPressed) y = -1f;
            if (Keyboard.current.upArrowKey.isPressed) y = 1f;
            return new Vector2(x, y);
        }
#endif
        return Vector2.zero;
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }

    private Vector2 ReadXRPrimary2DAxis()
    {
        UnityEngine.XR.InputDevice left = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (left.isValid && left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis) && axis != Vector2.zero)
        {
            return axis;
        }

        UnityEngine.XR.InputDevice right = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (right.isValid && right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out axis))
        {
            return axis;
        }

        return Vector2.zero;
    }

    private IEnumerator LoadXZ()
    {
        string url = $"{serverBase}/read_slice/{filename}?plane=xz&index={yIndex}";
        yield return FetchAndApply(url, xzRenderer, "XZ");
    }

    private IEnumerator LoadYZ()
    {
        string url = $"{serverBase}/read_slice/{filename}?plane=yz&index={xIndex}";
        yield return FetchAndApply(url, yzRenderer, "YZ");
    }

    private IEnumerator FetchAndApply(string url, Renderer target, string label)
    {
        if (target == null)
            yield break;

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            // Uncomment if you serve HTTPS with self-signed certs.
            // req.certificateHandler = new AcceptAllCertificatesHandler();

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[{label}] Slice fetch failed: {req.error} | {url}");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            Debug.Log($"[{label}] Loaded {tex.width}x{tex.height} from {url}");
            target.material.mainTexture = tex;
            target.material.color = Color.white;
        }
    }
}
