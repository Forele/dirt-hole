using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class TerrainData : ScriptableObject
{
    public int smallestChunkWidth; // Length
    public int segemntCountPerDimension; // Triangle count in chunk
    public int seed;
    public int newChunkR;
    public int nativeDataSetCount;
}
