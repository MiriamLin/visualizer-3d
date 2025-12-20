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
    [Header("Server Endpoints (tries in order)")]
    // iPhone 熱點常見：gateway 多為 172.20.10.1，但實際分配給主機的 IP 可能不同（如 172.20.10.5）。
    // 可在 Inspector 加入多個候選，如 gateway + 本機熱點 IP。
    public string[] serverBases = new[] { "http://172.20.10.5:8080", "http://172.20.10.1:8080" };
    public string filename = "your.tif";

    [Header("Targets")]
    public Renderer xzRenderer;
    public Renderer yzRenderer;

    [Header("UI Labels")] 
    public TMP_Text xzLabel;
    public TMP_Text yzLabel;

    [Header("Scaling")]
    public float scaleStep = 0.05f;
    public float minScale = 0.1f;
    public float maxScale = 2f;

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
        UpdateLabels();
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
            else if (Mathf.Abs(leftStick.y) > deadzone && xzRenderer != null)
            {
                AdjustQuadScale(xzRenderer.transform, leftStick.y, "XZ");
                nextAllowedInput = Time.time + repeatDelay;
            }
            else if (Mathf.Abs(rightStick.y) > deadzone && yzRenderer != null)
            {
                AdjustQuadScale(yzRenderer.transform, rightStick.y, "YZ");
                nextAllowedInput = Time.time + repeatDelay;
            }
        }

        UpdateLabels();
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

    private void AdjustQuadScale(Transform target, float inputY, string plane)
    {
        float current = target.localScale.x;
        float delta = Mathf.Sign(inputY) * scaleStep;
        float next = Mathf.Clamp(current + delta, minScale, maxScale);
        target.localScale = new Vector3(next, next, next);
        Debug.Log($"[{plane}] Scale -> {next:0.00}");
    }

    private void UpdateLabels()
    {
        if (xzLabel != null)
        {
            float xzScale = xzRenderer != null ? xzRenderer.transform.localScale.x : 0f;
            xzLabel.text = $"xz x={xIndex}";
        }

        if (yzLabel != null)
        {
            float yzScale = yzRenderer != null ? yzRenderer.transform.localScale.x : 0f;
            yzLabel.text = $"yz y={yIndex}";
        }
    }

    private IEnumerator LoadXZ()
    {
        var urls = BuildUrls("xz", xIndex);
        yield return FetchAndApply(urls, xzRenderer, "XZ");
    }

    private IEnumerator LoadYZ()
    {
        var urls = BuildUrls("yz", yIndex);
        yield return FetchAndApply(urls, yzRenderer, "YZ");
    }

    private System.Collections.Generic.List<string> BuildUrls(string plane, int index)
    {
        var list = new System.Collections.Generic.List<string>();
        if (serverBases != null)
        {
            foreach (string baseUrl in serverBases)
            {
                if (string.IsNullOrWhiteSpace(baseUrl))
                    continue;
                list.Add($"{baseUrl}/read_slice/{filename}?plane={plane}&index={index}");
            }
        }
        return list;
    }

    private IEnumerator FetchAndApply(System.Collections.Generic.List<string> urls, Renderer target, string label)
    {
        if (target == null)
            yield break;

        if (urls == null || urls.Count == 0)
        {
            Debug.LogError($"[{label}] No server endpoints configured.");
            yield break;
        }

        foreach (string url in urls)
        {
            using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
            {
                // Uncomment if you serve HTTPS with self-signed certs.
                // req.certificateHandler = new AcceptAllCertificatesHandler();

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(req);
                    target.material.mainTexture = tex;
                    target.material.color = Color.white;
                    yield break;
                }

                Debug.LogWarning($"[{label}] Slice fetch failed: {req.error} | {url}");
            }
        }

        Debug.LogError($"[{label}] All slice fetch attempts failed ({urls.Count} endpoint(s)).");
    }
}
