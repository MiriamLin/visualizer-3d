using UnityEngine;

public class TiffSlicer : MonoBehaviour
{
    private TiffData tiffData;

    public void LoadTiffFile(string filePath)
    {
        Debug.LogWarning("LoadTiffFile is not implemented for local files with the new TiffReader. Use LoadTiffFromData instead.");
    }

    public void LoadTiffFromData(byte[] tiffRawData)
    {
        tiffData = TiffReader.LoadTiff(tiffRawData);
        if (tiffData != null)
        {
            Debug.Log("Successfully loaded TIFF data from memory.");
        }
        else
        {
            Debug.LogError("Failed to load TIFF data from memory.");
        }
    }

    public byte[] GetSlice(string plane, int sliceIndex)
    {
        if (tiffData == null)
        {
            Debug.LogError("TIFF data not loaded. Call LoadTiffFile first.");
            return null;
        }

        plane = plane.ToLower();

        if (plane == "xz")
        {
            return GetSliceXZ(sliceIndex);
        }
        else if (plane == "yz")
        {
            return GetSliceYZ(sliceIndex);
        }
        else
        {
            Debug.LogError("Invalid plane specified. Use 'xz' or 'yz'.");
            return null;
        }
    }

    private byte[] GetSliceXZ(int y)
    {
        if (y < 0 || y >= tiffData.height)
        {
            Debug.LogError("Y slice index out of bounds.");
            return null;
        }

        Texture2D slice = new Texture2D(tiffData.width, tiffData.depth);
        Color[] slicePixels = new Color[tiffData.width * tiffData.depth];

        for (int z = 0; z < tiffData.depth; z++)
        {
            for (int x = 0; x < tiffData.width; x++)
            {
                slicePixels[z * tiffData.width + x] = tiffData.pixels[z * tiffData.width * tiffData.height + y * tiffData.width + x];
            }
        }

        slice.SetPixels(slicePixels);
        slice.Apply();
        byte[] pngData = slice.EncodeToPNG();
        Destroy(slice);
        return pngData;
    }

    private byte[] GetSliceYZ(int x)
    {
        if (x < 0 || x >= tiffData.width)
        {
            Debug.LogError("X slice index out of bounds.");
            return null;
        }

        Texture2D slice = new Texture2D(tiffData.height, tiffData.depth);
        Color[] slicePixels = new Color[tiffData.height * tiffData.depth];

        for (int z = 0; z < tiffData.depth; z++)
        {
            for (int y = 0; y < tiffData.height; y++)
            {
                slicePixels[z * tiffData.height + y] = tiffData.pixels[z * tiffData.width * tiffData.height + y * tiffData.width + x];
            }
        }

        slice.SetPixels(slicePixels);
        slice.Apply();
        byte[] pngData = slice.EncodeToPNG();
        Destroy(slice);
        return pngData;
    }
}
