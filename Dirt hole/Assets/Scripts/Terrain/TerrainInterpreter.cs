using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using UnityEngine.Rendering;
using UnityEngine.Jobs;
using System;

public class TerrainInterpreter : MonoBehaviour
{
    public Mesh mesh;
    GameObject chunkGameObject;
    Renderer rend;

    [ReadOnly] NativeList<int> instructions;
    [ReadOnly] NativeList<int> edgeNotations;
    [ReadOnly] NativeArray<float3> marchingCube;

    public int allChunkId = -1;

    int nodeXCount;
    int nodeZCount;
    int nodeYCount;

    int maxInstructionLength = 15;
    public Vector3 chunkRefPoint = new Vector3(0, 0, 0);

    public int cubeNodeSize = 100;
    public float threshold = 0.5f;
    public float seed = 0.3f;

    public int id;

    public int chunkDetailMultiplier = 1;
    public int state = 0;

    NativeArray<float3> foundVertaces;
    NativeArray<float3> halfPoints;
    NativeArray<int> triangleCounts;

    public JobHandle bakeJobJob;

    public NativeArray<float> strengths;

    int cubeCount;

    PopulateChunk dataGeneration = new PopulateChunk();
    CubeMarching cubeMarching = new CubeMarching();
    PopulateArrays populateArrays = new PopulateArrays();
    BakeMeshJob bakeJob = new BakeMeshJob();

    public NativeList<uint> triangles = new NativeList<uint>(Allocator.Persistent);
    public NativeList<Vector3> vertices = new NativeList<Vector3>(Allocator.Persistent);
    public NativeList<Color> colors = new NativeList<Color>(Allocator.Persistent);
    public NativeList<Vector3> normals = new NativeList<Vector3>(Allocator.Persistent);

    /// <summary>
    /// Sets up data structures for marching cube calculations.
    /// </summary>
    /// <param name="givenSeed">Random number</param>
    /// <param name="marchingCubeInstructions">Marching cube instructions.</param>
    public void setUpVariables(float givenSeed, MarchingCubeInstructions marchingCubeInstructions)
    {
        seed = givenSeed;

        instructions = marchingCubeInstructions.GetInstructions();
        edgeNotations = marchingCubeInstructions.GetNotations();
        marchingCube = marchingCubeInstructions.GetMarchingCube();

        nodeXCount = cubeNodeSize + 1;
        nodeYCount = cubeNodeSize + 1;
        nodeZCount = cubeNodeSize + 1;

        cubeCount = (nodeXCount - 1) * (nodeYCount - 1) * (nodeZCount - 1);
        strengths = new NativeArray<float>(nodeXCount * nodeYCount * nodeZCount, Allocator.Persistent);
        triangleCounts = new NativeArray<int>(cubeCount, Allocator.Persistent);
        halfPoints = new NativeArray<float3>(cubeCount * 12, Allocator.Persistent);
        foundVertaces = new NativeArray<float3>(cubeCount * maxInstructionLength, Allocator.Persistent);

        SetJobStaticValues();
    }

    public GameObject InitChunkObject()
    {
        chunkGameObject = new GameObject();
        chunkGameObject.transform.SetParent(gameObject.transform);
        chunkGameObject.AddComponent<MeshRenderer>();
        chunkGameObject.AddComponent<MeshCollider>();

        rend = chunkGameObject.GetComponent<Renderer>();
        rend.material = Resources.Load<Material>("Materials/TerrainMaterial");

        var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
        meshFilter.mesh.indexFormat = IndexFormat.UInt32;
        mesh = meshFilter.mesh;

        return chunkGameObject;
    }

    public void setStartData(GameObject go, Mesh givenMesh, Vector3 startRef)
    {
        chunkGameObject = go;
        mesh = givenMesh;
        chunkRefPoint = new Vector3(startRef.x - chunkDetailMultiplier / 2, startRef.y - chunkDetailMultiplier / 2, startRef.z - chunkDetailMultiplier / 2); ;
    }

    public Mesh GetMeshReference()
    {
        return mesh;
    }

    public Renderer GetRendererReference()
    {
        return rend;
    }

    /// <summary>
    /// Defines variables that do change between chunks.
    /// </summary>
    public void SetJobVariables()
    {
        dataGeneration.startPoint = chunkRefPoint * cubeNodeSize;
        dataGeneration.chunkDetailMultiplier = chunkDetailMultiplier;

        populateArrays.startRef = chunkRefPoint * cubeNodeSize;
        populateArrays.chunkDetailMultiplier = chunkDetailMultiplier;
    }

