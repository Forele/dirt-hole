using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class ChunkDataFetcher
{
    struct StrengthNode
    {
        public Vector3 pos;
        public float strength;
        public float dist;
    }

    public TerrainData terrainData;
    int oneDim = 1;

    public void SetTerrainData(TerrainData _terrainData)
    {
        this.terrainData = _terrainData;
        oneDim = terrainData.segemntCountPerDimension + 1;
    }

    string StringVector(Vector3 vec)
    {
        return (vec.x.ToString() + "_" + vec.y.ToString() + "_" + vec.z.ToString());
    }

    public void DeletePoint(Vector3 point, Vector3 position, Chunk chunk)
    {
        //point += point.normalized * 0.3f;

        Vector3 newRef = point - (position - Vector3.one * chunk.chunkR * terrainData.smallestChunkWidth);


        Vector3 triangleIndex = newRef / (chunk.chunkR * 2f * (terrainData.smallestChunkWidth / (float)terrainData.segemntCountPerDimension));
        var w = terrainData.segemntCountPerDimension + 1;

        Vector3 unrounded = triangleIndex;

        triangleIndex.x = Mathf.RoundToInt(triangleIndex.x);
        triangleIndex.y = Mathf.RoundToInt(triangleIndex.y);
        triangleIndex.z = Mathf.RoundToInt(triangleIndex.z);

        Debug.Log(
            StringVector(point) + " # " + 
            StringVector(position) + " # " +
            StringVector(newRef) + " ## " +
            StringVector(unrounded) + " # " +
            StringVector(triangleIndex)
        );

        List<StrengthNode> strengthNodeList = new List<StrengthNode>();

        for (int x = -4; x <= 4; x++)
        {
            for (int y = -4; y <= 4; y++)
            {
                for (int z = -4; z <= 4; z++)
                {
                    var strengthNode = new StrengthNode();

                    strengthNode.pos = new Vector3(triangleIndex.x + x, triangleIndex.y + y, triangleIndex.z + z);

                    if (
                        strengthNode.pos.x < 0 || strengthNode.pos.y < 0 || strengthNode.pos.z < 0 ||
                        strengthNode.pos.x >= w || strengthNode.pos.y >= w || strengthNode.pos.z >= w
                    )
                    {
                        continue;
                    }


                    strengthNode.strength = chunk.strengths[
                        (int)(strengthNode.pos.x + strengthNode.pos.y * w + strengthNode.pos.z * w * w)
                    ];

                    strengthNode.dist = Vector3.Distance(strengthNode.pos, unrounded);


                    strengthNodeList.Add(strengthNode);
                }
            }
        }

        bool found = false;
        Vector3 targetIndex = new Vector3(-1, -1, -1);
        float minDist = 100f;

        foreach (var item in strengthNodeList)
        {
            if (item.dist < minDist && item.strength > 0.5f)
            {
                found = true;
                targetIndex = item.pos;
                minDist = item.dist;
            }
        }

        if (found)
        {
            chunk.strengths[(int)(targetIndex.x + targetIndex.y * w + targetIndex.z * w * w)] = 0;
        }

        int i = 0;

        foreach (var item in chunk.strengths)
        {
            chunk.chunkNativeData.strengths[i] = item;
            i++;
        }
    }

    public void ScheduleChunkData(Chunk chunk)
    {
        chunk.chunkNativeData.meshMakingHandle = chunk.chunkNativeData.dataGeneration.Schedule(oneDim * oneDim * oneDim, oneDim * oneDim, chunk.chunkNativeData.meshMakingHandle);
    }
}

