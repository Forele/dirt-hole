using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class ChunkDataFetcher
{
    public struct StrengthNode
    {
        public Vector3 pos;
        public float strength;
        public float dist;
    }

    public int extraW = 1;
    public int oneDim = 1;
    public TerrainData terrainData;
    float smallStepSize;

    public void SetTerrainData(TerrainData _terrainData)
    {
        this.terrainData = _terrainData;
        oneDim = terrainData.segemntCountPerDimension + 1;
        smallStepSize = (terrainData.smallestChunkWidth) / (float)terrainData.segemntCountPerDimension;
    }

    string StringVector(Vector3 vec)
    {
        return (vec.x.ToString() + "_" + vec.y.ToString() + "_" + vec.z.ToString());
    }

    void WaitForSubchunk(Chunk chunk, Action<Chunk> callBack, bool pause = false)
    {
        ThreadDataRequest.RequestData(() =>
        {
            if (pause)
            {
                Thread.Sleep(1);
            }

            return chunk;
        }, (object _chunk) =>
        {
            Chunk chunk = (Chunk)_chunk;

            if (!chunk.chunkNativeData.meshMakingHandle.IsCompleted)
            {
                WaitForSubchunk(chunk, callBack, true);
            }
            else
            {
                chunk.chunkNativeData.meshMakingHandle.Complete();
                callBack(chunk);
            }
        });
    }

    public void ScheduleChunkData(ChunkNativeData chunkNativeData)
    {
        //Debug.Log(oneDim.ToString());
        chunkNativeData.meshMakingHandle = chunkNativeData.dataGeneration.Schedule(oneDim * oneDim * oneDim, oneDim * oneDim, chunkNativeData.meshMakingHandle);
    }
}

//[BurstCompile]
public struct ChunkGenerator : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float> strengths;
    [ReadOnly] public float3 startPoint;
    [ReadOnly] public int size;

    [ReadOnly] public float seed;
    [ReadOnly] public float stepSize;
    [ReadOnly] public float threshold;
    [ReadOnly] public int chunkDetailMultiplier;
    [ReadOnly] public int setSize;
    [ReadOnly] public float strengthTest;
    [ReadOnly] public float testNumber;
    public Unity.Mathematics.Random random;

    public void Execute(int chunkVertexId)
    {
        int indexCoppy = chunkVertexId;

        int z = indexCoppy / (size * size);
        indexCoppy = indexCoppy % (size * size);
        int y = indexCoppy / size;
        indexCoppy = indexCoppy % size;
        int x = indexCoppy;

        //if (x == 0 && y == 0 && z == 0)
        //{
        //    strengths[index] = 0.5961156f;
        //    //strengths[index] = 0.661156f;
        //} else
        //{
        //    strengths[index] = 0;
        //    //strengths[index] = GetStrengthValue(x, y, z);
        //}

        //Debug.Log(GetStrengthValue(x, y, z).ToString());

        strengths[chunkVertexId] = GetStrengthValue(x, y, z);

    }

    float GetStrengthValue(int x, int y, int z)
    {
        // Cubes test
        //if (x == 0 || y == 0 || z == 0 || x == size - 1 || y == size - 1 || z == size - 1)
        //{
        //    return 0.6f;
        //}
        //else
        //{
        //    return strengthTest;
        //}

        //====================
        //float res = z / (float)size - y / (float)size * strengthTest;
        //float change = 5f;

        //res = (res - 0.5f) * testNumber;


        //if (res < 0.0001)
        //{
        //    res = 0f;
        //} else if (res > 0.9999)
        //{
        //    res = 1f;
        //}

        //return res;
        //=======================


        //if (y == x)
        //{

        //}


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


        //float newThing = x * -9.8f * strengthTest + y * 2;

        //if (newThing < 0.0001)
        //{
        //    newThing = 0f;
        //} else if (newThing > 0.9999)
        //{
        //    newThing = 1f;
        //} else
        //{
        //    Debug.Log(newThing.ToString());
        //}

        //return newThing;

        //Debug.Log(stepSize);

        //Debug.Log(
        //    ((x * stepSize) / 100f).ToString() + " " + 
        //    ((y * stepSize) / 100f).ToString() + " " + 
        //    ((z * stepSize) / 100f).ToString());

        // Bubble world test
        //var bubble = Perlin3D(
        //    (seed + startPoint.x + x * stepSize) / 100f,
        //    (seed + startPoint.y + y * stepSize) / 100f,
        //    (seed + startPoint.z + z * stepSize) / 100f
        //); // For testing

        //return bubble * strengthTest;
        //return Mathf.Clamp((bubble - 0.5f) * 2f, 0, 1) * strengthTest;


        //return UnityEngine.Random.Range(0, 1);

        float scale = 90f; // Bigger number - bigger mountins (as if player gets smaller)
        scale = scale * 200.1f;
        float overallScale = 0.4f; // Bigger number - higher frequency
        float foundY = 0;

        float persistance = 0.5f; // 0.2f // [0;1]
        float lecrunarity = 2; // 4 // (0; +inf)
        uint octaves = 6; // 4


        float amplitude = 100; // Bigger means lower valleys and bigger mountains.
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

        foundY -= size / 2f; // While testing shift ground

        float range = 2f;// stepSize + strengthTest; // Something solid instead should be used instead of vague +2
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


        //float partSteps = 4f;
        //return (Mathf.RoundToInt((max - yAbsLoc) / (range * 2) * partSteps)) / partSteps;

        float calcVal = (max - yAbsLoc) / (range * 2);

        return calcVal;
        //return Mathf.Clamp((calcVal - 0.5f) * 2, 0, 1) * strengthTest;
    }

    public float Perlin3D(float x, float y, float z)
    {
        float xP = Mathf.PerlinNoise(y, z);
        float yP = Mathf.PerlinNoise(x, z);
        float zP = Mathf.PerlinNoise(y, x);

        //Debug.Log(getSameProbabilityNoise(xP, yP, zP).ToString());

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

