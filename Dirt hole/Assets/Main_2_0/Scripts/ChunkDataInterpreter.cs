using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class ChunkDataInterpreter
{
    MarchingCubeInstructions marchingCubeInstructions;
    public TerrainData terrainData;

    NativeArray<float> strengths = new NativeList<float>(Allocator.Persistent);

    int maxInstructionLength = 15;
    int cubeCount;
    int batchCount;

    public void SetUpVariables(MarchingCubeInstructions _marchingCubeInstructions, TerrainData _terrainData)
    {
        marchingCubeInstructions = _marchingCubeInstructions;
        terrainData = _terrainData;

        int cubesPerLine = terrainData.segemntCountPerDimension;
        batchCount = (int)(cubeCount / 10f + 1);
        cubeCount = cubesPerLine * cubesPerLine * cubesPerLine;
    }

    public void ScheduleChunkInterpretation(ChunkNativeData chunkNativeData)
    {
        chunkNativeData.meshMakingHandle = chunkNativeData.cubeMarch.Schedule(cubeCount, batchCount, chunkNativeData.meshMakingHandle);
        chunkNativeData.meshMakingHandle = chunkNativeData.populate.Schedule(chunkNativeData.meshMakingHandle);
    }

    public void SetChunkData(Chunk chunk)
    {
        //var b = terrainData.smallestChunkWidth;
        chunk.mesh.Clear();
        //var a = chunk.gameObject.transform.position;

        //var c = chunk.position;
        chunk.gameObject.transform.position = terrainData.smallestChunkWidth * chunk.position;
        chunk.gameObject.transform.localScale = Vector3.one * 2 * terrainData.smallestChunkWidth * chunk.chunkR;

        // Debug.Log(chunk.chunkNativeData.vertices.Length.ToString());

        chunk.mesh.SetVertices(chunk.chunkNativeData.vertices.AsArray(), 0, chunk.chunkNativeData.vertices.Length);
        chunk.mesh.SetIndices(chunk.chunkNativeData.triangles.AsArray(), MeshTopology.Triangles, 0);
        chunk.mesh.SetNormals(chunk.chunkNativeData.normals.AsArray(), 0, chunk.chunkNativeData.normals.Length);
        chunk.mesh.SetColors(chunk.chunkNativeData.colors.AsArray(), 0, chunk.chunkNativeData.colors.Length);

    }
}

//[BurstCompile]
public struct Populate : IJob
{
    [ReadOnly] public float3 startRef;
    [ReadOnly] public float chunkDetailMultiplier;
    [ReadOnly] public float segemntCountPerDimension;

    public NativeList<uint> triangles;
    public NativeList<Vector3> vertices;
    public NativeList<Color> colors;
    public NativeList<Vector3> normals;

    [ReadOnly] public NativeArray<int> triangleCounts;
    [ReadOnly] public NativeArray<float3> foundVertaces;

    Vector3 calculatedNormal;
    float3 u;
    float3 v;
    float3 p1;
    float3 p2;
    float3 p3;

    public void Execute()
    {
        populateTrianglesAndVertaces();
    }

    public void populateTrianglesAndVertaces()
    {
        // startRef = new float3(0, 0, 0);
        triangles.Clear();
        vertices.Clear();
        colors.Clear();
        normals.Clear();

        var rand = new System.Random();

        //Chunking test
        bool chunkTestColors = false;

        Color c = new Color(rand.Next(0, 20) / 100f, 0.8f, rand.Next(0, 20) / 100f, 1f);

        if (chunkTestColors)
        {
            c = new Color(rand.Next(1, 99) / 100f, rand.Next(1, 99) / 100f, rand.Next(1, 99) / 100f);
        }

        for (int cubeNumber = 0; cubeNumber < triangleCounts.Length; cubeNumber++)
        {

            for (int i = 0; i < triangleCounts[cubeNumber]; i++)
            {
                triangles.Add(Convert.ToUInt32(vertices.Length));
                vertices.Add((startRef + foundVertaces[cubeNumber * 15 + i] * chunkDetailMultiplier));

                if ((i + 1) % 3 == 0)
                {
                    p1 = startRef + foundVertaces[cubeNumber * 15 + i - 2];
                    p2 = startRef + foundVertaces[cubeNumber * 15 + i - 1];
                    p3 = startRef + foundVertaces[cubeNumber * 15 + i - 0];
                    addNormals(p1, p2, p3);
                    addNormals(p2, p3, p1);
                    addNormals(p3, p1, p2);

                    if (!chunkTestColors)
                    {
                        var dfgdfg = (startRef + foundVertaces[cubeNumber * 15 + i] * chunkDetailMultiplier);
                        float seed = dfgdfg.x + dfgdfg.y + dfgdfg.z;
                        rand = new System.Random((int)seed);
                        c = new Color(rand.Next(0, 20) / 100f, 0.8f, rand.Next(0, 20) / 100f, 1f);
                    }

                    //colors.Add(new Color(1, 1, 1));
                    colors.Add(c);
                }
                else
                {
                    colors.Add(c);
                }
            }
        }
    }

