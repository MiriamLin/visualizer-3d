using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class TiffData
{
    public Color[] pixels;
    public int width;
    public int height;
    public int depth;
}

public static class TiffReader
{
    public static TiffData LoadTiff(byte[] tiffBytes)
    {
        if (tiffBytes == null || tiffBytes.Length < 8)
        {
            Debug.LogError("Invalid TIFF data.");
            return null;
        }

        using (MemoryStream ms = new MemoryStream(tiffBytes))
        {
            using (BinaryReader reader = new BinaryReader(ms))
            {
                // Read TIFF Header
                char byteOrder1 = (char)reader.ReadByte();
                char byteOrder2 = (char)reader.ReadByte();
                bool isLittleEndian = (byteOrder1 == 'I' && byteOrder2 == 'I');

                if (!isLittleEndian && !(byteOrder1 == 'M' && byteOrder2 == 'M'))
                {
                    Debug.LogError("Invalid TIFF byte order marks.");
                    return null;
                }

                ushort magicNumber = ReadUInt16(reader, isLittleEndian);
                if (magicNumber != 42)
                {
                    Debug.LogError("Invalid TIFF magic number.");
                    return null;
                }

                uint ifdOffset = ReadUInt32(reader, isLittleEndian);

                List<Color> allPixels = new List<Color>();
                int width = 0, height = 0, depth = 0;

                // Read all IFDs (pages)
                while (ifdOffset != 0)
                {
                    reader.BaseStream.Seek(ifdOffset, SeekOrigin.Begin);
                    ushort numEntries = ReadUInt16(reader, isLittleEndian);
                    
                    uint imageWidth = 0, imageHeight = 0, bitsPerSample = 0, compression = 1;
                    uint stripOffsets = 0, rowsPerStrip = 0, stripByteCounts = 0;
                    
                    for (int i = 0; i < numEntries; i++)
                    {
                        ushort tag = ReadUInt16(reader, isLittleEndian);
                        ushort type = ReadUInt16(reader, isLittleEndian);
                        uint count = ReadUInt32(reader, isLittleEndian);
                        uint valueOffset = ReadUInt32(reader, isLittleEndian);

                        long currentPos = reader.BaseStream.Position;

                        switch (tag)
                        {
                            case 256: // ImageWidth
                                imageWidth = valueOffset;
                                break;
                            case 257: // ImageLength
                                imageHeight = valueOffset;
                                break;
                            case 258: // BitsPerSample
                                bitsPerSample = valueOffset;
                                break;
                            case 259: // Compression
                                compression = valueOffset;
                                break;
                            case 273: // StripOffsets
                                stripOffsets = valueOffset;
                                break;
                            case 278: // RowsPerStrip
                                rowsPerStrip = valueOffset;
                                break;
                            case 279: // StripByteCounts
                                stripByteCounts = valueOffset;
                                break;
                        }
                        reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
                    }

                    if (width == 0 && height == 0)
                    {
                        width = (int)imageWidth;
                        height = (int)imageHeight;
                    }

                    if (compression != 1)
                    {
                        Debug.LogError("Only uncompressed TIFFs are supported.");
                        return null;
                    }

                    if (bitsPerSample != 8)
                    {
                        Debug.LogError("Only 8-bit grayscale TIFFs are supported.");
                        return null;
                    }

                    reader.BaseStream.Seek(stripOffsets, SeekOrigin.Begin);
                    byte[] stripData = reader.ReadBytes((int)stripByteCounts);

                    for (int j = 0; j < stripData.Length; j++)
                    {
                        float val = stripData[j] / 255.0f;
                        allPixels.Add(new Color(val, val, val));
                    }
                    
                    depth++;
                    ifdOffset = ReadUInt32(reader, isLittleEndian);
                }
                
                return new TiffData { pixels = allPixels.ToArray(), width = width, height = height, depth = depth };
            }
        }
    }

    private static ushort ReadUInt16(BinaryReader reader, bool isLittleEndian)
    {
        byte[] bytes = reader.ReadBytes(2);
        if (isLittleEndian != BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        return BitConverter.ToUInt16(bytes, 0);
    }

    private static uint ReadUInt32(BinaryReader reader, bool isLittleEndian)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (isLittleEndian != BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        return BitConverter.ToUInt32(bytes, 0);
    }
}