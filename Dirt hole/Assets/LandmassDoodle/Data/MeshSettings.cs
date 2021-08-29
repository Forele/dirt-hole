using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class MeshSettings : UpdatebleData
{
    public const int numSupportedLODs = 5;
    public const int numSupportedChunkSizes = 9;
    public const int numSupportedFlatShadedChunkSizes = 3;
    public static readonly int[] supportedChunkSizes = { 48, 72, 96, 120, 144, 168, 216, 240 };

    public float meshScale = 2.5f;
    public bool useFlatShading;


    [Range(0, numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;

    [Range(0, numSupportedFlatShadedChunkSizes - 1)]
    public int flatShadedchunkSizeIndex;

    // Number of vertaces per line of mesh rendered at LOD = 0.
    // Inclues th 2 extra verts that are excluded from final mesh but used for calculating normals.
    public int numVertsPerLine
    {
        get
        {
            return supportedChunkSizes[(useFlatShading)?flatShadedchunkSizeIndex:chunkSizeIndex] + 5;
        }
    }

    public float meshWorldSize
    {
        get
        {
            return (numVertsPerLine - 3) * meshScale;
        }
    }

}
