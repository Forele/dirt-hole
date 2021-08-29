using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public class NewTerrainManager : MonoBehaviour
{
    public TerrainData terrainData;
    public int targetFps = 300;

    ChunkDataFetcher chunkDataFetcher;
    ChunkDataInterpreter chunkDataInterpreter;
    MarchingCubeInstructions marchingCubeInstructions;
    ChunkOrder chunkOrder;
    GameObject player;
    Vector3 playerGridLoc;
    Vector3 oldPlayerGridLoc;
    IEnumerator<Chunk> chOrder;

    List<Chunk> allActiveChunks;
    List<Vector3> showableChunks;
    List<ChunkNativeData> chunkNativeDataList = new List<ChunkNativeData>();

    Queue<Chunk> chunkWaiteForNative = new Queue<Chunk>();
    Thread chunkWaiteForNativeThread;

    public Camera fpsCam;
    int range = 1000;

    void CodeNoteKeeper()
    {
        //Profiler.BeginSample("MyPieceOfCode");
        //Profiler.EndSample();

        //ThreadDataRequest.RequestData(() =>
        //{
        //    return chunk;
        //}, (object _chunk) => {
        //    Chunk chunk = (Chunk)_chunk;
        //
        //});
    }

    void Start()
    {
        RenderTerraneStart();
    }

    bool shown = false;

    private void Update()
    {
        RenderTerrane();

        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        //gameObject.layer = LayerMask.NameToLayer("Terrain");
        RaycastHit hit;
        bool isHit = Physics.Raycast(fpsCam.transform.position, fpsCam.transform.forward, out hit, range, LayerMask.GetMask("Terrain"));

        if (isHit)
        {
            TestMadness(hit.transform.position, hit.triangleIndex, hit.point);
        }
    }

    void TestMadness(Vector3 position, int triangleIndex, Vector3 point)
    {
        Vector3 chunkL;
        chunkL.x = position.x / terrainData.smallestChunkWidth;
        chunkL.y = position.y / terrainData.smallestChunkWidth;
        chunkL.z = position.z / terrainData.smallestChunkWidth;

        Chunk chunk = allActiveChunks.SingleOrDefault(x => x.location == chunkL);

        ThreadDataRequest.RequestData(() =>
        {

            return chunk;
        }, (object _chunk) =>
        {
            Chunk chunk = (Chunk)_chunk;
            chunkDataFetcher.DeletePoint(point, position, chunk);

            chunkDataInterpreter.ScheduleChunkInterpretation(chunk);
            WaitForChunkJobs(chunk);
        });

        //Debug.Log(StringVector(position) + "__" + triangleIndex.ToString() + "__" + StringVector(point).ToString());

        var colors = chunk.mesh.colors;


        colors[triangleIndex * 3] = new Color(1, 0, 0);
        colors[triangleIndex * 3 + 1] = new Color(1, 0, 0);
        colors[triangleIndex * 3 + 2] = new Color(1, 0, 0);

        chunk.mesh.SetColors(colors);
    }

    void RenderTerraneStart()
    {
        Application.targetFrameRate = Mathf.Clamp(targetFps, 10, 1000);
        marchingCubeInstructions = new MarchingCubeInstructions();

        player = GameObject.FindWithTag("Player");
        allActiveChunks = new List<Chunk>();

        chunkOrder = new ChunkOrder();
        chunkOrder.SetUpVariables(terrainData);

        chunkDataFetcher = new ChunkDataFetcher();
        chunkDataFetcher.SetTerrainData(terrainData);

        chunkDataInterpreter = new ChunkDataInterpreter();
        chunkDataInterpreter.SetUpVariables(marchingCubeInstructions, terrainData);

        showableChunks = new List<Vector3>();

        for (int i = 0; i < 8; i++)
        {
            chunkNativeDataList.Add(new ChunkNativeData(terrainData, marchingCubeInstructions));
        }

        ThreadStart threadStart = delegate
        {
            chunkWaiteForNativeManager();
        };

        chunkWaiteForNativeThread = new Thread(threadStart);
        chunkWaiteForNativeThread.Start();
    }

    void RenderTerrane()
    {
        oldPlayerGridLoc = playerGridLoc;
        playerGridLoc = GetPlayerLoc(player);

        //Profiler.BeginSample("MyPieceOfCode");

        // When start at 0,0,0 chunks dont create
        if (oldPlayerGridLoc == null || oldPlayerGridLoc != playerGridLoc)
        {
            chOrder = chunkOrder.GetNextChunk(playerGridLoc).GetEnumerator();
            showableChunks.Clear();
        }

        if (!shown && Input.GetKey(KeyCode.P))
        {
            shown = true;

            foreach (Vector3 chunkL in showableChunks)
            {
                if (chunkL.y < 0)
                {
                    var c = allActiveChunks.SingleOrDefault(x => x.location == chunkL);

                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.position = c.location;
                    cube.transform.localScale = Vector3.one * c.chunkR * 2;
                }
            }
        }

        if (chOrder != null && chOrder.MoveNext())
        {
            Chunk chunkToCreate = chOrder.Current;

            if (chunkToCreate.location.y > 0)
            {
                return;
            }

            Chunk isSet = allActiveChunks.SingleOrDefault(x => x.location == chunkToCreate.location);
            showableChunks.Add(chunkToCreate.location);

            //Debug.Log(StringVector(chunkToCreate.location) + "_" +  chunkToCreate.chunkR);

            if (isSet == null)
            {
                Chunk chunk;

                if (isSet == null)
                {
                    chunk = allActiveChunks.SingleOrDefault(x => x.state == Chunk.State.Free);
                }
                else
                {
                    chunk = isSet;
                }

                if (chunk != null)
                {
                    chunk.location = chunkToCreate.location;
                    chunk.chunkR = chunkToCreate.chunkR;
                }
                else
                {
                    chunk = chunkToCreate;
                    allActiveChunks.Add(chunk);
                }

                chunk.state = Chunk.State.WaitingNative;
                chunk.command = Chunk.Command.None;
                chunk.finished = false;

                chunkWaiteForNative.Enqueue(chunk);
            }
            else
            {
                if (isSet.renderer != null)
                {
                    isSet.renderer.enabled = true;
                }
            }
        }
        else
        {
            foreach (Chunk chunk in allActiveChunks)
            {
                if (
                    chunk.renderer != null &&
                    chunk.renderer.enabled != false &&
                    !showableChunks.Contains(chunk.location)
                )
                {
                    chunk.renderer.enabled = false;
                    chunk.command = Chunk.Command.NotNeeded;
                }
            }
        }

        //Profiler.EndSample();
    }

    void chunkWaiteForNativeManager()
    {
        while (true)
        {
            if (chunkWaiteForNative.Count > 0)
            {
                Chunk chunk = chunkWaiteForNative.Dequeue();

                bool foundFree = false;

                while (!foundFree && chunk.state == Chunk.State.WaitingNative)
                {
                    foreach (var chunkNativeData in chunkNativeDataList)
                    {
                        if (!chunkNativeData.inUse)
                        {
                            chunkNativeData.inUse = true;
                            chunk.chunkNativeData = chunkNativeData;
                            foundFree = true;
                            chunk.state = Chunk.State.Making;
                            break;
                        }
                    }
                }

                ThreadDataRequest.RequestData(() =>
                {
                    return chunk;
                }, (object _chunk) =>
                {
                    Chunk chunk = (Chunk)_chunk;
                    chunk.InitMT(gameObject, terrainData, marchingCubeInstructions);
                    ThreadDataRequest.RequestData(() => CreateChunk(chunk), AfterCreateChunk);
                });
            }
            else
            {
                Thread.Sleep(1000);
            }
        }
    }

    void OnDestroy()
    {
        foreach (var chunkNativeData in chunkNativeDataList)
        {
            chunkNativeData.DisposeInstances();
        }
    }

    Chunk CreateChunk(object _chunk)
    {
        Chunk chunk = (Chunk)_chunk;

        chunk.InitNMT(terrainData, marchingCubeInstructions);

        return chunk;
    }

    // MT
    void AfterCreateChunk(object _chunk)
    {
        Chunk chunk = (Chunk)_chunk;

        chunkDataFetcher.ScheduleChunkData(chunk);
        chunkDataInterpreter.ScheduleChunkInterpretation(chunk);

        WaitForChunkJobs(chunk);
    }

    void WaitForChunkJobs(Chunk chunk, bool pause = false)
    {
        ThreadDataRequest.RequestData(() =>
        {
            if (pause)
            {
                Thread.Sleep(1000);
            }

            return chunk;
        }, (object _chunk) =>
        {
            Chunk chunk = (Chunk)_chunk;

            if (!chunk.chunkNativeData.meshBakeHandle.IsCompleted)
            {
                WaitForChunkJobs(chunk, true);
            }
            else
            {
                CreateCollider(chunk);
            }
        });
    }

    void CreateCollider(Chunk chunk)
    {
        chunk.CompleteJobHandle();
        chunkDataInterpreter.SetChunkData(chunk);

        ThreadDataRequest.RequestData(() =>
        {
            Physics.BakeMesh(chunk.meshInstanceId, false);
            return chunk;
        }, (object _chunk) =>
        {
            Chunk chunk = (Chunk)_chunk;

            chunk.strengths = chunk.chunkNativeData.strengths.ToList();
            chunk.gameObject.GetComponent<MeshCollider>().sharedMesh = chunk.mesh;
            chunk.renderer.enabled = true;
            chunk.chunkNativeData.inUse = false;
            chunk.finished = true;
            chunk.state = Chunk.State.Done;
        });
    }

    string StringVector(Vector3 vec)
    {
        return (vec.x.ToString() + "_" + vec.y.ToString() + "_" + vec.z.ToString());
    }

    Vector3 GetPlayerLoc(GameObject _player)
    {
        int x = Mathf.RoundToInt(_player.transform.position.x / terrainData.smallestChunkWidth);
        int y = Mathf.RoundToInt(_player.transform.position.y / terrainData.smallestChunkWidth);
        int z = Mathf.RoundToInt(_player.transform.position.z / terrainData.smallestChunkWidth);

        if (x == 0) { x += _player.transform.position.x > 0 ? 1 : -1; }
        if (y == 0) { y += _player.transform.position.y > 0 ? 1 : -1; }
        if (z == 0) { z += _player.transform.position.z > 0 ? 1 : -1; }

        

        return new Vector3(x, y, z);
    }
}