    /// <summary>
    /// Defines variables that do not change between chunks.
    /// </summary>
    public void SetJobStaticValues()
    {
        dataGeneration.startPoint = chunkRefPoint * cubeNodeSize;
        dataGeneration.strengths = strengths;
        dataGeneration.width = nodeXCount;
        dataGeneration.height = nodeYCount;
        dataGeneration.depth = nodeZCount;
        dataGeneration.seed = seed;
        dataGeneration.threshold = threshold;
        dataGeneration.chunkDetailMultiplier = chunkDetailMultiplier;

        cubeMarching.cube = marchingCube;
        cubeMarching.strengths = strengths;
        cubeMarching.halfPoints = halfPoints;
        cubeMarching.triangleCounts = triangleCounts;
        cubeMarching.threshold = threshold;
        cubeMarching.instructions = instructions;
        cubeMarching.edgeNotations = edgeNotations;
        cubeMarching.width = nodeXCount;
        cubeMarching.height = nodeYCount;
        cubeMarching.depth = nodeZCount;
        cubeMarching.vertaces = foundVertaces;

        populateArrays.startRef = chunkRefPoint * cubeNodeSize;
        populateArrays.chunkDetailMultiplier = chunkDetailMultiplier;
        populateArrays.triangles = triangles;
        populateArrays.vertices = vertices;
        populateArrays.colors = colors;
        populateArrays.normals = normals;
        populateArrays.triangleCounts = triangleCounts;
        populateArrays.foundVertaces = foundVertaces;
    }

    JobHandle meshMakingHandle;

    public bool runJobs()
    {
        bakeJob.meshId = mesh.GetInstanceID();
        mesh.Clear();


        meshMakingHandle = dataGeneration.Schedule(nodeXCount * nodeYCount * nodeZCount, nodeXCount * nodeYCount);
        meshMakingHandle = cubeMarching.Schedule(cubeCount, (int)(cubeCount / 10f + 1), meshMakingHandle);
        meshMakingHandle = populateArrays.Schedule(meshMakingHandle);
        // --------------------------------------------------

        return true;
    }

    public bool jobComplete()
    {
        if (meshMakingHandle.IsCompleted)
        {
            meshMakingHandle.Complete();
            
            return true;
        }

        return false;
    }

    public bool UpdateMesh()
    {
        mesh.SetVertices(vertices.AsArray(), 0, vertices.Length);
        mesh.SetIndices(triangles.AsArray(), MeshTopology.Triangles, 0);
        mesh.SetNormals(normals.AsArray(), 0, normals.Length);
        mesh.SetColors(colors.AsArray(), 0, colors.Length);

        bakeJobJob = bakeJob.Schedule();

        return true;
    }

    public void setColiderMesh()
    {
        chunkGameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    }
}

// [BurstCompile]
public struct PopulateArrays : IJob
{
    [ReadOnly] public float3 startRef;
    [ReadOnly] public float chunkDetailMultiplier;

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
                colors.Add(c);

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
                        c = new Color(rand.Next(0, 20) / 100f, 0.8f, rand.Next(0, 20) / 100f, 1f);
                    }
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

[BurstCompile]
public struct PopulateChunk : IJobParallelFor
{
    [NativeDisableParallelForRestriction] public NativeArray<float> strengths;
    [ReadOnly] public float3 startPoint;
    [ReadOnly] public int width;
    [ReadOnly] public int height;
    [ReadOnly] public int depth;

    [ReadOnly] public float seed;
    [ReadOnly] public float threshold;
    [ReadOnly] public int chunkDetailMultiplier;

    public void Execute(int chunkVertexId)
    {
        int indexCoppy = chunkVertexId;

        int z = indexCoppy / (width * height) * chunkDetailMultiplier + (int)startPoint.z;
        indexCoppy = indexCoppy % (width * height);
        int y = indexCoppy / (width) * chunkDetailMultiplier + (int)startPoint.y;
        indexCoppy = indexCoppy % width;
        int x = indexCoppy * chunkDetailMultiplier + (int)startPoint.x;

        strengths[chunkVertexId] = GetStrengthValue(x, y, z);
    }

