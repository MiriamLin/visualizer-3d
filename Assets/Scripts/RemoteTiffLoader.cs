using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[RequireComponent(typeof(TiffSlicer))]
public class RemoteTiffLoader : MonoBehaviour
{
    [Tooltip("The URL of the .tif file to download.")]
    public string tiffUrl = "https://your-server.com/path/to/your/file.tif";

    private TiffSlicer tiffSlicer;

    void Start()
    {
        tiffSlicer = GetComponent<TiffSlicer>();
        
        StartCoroutine(DownloadAndSliceTiff());
    }

    private IEnumerator DownloadAndSliceTiff()
    {
        Debug.Log($"Starting download from: {tiffUrl}");

        UnityWebRequest www = UnityWebRequest.Get(tiffUrl);
        www.certificateHandler = new AcceptAllCertificatesHandler();
        www.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (HTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");
        
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Failed to download TIFF file: {www.error}");
        }
        else
        {
            Debug.Log("Download complete!");
            
            byte[] tiffData = www.downloadHandler.data;

            byte[] pngDataXZ = tiffData;
            if (pngDataXZ != null)
            {
                System.IO.File.WriteAllBytes("remote_slice.png", pngDataXZ);
                Debug.Log("Saved remote XZ slice to remote_slice_xz_128.png");
            }
            /*
            byte[] pngDataYZ = tiffData;//.GetSlice("yz", 64);
            if (pngDataYZ != null)
            {
                System.IO.File.WriteAllBytes("remote_slice_yz_64.png", pngDataYZ);
                Debug.Log("Saved remote YZ slice to remote_slice_yz_64.png");
            }
            */
        }
    }
}