    /// <summary>
    /// Normal calculation from  https://www.khronos.org/opengl/wiki/Calculating_a_Surface_Normal
    /// </summary>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <param name="p3"></param>
    private void addNormals(float3 p1, float3 p2, float3 p3)
    {
        calculatedNormal = new Vector3();
        u = p2 - p1;
        v = p3 - p1;

        calculatedNormal.x = u.y * v.z - u.z * v.y;
        calculatedNormal.y = u.z * v.x - u.x * v.z;
        calculatedNormal.z = u.x * v.y - u.y * v.x;

        normals.Add(calculatedNormal.normalized);
    }
}

//[BurstCompile]
public struct CubeMarch : IJobParallelFor
{
    [ReadOnly] public float segemntCountPerDimension;
    [ReadOnly] public NativeArray<float3> cube;
    [ReadOnly] public NativeArray<float> strengths;
    [ReadOnly] public float threshold;
    [ReadOnly] public NativeList<int> instructions;
    [ReadOnly] public NativeList<int> edgeNotations;
    [ReadOnly] public int size;

    [ReadOnly] public float testNumber;

    [NativeDisableParallelForRestriction] public NativeArray<float3> halfPoints;
    [NativeDisableParallelForRestriction] public NativeArray<int> triangleCounts; // Probably should be vertex count
    [NativeDisableParallelForRestriction] public NativeArray<float3> vertaces;

    public void Execute(int chunkCubeNr)
    {
        int indexCoppy = chunkCubeNr;

        int z = indexCoppy / (size * size);
        indexCoppy = indexCoppy % (size * size);
        int y = indexCoppy / size;
        indexCoppy = indexCoppy % size;
        int x = indexCoppy;

        int caseNumber = 0;

        var vertexOffset = new float3(x, y, z);

        // Debug.Log(threshold.ToString());

        for (int zz = 0; zz < 2; zz++)
        {
            for (int yy = 0; yy < 2; yy++)
            {
                for (int xx = 0; xx < 2; xx++)
                {
                    var strength = strengths[x + xx + ((y + yy) * (size + 1)) + ((z + zz) * ((size + 1) * (size + 1)))];
                    //strength = GetSteppedStrength(strength);
                    caseNumber += true && strength >= threshold ? (int)math.pow(2f, xx + yy * 2 + zz * 4) : 0;
                }
            }
        }

        triangleCounts[chunkCubeNr] = 0;

        if (caseNumber == 0 || caseNumber == 256)
        {
            return;
        }

        int setpoints = 0;
        int refVal = 0;

        for (int i = caseNumber * 15; i < caseNumber * 15 + 15; i++)
        {
            if (instructions[i] < 0)
            {
                break;
            }

            refVal = (int)math.pow(2, instructions[i] + 1) | setpoints;

            if (refVal != setpoints)
            {
                setpoints = refVal;
                var pOne = cube[edgeNotations[instructions[i] * 2 + 0]];
                var pOneSt = strengths[x + (int)pOne.x + ((y + (int)pOne.y) * (size + 1)) + ((z + (int)pOne.z) * ((size + 1) * (size + 1)))];

                var pTwo = cube[edgeNotations[instructions[i] * 2 + 1]];
                var pTwoSt = strengths[x + (int)pTwo.x + ((y + (int)pTwo.y) * (size + 1)) + ((z + (int)pTwo.z) * ((size + 1) * (size + 1)))];

                halfPoints[chunkCubeNr * 12 + instructions[i]] = GetBetweenPoint(pOne, pTwo, pOneSt, pTwoSt);
            }

            triangleCounts[chunkCubeNr]++;
            vertaces[(chunkCubeNr * 15) + i - (caseNumber * 15)] = (vertexOffset + halfPoints[chunkCubeNr * 12 + instructions[i]]) / segemntCountPerDimension;
        }
        
        int lastOverwrite = 0;
        int removedPointCount = 0;

        for (int i = (chunkCubeNr * 15); i < (chunkCubeNr * 15) + triangleCounts[chunkCubeNr]; i += 3)
        {
            if (isInvalidTriangle(vertaces[i], vertaces[i + 1], vertaces[i + 2]))
            {
                //Debug.Log(vertaces[i].x + "_" + vertaces[i].y + "_" + vertaces[i].z + "###" + vertaces[i + 1].x + "_" + vertaces[i + 1].y + "_" + vertaces[i + 1].z + "###" + vertaces[i + 2].x + "_" + vertaces[i + 2].y + "_" + vertaces[i + 2].z);
                //triangleCounts[index] -= 3;
                removedPointCount -= 3;
            }
            else
            {
                //Debug.Log(vertaces[i].x.ToString() + "_" + vertaces[i].y.ToString() + "_" + vertaces[i].z.ToString() + "###" + vertaces[i + 1].x.ToString() + "_" + vertaces[i + 1].y.ToString() + "_" + vertaces[i + 1].z.ToString() + "###" + vertaces[i + 2].x.ToString() + "_" + vertaces[i + 2].y.ToString() + "_" + vertaces[i + 2].z.ToString());

                vertaces[(chunkCubeNr * 15) + lastOverwrite] = vertaces[i];
                vertaces[(chunkCubeNr * 15) + lastOverwrite + 1] = vertaces[i + 1];
                vertaces[(chunkCubeNr * 15) + lastOverwrite + 2] = vertaces[i + 2];
                lastOverwrite += 3;
            }
        }

        triangleCounts[chunkCubeNr] += removedPointCount;
    }

