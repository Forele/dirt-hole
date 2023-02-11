using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class ChunkNativeData
{
    public TerrainData terrainData;
    MarchingCubeInstructions marchingCubeInstructions;

    public NativeArray<float> strengths;
    public NativeArray<float3> foundVertaces;
    public NativeArray<float3> halfPoints;
    public NativeArray<int> triangleCounts;

    public NativeList<uint> triangles;
    public NativeList<Vector3> vertices;
    public NativeList<Color> colors;
    public NativeList<Vector3> normals;

    public ChunkGenerator dataGeneration;
    public CubeMarch cubeMarch;
    public Populate populate;

    public JobHandle meshBakeHandle;
    public JobHandle meshMakingHandle;

    public bool inUse = false;

    bool firstSetUpDone = false;
    const int maxInstructionLength = 15;

    public int userCount = 0;

    public ChunkNativeData(TerrainData _terrainData, MarchingCubeInstructions _marchingCubeInstructions)
    {
        terrainData = _terrainData;
        marchingCubeInstructions = _marchingCubeInstructions;
        SetUpInstances();
    }

    public void SetUpInstances()
    {
        if (!firstSetUpDone)
        {
            int cubesPerLine = terrainData.segemntCountPerDimension;
            int cubeCount = cubesPerLine * cubesPerLine * cubesPerLine;
            int oneDim = terrainData.segemntCountPerDimension + 1;

            //Debug.Log(oneDim.ToString());

            strengths = new NativeArray<float>(oneDim * oneDim * oneDim, Allocator.Persistent);
            triangleCounts = new NativeArray<int>(cubeCount, Allocator.Persistent);
            halfPoints = new NativeArray<float3>(cubeCount * 12, Allocator.Persistent);
            foundVertaces = new NativeArray<float3>(cubeCount * maxInstructionLength, Allocator.Persistent);

            triangles = new NativeList<uint>(Allocator.Persistent);
            vertices = new NativeList<Vector3>(Allocator.Persistent);
            colors = new NativeList<Color>(Allocator.Persistent);
            normals = new NativeList<Vector3>(Allocator.Persistent);

            dataGeneration = new ChunkGenerator();

            dataGeneration.strengths = strengths;
            dataGeneration.threshold = terrainData.threshold;
            dataGeneration.chunkDetailMultiplier = 1;
            dataGeneration.size = oneDim;
            dataGeneration.random = new Unity.Mathematics.Random(235);
            dataGeneration.testNumber = 1;

            cubeMarch = new CubeMarch();

            cubeMarch.segemntCountPerDimension = terrainData.segemntCountPerDimension;
            cubeMarch.size = terrainData.segemntCountPerDimension;
            cubeMarch.strengths = strengths;
            cubeMarch.halfPoints = halfPoints;
            cubeMarch.triangleCounts = triangleCounts;
            cubeMarch.threshold = terrainData.threshold;
            cubeMarch.cube = marchingCubeInstructions.GetMarchingCube();
            cubeMarch.instructions = marchingCubeInstructions.GetInstructions();
            cubeMarch.edgeNotations = marchingCubeInstructions.GetNotations();
            cubeMarch.vertaces = foundVertaces;

            populate = new Populate();

            float newChunkScale = terrainData.segemntCountPerDimension / -2f;
            populate.startRef = new Vector3(newChunkScale, newChunkScale, newChunkScale) / terrainData.segemntCountPerDimension;
            populate.chunkDetailMultiplier = 1;
            populate.triangles = triangles;
            populate.vertices = vertices;
            populate.colors = colors;
            populate.normals = normals;
            populate.triangleCounts = triangleCounts;
            populate.foundVertaces = foundVertaces;
            populate.segemntCountPerDimension = terrainData.segemntCountPerDimension;

            firstSetUpDone = true;
        }
    }

    public void SetRunData(Chunk chunk)
    {
        if (!firstSetUpDone)
        {
            SetUpInstances();
        }

        float stepSize = (terrainData.smallestChunkWidth * (chunk.chunkR * 2)) / (float)terrainData.segemntCountPerDimension;

        //Debug.Log(terrainData.smallestChunkWidth.ToString());
        //Debug.Log(chunk.chunkR.ToString());
        //Debug.Log(terrainData.segemntCountPerDimension.ToString());
        //Debug.Log("=================================");

        Vector3 startPoint = chunk.position * terrainData.smallestChunkWidth - Vector3.one * chunk.chunkR * terrainData.smallestChunkWidth;

        dataGeneration.startPoint = startPoint;
        dataGeneration.stepSize = stepSize;
        dataGeneration.seed = terrainData.seed;
    }

    public void DisposeInstances()
    {
        meshBakeHandle.Complete();
        meshMakingHandle.Complete();

        strengths.Dispose();
        foundVertaces.Dispose();
        halfPoints.Dispose();
        triangleCounts.Dispose();

        triangles.Dispose();
        vertices.Dispose();
        colors.Dispose();
        normals.Dispose();
    }
}