public class ChunkNativeData
{
    TerrainData terrainData;
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

    //public int parentChunkClassInstanceId;
    public bool inUse = false;

    bool firstSetUpDone = false;
    const int maxInstructionLength = 15;

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
            dataGeneration.threshold = 0.5f;
            dataGeneration.chunkDetailMultiplier = 1;
            dataGeneration.size = oneDim;
            dataGeneration.random = new Unity.Mathematics.Random(235);

            cubeMarch = new CubeMarch();

            cubeMarch.segemntCountPerDimension = terrainData.segemntCountPerDimension;
            cubeMarch.size = terrainData.segemntCountPerDimension;
            cubeMarch.strengths = strengths;
            cubeMarch.halfPoints = halfPoints;
            cubeMarch.triangleCounts = triangleCounts;
            cubeMarch.threshold = 0.5f;
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
        Vector3 startPoint = chunk.location * terrainData.smallestChunkWidth - Vector3.one * chunk.chunkR * terrainData.smallestChunkWidth;

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

public class Chunk
{
    public enum State { New, WaitingNative, Making, Done, Free };

    public enum Command { None, NotNeeded };

    public ChunkNativeData chunkNativeData;

    public Vector3 location;
    public int chunkR;
    public State state = State.New;
    public Command command = Command.None;
    public GameObject gameObject;
    public Renderer renderer;
    public MeshFilter meshFilter;
    public Mesh mesh;
    public bool firstSetUpDone = false;
    public int meshInstanceId;
    public bool finished = false;