    bool isInvalidTriangle(float3 p1, float3 p2, float3 p3)
    {
        return sameFloat(p1, p2) || sameFloat(p2, p3) || sameFloat(p3, p1);

        //var AB = p2 - p1;
        //var AC = p3 - p1;

        //Debug.Log(AB.x + "_" + AB.y + "_" + AB.z + "###" + AC.x + "_" + AC.y + "_" + AC.z);

        //return AB.x / AC.x == AB.x / AC.x && AB.y / AC.y == AB.y / AC.y && AB.z / AC.z == AB.z / AC.z;
    }

    bool sameFloat(float3 p1, float3 p2)
    {
        var p = p1 - p2;

        //Debug.Log("?" + p.x.ToString() + p.y.ToString() + p.z.ToString());

        return Math.Abs(p.x) < 0.0001 && Math.Abs(p.y) < 0.0001 && Math.Abs(p.z) < 0.0001;
    }

    float GetSteppedStrength(float inp)
    {
        //return 0.6f;
        return inp;

        float stepCount = 4f;

        float res = 0;
        //inp = Mathf.Round(inp * 1024f) / 1024f;

        //res = (Mathf.Round(inp * stepCount)) / stepCount;

        inp = (inp + 1f / stepCount / 2f);
        res = (Mathf.Round(inp * stepCount)) / stepCount;



        if (res < 0.0001)
        {
            return 0;
        } else if (res > 0.9999)
        {
            return 1;
        }

        return res;
    }

    // Values from 0 to 1 as strong
    Vector3 GetBetweenPoint2(float3 pOne, float3 pTwo, float pOneSt, float pTwoSt)
    {
        //bool strongIsOne = true;
        float3 strong = 0;
        float3 week = 0;
        //float fromWeek = 0;
        float strongStrength = 0;
        //float relativeStr = 0;

        if (pOneSt >= threshold)
        {
            strongStrength = pOneSt;
            //relativeStr = pOneSt - pTwoSt;
            strong = pOne;
            week = pTwo;
        }
        else
        {
            strongStrength = pTwoSt;
            //relativeStr = pTwoSt - pOneSt;
            strong = pTwo;
            week = pOne;
        }


        //strongStrength = relativeStr;
        strongStrength = GetSteppedStrength(strongStrength);

        return ((week - strong) * strongStrength) + strong;
    }

    // Values from 0.5 to 1 as strong
    Vector3 GetBetweenPoint(float3 pOne, float3 pTwo, float pOneSt, float pTwoSt)
    {
        //pOneSt = GetSteppedStrength(pOneSt);
        //pTwoSt = GetSteppedStrength(pTwoSt);

        bool strongIsOne = true;
        float strong = 0;
        float week = 0;
        float fromWeek = 0;

        if (pOneSt >= threshold)
        {
            strongIsOne = true;
            strong = pOneSt;
            week = pTwoSt;
        }
        else
        {
            strongIsOne = false;
            strong = pTwoSt;
            week = pOneSt;
        }

        float weekSide = threshold - week;
        float strongSide = strong - threshold;

        fromWeek = GetSteppedStrength(strongSide / (weekSide + strongSide));

        //Debug.Log(fromWeek.ToString());

        Vector3 betweenPoint; 

        if (strongIsOne)
        {
            betweenPoint = (pTwo - pOne) * fromWeek + pOne;
        }
        else
        {
            betweenPoint = (pOne - pTwo) * fromWeek + pTwo;
        }

        return betweenPoint;
    }
}

[BurstCompile]
public struct BakeMesh : IJob
{
    [ReadOnly] public int meshId;

    public void Execute()
    {
        Physics.BakeMesh(meshId, false);
    }
}
