using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

#if UNITY_INPUT_SYSTEM || ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.XR;
using TMPro;

public class RemoteSliceViewer : MonoBehaviour
{
    [Header("Server (http://<IP>:8080)")]
    public string serverBase = "http://192.168.0.2:8080";
    public string filename = "your.tif";

    [Header("Targets")]
    public Renderer xzRenderer;
    public Renderer yzRenderer;

    [Header("UI Labels")] 
    public TMP_Text xzLabel;
    public TMP_Text yzLabel;

    [Header("Slice Indices")]
    public int xIndex = 0; // XZ uses X index
    public int yIndex = 0; // YZ uses Y index
    public int maxX = 128;
    public int maxY = 128;

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
        Vector2 leftStick = ReadLeftStick();
        Vector2 rightStick = ReadRightStick();

        if (Time.time > nextAllowedInput)
        {
            if (Mathf.Abs(leftStick.x) > deadzone)
            {
                xIndex = Mathf.Clamp(xIndex + (leftStick.x > 0 ? 1 : -1), 0, maxX - 1);
                Debug.Log($"[RemoteSliceViewer] X (XZ) index -> {xIndex}");
                StartCoroutine(LoadXZ());
                nextAllowedInput = Time.time + repeatDelay;
            }
            else if (Mathf.Abs(rightStick.x) > deadzone)
            {
                yIndex = Mathf.Clamp(yIndex + (rightStick.x > 0 ? 1 : -1), 0, maxY - 1);
                Debug.Log($"[RemoteSliceViewer] Y (YZ) index -> {yIndex}");
                StartCoroutine(LoadYZ());
                nextAllowedInput = Time.time + repeatDelay;
            }
        }

        if (xzLabel != null)
        {
            xzLabel.text = $"xz x={xIndex}"; 
        }

        if (yzLabel != null)
        {
            yzLabel.text = $"yz y={yIndex}";
        }
    }

    // Reads left controller stick if Oculus Integration is present; otherwise falls back to Input System or XR.
    private Vector2 ReadLeftStick()
    {
#if OCULUS_INTEGRATION
        return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
#elif UNITY_INPUT_SYSTEM || ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            return Gamepad.current.leftStick.ReadValue();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null)
        {
            float x = 0f;
            if (Keyboard.current.leftArrowKey.isPressed) x = -1f;
            if (Keyboard.current.rightArrowKey.isPressed) x = 1f;
            return new Vector2(x, 0f);
        }
#endif
        return ReadXR2DAxis(UnityEngine.XR.XRNode.LeftHand);
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), 0f);
#endif
    }

    // Reads right controller stick if Oculus Integration is present; otherwise falls back to Input System or XR.
    private Vector2 ReadRightStick()
    {
#if OCULUS_INTEGRATION
        return OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
#elif UNITY_INPUT_SYSTEM || ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            return Gamepad.current.rightStick.ReadValue();
        }
        return ReadXR2DAxis(UnityEngine.XR.XRNode.RightHand);
#else
        return Vector2.zero;
#endif
    }

    private Vector2 ReadXR2DAxis(UnityEngine.XR.XRNode node)
    {
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
        {
            return axis;
        }
        return Vector2.zero;
    }

    private IEnumerator LoadXZ()
    {
        string url = $"{serverBase}/read_slice/{filename}?plane=xz&index={xIndex}";
        yield return FetchAndApply(url, xzRenderer, "XZ");
    }

    private IEnumerator LoadYZ()
    {
        string url = $"{serverBase}/read_slice/{filename}?plane=yz&index={yIndex}";
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
            // Debug.Log($"[{label}] Loaded {tex.width}x{tex.height} from {url}");
            target.material.mainTexture = tex;
            target.material.color = Color.white;
        }
    }
}