    public List<float> strengths;

    bool CheckForStop()
    {
        if (command == Command.NotNeeded)
        {
            chunkNativeData.inUse = false;
            chunkNativeData = null;
            state = State.Free;
            command = Command.None;

            return true;
        }

        return false;
    }

    public bool InitMT(GameObject pGameObject, TerrainData terrainData, MarchingCubeInstructions marchingCubeInstructions)
    {
        if (!firstSetUpDone)
        {
            gameObject = new GameObject();
            gameObject.layer = LayerMask.NameToLayer("Terrain");
            gameObject.transform.SetParent(pGameObject.transform);
            gameObject.AddComponent<MeshRenderer>();
            gameObject.AddComponent<MeshCollider>();
            meshFilter = gameObject.AddComponent<MeshFilter>();
            renderer = gameObject.GetComponent<Renderer>();
            renderer.material = Resources.Load<Material>("Materials/TerrainMaterial");
            meshFilter.mesh.indexFormat = IndexFormat.UInt32;
            mesh = meshFilter.mesh;
            strengths = new List<float>();
        }

        return chunkNativeData != null;
    }

    public void CompleteJobHandle()
    {
        chunkNativeData.meshMakingHandle.Complete();
    }

    public void InitNMT(TerrainData terrainData, MarchingCubeInstructions marchingCubeInstructions)
    {
        state = State.Making;

        chunkNativeData.SetRunData(this);
    }
}
