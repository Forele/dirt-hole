using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/TerrainData")]
public class TerrainData : ScriptableObject
{
    public int smallestChunkWidth; // Length
    public int segemntCountPerDimension; // Triangle count in chunk
    public int seed;
    public int newChunkR;
    public int nativeDataSetCount; // Data generation instances (paralel mesh calculators)
    public float threshold; // Data generation instances (paralel mesh calculators)
}