    float GetStrengthValue(int x, int y, int z)
    {
        float scale = 90f; // Bigger number - bigger mountins (as if player gets smaller)
        scale = scale * 200.1f;
        float overallScale = 0.4f; // Bigger number - higher frequency
        float foundY = 0;

        float persistance = 0.5f; // 0.2f // [0;1]
        float lecrunarity = 2; // 4 // (0; +inf)
        uint octaves = 6; // 4


        float amplitude = 1; // Bigger means lower valleys and bigger mountains.
        float frequency = 1; // Bigger means more rapid change.

        // return y < 0 ? 1 : 0;

        if (false)
        {
            return Perlin3D(
                (x + seed) / scale * 80,
                (y + seed) / scale * 120,
                (z + seed) / scale * 160
            );
        }

        for (uint i = 0; i < octaves; i++)
        {
            foundY += (
                (Mathf.PerlinNoise(
                    (x + seed) / scale * (overallScale * frequency),
                    (z + seed) / scale * (overallScale * frequency)
                ) - 0.5f) * scale * amplitude
            );

            amplitude *= persistance;
            frequency *= lecrunarity;
        }

        y = y / chunkDetailMultiplier;
        foundY = foundY / chunkDetailMultiplier;

        float range = 1f;
        var min = foundY - range;
        var max = foundY + range;

        if (y <= min)
        {
            return 1f;
        }

        if (y >= max)
        {
            return 0f;
        }

        return (max - y) / (range * 2);
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
        } else if (sum > 1)
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

[BurstCompile]
public struct BakeMeshJob : IJob
{
    [ReadOnly] public int meshId;

    public void Execute()
    {
        Physics.BakeMesh(meshId, false);
    }
}

[BurstCompile]
public struct CubeMarching : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> cube;
    [ReadOnly] public NativeArray<float> strengths;
    [NativeDisableParallelForRestriction] public NativeArray<float3> halfPoints;
    [NativeDisableParallelForRestriction] public NativeArray<int> triangleCounts;
    [NativeDisableParallelForRestriction] public NativeArray<float3> vertaces;

    [ReadOnly] public float threshold;
    [ReadOnly] public NativeList<int> instructions;
    [ReadOnly] public NativeList<int> edgeNotations;
    [ReadOnly] public int width;
    [ReadOnly] public int height;
    [ReadOnly] public int depth;

    public void Execute(int index)
    {
        int indexCoppy = index;

        int z = indexCoppy / ((width - 1) * (height - 1));
        indexCoppy = indexCoppy % ((width - 1) * (height - 1));
        int y = indexCoppy / (width - 1);
        indexCoppy = indexCoppy % (width - 1);
        int x = indexCoppy;

        int caseNumber = 0;

        var vertexOffset = new float3(x, y, z);

        for (int zz = 0; zz < 2; zz++)
        {
            for (int yy = 0; yy < 2; yy++)
            {
                for (int xx = 0; xx < 2; xx++)
                {
                    var strength = strengths[x + xx + ((y + yy) * width) + ((z + zz) * (width * height))];
                    caseNumber += strength > threshold ? (int)math.pow(2f, xx + yy * 2 + zz * 4) : 0;
                }
            }
        }

        triangleCounts[index] = 0;

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
                var pOneSt = strengths[x + (int)pOne.x + ((y + (int)pOne.y) * width) + ((z + (int)pOne.z) * (width * height))];

                var pTwo = cube[edgeNotations[instructions[i] * 2 + 1]];
                var pTwoSt = strengths[x + (int)pTwo.x + ((y + (int)pTwo.y) * width) + ((z + (int)pTwo.z) * (width * height))];

                halfPoints[index * 12 + instructions[i]] = GetBetweenPoint(pOne, pTwo, pOneSt, pTwoSt);
            }

            triangleCounts[index]++;
            vertaces[(index * 15) + i - (caseNumber * 15)] = (vertexOffset + halfPoints[index * 12 + instructions[i]]);
        }
    }

    Vector3 GetBetweenPoint(float3 pOne, float3 pTwo, float pOneSt, float pTwoSt)
    {
        bool strongIsOne = true;
        float strong = 0;
        float week = 0;
        float fromWeek = 0;

        if (pOneSt > threshold)
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

        fromWeek = strongSide / (weekSide + strongSide);

        if (strongIsOne)
        {
            return (pTwo - pOne) * fromWeek + pOne;
        }
        else
        {
            return (pOne - pTwo) * fromWeek + pTwo;
        }
    }
}