[BurstCompile]
public struct ChunkGenerator : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float> strengths;
    [ReadOnly] public float3 startPoint;
    [ReadOnly] public int size;

    [ReadOnly] public float seed;
    [ReadOnly] public float stepSize;
    [ReadOnly] public float threshold;
    [ReadOnly] public int chunkDetailMultiplier;
    public Unity.Mathematics.Random random;

    public void Execute(int index)
    {
        int indexCoppy = index;

        int z = indexCoppy / (size * size);
        indexCoppy = indexCoppy % (size * size);
        int y = indexCoppy / size;
        indexCoppy = indexCoppy % size;
        int x = indexCoppy;

        strengths[index] = GetStrengthValue(x, y, z);
    }

    float GetStrengthValue(int x, int y, int z)
    {
        // test for complex chunk
        // return random.NextFloat(0, 1);

        // Test for slow PC
        //float asdasd = 235235;
        //for (int i = 0; i < 100000; i++)
        //{
        //    asdasd = 235235 / 1.1f;
        //}

        float xAbsLoc = (startPoint.x + x * stepSize);
        float yAbsLoc = (startPoint.y + y * stepSize);
        float zAbsLoc = (startPoint.z + z * stepSize);

        // Bubble world test
        //return Perlin3D(
        //    (seed + startPoint.x + x * stepSize) / 200f,
        //    (seed + startPoint.y + y * stepSize) / 200f,
        //    (seed + startPoint.z + z * stepSize) / 200f
        //); // For testing


        //return UnityEngine.Random.Range(0, 1);

        float scale = 90f; // Bigger number - bigger mountins (as if player gets smaller)
        scale = scale * 200.1f;
        float overallScale = 0.4f; // Bigger number - higher frequency
        float foundY = 0;

        float persistance = 0.5f; // 0.2f // [0;1]
        float lecrunarity = 2; // 4 // (0; +inf)
        uint octaves = 6; // 4


        float amplitude = 1; // Bigger means lower valleys and bigger mountains.
        float frequency = 100; // Bigger means more rapid change.

        for (uint i = 0; i < octaves; i++)
        {
            foundY += (Mathf.PerlinNoise
                (
                    (seed + xAbsLoc) / frequency, 
                    (seed + zAbsLoc) / frequency) - 0.5f
                ) * amplitude;

            amplitude *= persistance;
            frequency *= lecrunarity;
        }

        foundY -= 100;

        float range = stepSize;
        var min = foundY - range;
        var max = foundY + range;

        if (yAbsLoc <= min)
        {
            return 1f;
        }

        if (yAbsLoc >= max)
        {
            return 0f;
        }

        return (max - yAbsLoc) / (range * 2);
    }

    public float Perlin3D(float x, float y, float z)
    {
        float xP = Mathf.PerlinNoise(y, z);
        float yP = Mathf.PerlinNoise(x, z);
        float zP = Mathf.PerlinNoise(y, x);

        return getSameProbabilityNoise(xP, yP, zP);
    }

    public float getSameProbabilityNoise(float a, float b, float c = 0.5f)
    {
        float sum = a + b + c;

        sum = sum / 3f;

        if (sum < 0)
        {
            sum = 0f;
        }
        else if (sum > 1)
        {
            sum = 1f;
        }


        //sum = Mathf.Clamp(sum, 0, 1);

        // Equalizes result value probability
        if (0 <= sum && sum <= 0.25f)
        {
            return sum * 4;
        }
        else if (0.25f < sum && sum <= 0.5f)
        {
            return 1 - ((sum - 0.25f) * 4);
        }
        else if (0.5f < sum && sum <= 0.75f)
        {
            return (sum - 0.5f) * 4;
        }
        else if (0.75f < sum && sum <= 1f)
        {
            return 1 - ((sum - 0.75f) * 4);
        }

        throw new Exception("Something invalid with \"getSameProbabilityNoise\" function call");
    }

    public float clamp(float input, int numBase = 10)
    {
        if (input < 0)
        {
            return 0;
        }
        else if (input >= 1)
        {
            return 0.9f * numBase;
        }
        else
        {
            return Mathf.Round(input * 10) * numBase / 10;
        }
    }
}

