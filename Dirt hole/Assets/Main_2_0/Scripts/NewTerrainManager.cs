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
    public TerrainEdit terrainEdit;

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

    bool shown = false; // Fot P to display showableChunks as cubes

    private void Update()
    {
        RenderTerrain();

        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }

        terrainEdit.Update();
    }

    void Shoot()
    {
        RaycastHit hit;
        bool isHit = Physics.Raycast(fpsCam.transform.position, fpsCam.transform.forward, out hit, range, LayerMask.GetMask("Terrain"));

        if (isHit)
        {
            TerrainEdit.DigData digData  = new TerrainEdit.DigData();

            digData.digPos = hit.point;
            digData.chunkPos = hit.transform.position;

            terrainEdit.digRequests.Enqueue(digData);
        }
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

        for (int i = 0; i < terrainData.nativeDataSetCount; i++)
        {
            chunkNativeDataList.Add(new ChunkNativeData(terrainData, marchingCubeInstructions));
        }

        ThreadStart threadStart = delegate
        {
            ChunkWaiteForNativeManager();
        };

        chunkWaiteForNativeThread = new Thread(threadStart);
        chunkWaiteForNativeThread.Start();

        terrainEdit = new TerrainEdit(ref allActiveChunks, terrainData, ref chunkWaiteForNative);
    }

    void RenderTerrain()
    {
        oldPlayerGridLoc = playerGridLoc;
        playerGridLoc = GetPlayerLoc(player);

        //Profiler.BeginSample("MyPieceOfCode");

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
                    var c = allActiveChunks.SingleOrDefault(x => x.position == chunkL);

                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.position = c.position;
                    cube.transform.localScale = Vector3.one * c.chunkR * 2;
                }
            }
        }

        if (chOrder != null && chOrder.MoveNext())
        {
            Chunk chunkToCreate = chOrder.Current;

            if (chunkToCreate.position.y > 0)
            {
                //return;
            }

            Chunk isSet = allActiveChunks.SingleOrDefault(x => x.position == chunkToCreate.position);
            showableChunks.Add(chunkToCreate.position);

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
                    chunk.position = chunkToCreate.position;
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
                chunk.nextStep = FirstChunkRun;

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
                    !showableChunks.Contains(chunk.position)
                )
                {
                    chunk.renderer.enabled = false;
                    chunk.command = Chunk.Command.NotNeeded;
                }
            }
        }

        //Profiler.EndSample();
    }

    //+ Whenever chunk is edied (create chunk, dig chunk)
    void ChunkWaiteForNativeManager()
    {
        while (true)
        {
            if (chunkWaiteForNative.Count > 0)
            {
                Chunk chunk = chunkWaiteForNative.Dequeue();

                if (chunk == null)
                {
                    continue;
                }

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
                            chunk.state = Chunk.State.GotNative;
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
                    chunk.nextStep(chunk);
                });
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }

    //+
    void FirstChunkRun(Chunk chunk)
    {
        ThreadDataRequest.RequestData(() =>
        {
            return chunk;
        }, (object _chunk) =>
        {
            Chunk chunk = (Chunk)_chunk;
            chunk.state = Chunk.State.Making;
            chunk.InitMT(gameObject, terrainData, marchingCubeInstructions, chunkDataFetcher, chunkDataInterpreter);

            ThreadDataRequest.RequestData(() => {
                chunk.InitNMT(terrainData, marchingCubeInstructions);

                return chunk;
            }, AfterAllInits);
        });
    }

    //+
    void OnDestroy()
    {
        foreach (var chunkNativeData in chunkNativeDataList)
        {
            chunkNativeData.DisposeInstances();
        }
    }

    //+ MT
    void AfterAllInits(object _chunk)
    {
        Chunk chunk = (Chunk)_chunk;

        chunkDataFetcher.ScheduleChunkData(chunk);
        chunkDataInterpreter.ScheduleChunkInterpretation(chunk);

        chunk.nextStep = chunk.CreateCollider;
        chunk.WaitForChunkJobs(chunk);
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

public class Chunk
{
    //public struct ChunkPart
    //{
    //    public Vector3 position;
    //    public Vector3 index;
    //    public float[] strengths;
    //}
    
    public enum State { New, WaitingNative, GotNative, Making, Done, Free };

    public enum Command { None, NotNeeded };

    public ChunkNativeData chunkNativeData;

    public Vector3 position;
    public Vector3 subchunkIndex;
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
    //public ChunkPart[,,] chunkParts;
    public ChunkDataFetcher chunkDataFetcher;
    public ChunkDataInterpreter chunkDataInterpreter;

    //public Queue<Chunk> chunkWaiteForNative;
    //public Queue<ChunkPart> smallChunkQueue;

    //public float[,,][] smallChunkStrengths;
    public Action<Chunk> nextStep;

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

    public bool InitMT(
        GameObject pGameObject, 
        TerrainData terrainData, 
        MarchingCubeInstructions marchingCubeInstructions,
        ChunkDataFetcher _chunkDataFetcher,
        ChunkDataInterpreter _chunkDataInterpreter
    )
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
            //chunkWaiteForNative = new Queue<Chunk>();
            //smallChunkQueue = new Queue<ChunkPart>();
            chunkDataFetcher = _chunkDataFetcher;
            chunkDataInterpreter = _chunkDataInterpreter;
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
        int chunkEdgeCount = (chunkR + chunkDataFetcher.extraW) * 2;
        //chunkParts = new ChunkPart[chunkEdgeCount, chunkEdgeCount, chunkEdgeCount];

        //foreach (var item in chunkDataFetcher.GetSubchunks(this))
        //{
        //    smallChunkQueue.Enqueue(item);
        //}
    }

    //public void CollectSmallChunks(Chunk chunk)
    //{
    //    if (smallChunkQueue.Count > 0)
    //    {
    //        var chunkPart = smallChunkQueue.Dequeue();

    //        subchunkIndex = chunkPart.index;

    //        chunkDataFetcher.DoSubchunk(this, chunkPart.position, ShoveStrengthData);
    //    }
    //    else
    //    {
    //        ThreadDataRequest.RequestData(() =>
    //        {
    //            return chunk;
    //        }, (object _chunk) =>
    //        {
    //            Chunk chunk = (Chunk)_chunk;
    //            chunkDataFetcher.PopulateStrengths(this);
    //            nextStep(chunk);
    //        });
    //    }
    //}

    //public void ShoveStrengthData(Chunk chunk)
    //{
    //    chunkParts[(int)subchunkIndex.x, (int)subchunkIndex.y, (int)subchunkIndex.z].strengths = chunkNativeData.strengths.ToArray();
    //    //smallChunkStrengths[(int)subchunkIndex.x, (int)subchunkIndex.y, (int)subchunkIndex.z] = strengths.ToArray();

    //    CollectSmallChunks(chunk);
    //}

    // MT
    public void InterpretStrengths(Chunk chunk)
    {
        chunk.chunkDataInterpreter.ScheduleChunkInterpretation(chunk);
        chunk.nextStep = CreateCollider;
        WaitForChunkJobs(chunk);
    }

    public void WaitForChunkJobs(Chunk chunk, bool pause = false)
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

            if (!chunk.chunkNativeData.meshBakeHandle.IsCompleted)
            {
                WaitForChunkJobs(chunk, true);
            }
            else
            {
                chunk.nextStep(chunk);
            }
        });
    }

    public void CreateCollider(Chunk chunk)
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
            chunk.chunkNativeData.meshBakeHandle.Complete();
            chunk.chunkNativeData.meshMakingHandle.Complete();
            chunk.chunkNativeData.inUse = false;
            chunk.finished = true;
            chunk.state = Chunk.State.Done;
        });
    }
}
