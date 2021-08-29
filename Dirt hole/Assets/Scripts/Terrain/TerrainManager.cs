using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

class ChunkData
{
    public GameObject go;
    public Mesh mesh;

    /// <summary>
    /// 0 - ready to be reused
    /// 1 - is finished rendering
    /// 2 - needs to be removed
    /// 3 - is being actively used
    /// </summary>
    public int state;
    public Vector3 chunkLocation;
    public int chunkR;
    public Renderer rend;
    public int generatorId;
}

public class TerrainManager : MonoBehaviour
{
    FindChunkOrder findChunkOrder;
    MarchingCubeInstructions marchingCubeInstructions;
    GameObject player;

    Vector3 playerSmallGridLoc;

    public GameObject myPrefab;
    float seed;

    Thread terraneGeneratorThread;
    Thread meshCleanThread;

    List<ChunkData> allChunks = new List<ChunkData>();
    List<TerrainInterpreter> terraneGenerators = new List<TerrainInterpreter>();
    List<int>[] renderersToDisable;

    Dictionary<Vector3, ChunkOrderData> chunksToCreate = new Dictionary<Vector3, ChunkOrderData>();
    Dictionary<Vector3, int> createdChunks = new Dictionary<Vector3, int>();


    int renderR = 400;

    int triangleCountPerChunkVertex = 32; // 32
    int generatorCount = 32; //4 //32
    int cleanMesh = 1; // 1 => look for meshes

    float updateFrameStart;

    void Start()
    {
        Application.targetFrameRate = 300;
        player = GameObject.FindWithTag("Player");
        marchingCubeInstructions = new MarchingCubeInstructions();

        findChunkOrder = new FindChunkOrder(
            ref chunksToCreate,
            renderR,
            generatorCount
        );

        // Seed
        seed = UnityEngine.Random.Range(60000f, 1111111f);
        //seed = 3532.9f;
        //seed = 0;

        GameObject chunkMainObject = new GameObject();
        chunkMainObject.name = "Chunks";
        chunkMainObject.transform.SetParent(gameObject.transform); // Setting chunk parent (for organizing)

        for (int i = 0; i < generatorCount; i++)
        {
            addTerraneGenerator(chunkMainObject, seed, i);
        }

        renderersToDisable = new List<int>[generatorCount];

        for (int i = 0; i < renderersToDisable.Length; i++)
        {
            renderersToDisable[i] = new List<int>();
        }

        // Thread to handle terrane generation
        terraneGeneratorThread = new Thread(new ThreadStart(terraneHandleing));
        terraneGeneratorThread.Start();

        // Thread to clean meshes
        meshCleanThread = new Thread(new ThreadStart(cleanMeshes));
        meshCleanThread.Start();

        playerSmallGridLoc = getPlayerLoc();
    }

    public void addTerraneGenerator(GameObject chunkMainObject, float sqew, int i)
    {
        TerrainInterpreter terraneGenerator = chunkMainObject.AddComponent<TerrainInterpreter>();
        terraneGenerator.cubeNodeSize = triangleCountPerChunkVertex;
        terraneGenerator.id = i;
        terraneGenerator.setUpVariables(sqew, marchingCubeInstructions);

        terraneGenerators.Add(terraneGenerator);
    }

    void cleanMeshes()
    {
        while (true)
        {
            if (cleanMesh == 3)
            {
                findChunkOrder.FindChunksToCreate(playerSmallGridLoc);

                cleanMesh = 0;
            }
            else
            {
                Thread.Sleep(1000);
            }
        }
    }

    bool tested = false;

    // Gets called every frame from main thread
    private void Update()
    {
        if (!tested && Input.GetKeyDown(KeyCode.P))
        {
            tested = true;

            foreach (var c in chunksToCreate)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = c.Value.location;
                cube.transform.localScale = c.Value.chunkR * Vector3.one;
            }
        }

        updateFrameStart = Time.time;
        playerSmallGridLoc = getPlayerLoc();

        if (cleanMesh == 1)
        {
            cleanMesh = 2;
        }

        foreach (var terraneGenerator in terraneGenerators)
        {
            if (Time.time - updateFrameStart > 0.001)
            {
                break;
            }

            if (cleanMesh == 0)
            {
                switchMT(terraneGenerator);
            }
            else
            {
                break;
            }
        }
    }

    // (NMT) - Things that can be done on Non Main Thread (NMT) for terrane generation
    void terraneHandleing()
    {
        while (true)
        {
            if (cleanMesh == 2)
            {
                cleanMesh = 3;
            }

            foreach (var terraneGenerator in terraneGenerators)
            {
                if (cleanMesh == 0)
                {
                    switchNMT(terraneGenerator);
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void switchMT(TerrainInterpreter terraneGenerator)
    {
        switch (terraneGenerator.state)
        {
            case 1: // Hide not needed chunk cube meshes (Renderer.enabled = false; - only in MT)
                removeNotNeededChunks(terraneGenerator);
                terraneGenerator.state = 2;
                break;
            case 3: // CreateJobReflectionData - MT only
                if (terraneGenerator.runJobs())
                {
                    terraneGenerator.state = 4;
                }
                break;
            case 4:
                if (terraneGenerator.jobComplete())
                {
                    terraneGenerator.state = 5;
                }

                break;
            case 5:
                if (terraneGenerator.UpdateMesh())
                {
                    terraneGenerator.state = 6;
                }
                break;
            case 6:
                allChunks[terraneGenerator.allChunkId].rend.enabled = true;

                terraneGenerator.state = 7;
                break;
            case 7:
                if (terraneGenerator.bakeJobJob.IsCompleted)
                {
                    terraneGenerator.setColiderMesh();
                    allChunks[terraneGenerator.allChunkId].state = 1;

                    terraneGenerator.state = 0;
                }
                break;
            case 101:
                var cd = new ChunkData();
                cd.go = terraneGenerator.InitChunkObject();
                cd.mesh = terraneGenerator.GetMeshReference();
                cd.rend = terraneGenerator.GetRendererReference();
                cd.generatorId = terraneGenerator.id;
                cd.state = 0;
                cd.chunkLocation = new Vector3(0.1f, 0, 0);

                allChunks.Add(cd);

                terraneGenerator.state = 0;

                break;
            default:
                break;
        }
    }

    // NMT - Not Main Thread
    private void switchNMT(TerrainInterpreter terraneGenerator)
    {
        switch (terraneGenerator.state)
        {
            case 0:
                findNotNeededChunks(terraneGenerator);
                terraneGenerator.state = 1;
                break;
            case 2: // Select next chunk (chunksToCreate) to do and pick mesh cube (allChunks) to draw it in
                int stateCase = initiatenewChunkLoad(terraneGenerator);

                if (stateCase == 0)
                {
                    terraneGenerator.state = 0;
                }
                else if (stateCase == 1)
                {
                    terraneGenerator.SetJobVariables();
                    terraneGenerator.state = 3;
                }
                else if (stateCase == 2)
                {
                    // Create new mesh cube (allChunks) to draw in
                    terraneGenerator.state = 101;
                }

                break;
            default:
                break;
        }
    }

    public bool allChildrenDone(Vector3 input, int size)
    {
        if (size < 2)
        {
            return true;
        }

        bool result = true;
        int halfSize = size / 2;
        Vector3 child = new Vector3();

        for (int xs = -1; xs <= 1; xs += 2)
        {
            for (int ys = -1; ys <= 1; ys += 2)
            {
                for (int zs = -1; zs <= 1; zs += 2)
                {
                    child.x = input.x + halfSize * xs;
                    child.y = input.y + halfSize * ys;
                    child.z = input.z + halfSize * zs;

                    if (chunksToCreate.ContainsKey(child))
                    {
                        if (createdChunks.ContainsKey(child) && allChunks[createdChunks[child]].state == 1)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        result = result && allChildrenDone(child, halfSize);

                        if (!result)
                        {
                            return result;
                        }
                    }
                }
            }
        }

        return true;
    }

    public bool parentIsDone(Vector3 input, int chunkR)
    {
        if (chunkR == 0)
        {
            return true;
        }

        Vector3 parent = getParentChunk(input, chunkR);

        if (chunkR > renderR * 2)
        {
            return true;
        }
        else
        {
            if (chunksToCreate.ContainsKey(parent))
            {
                if (createdChunks.ContainsKey(parent) && allChunks[createdChunks[parent]].state == 1)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return parentIsDone(parent, chunkR * 2);
            }
        }
    }

    public void findNotNeededChunks(TerrainInterpreter terraneGenerator)
    {
        renderersToDisable[terraneGenerator.id].Clear();

        for (int i = 0; i < allChunks.Count; i++)
        {
            if (allChunks[i] != null && allChunks[i].state == 2 && allChunks[i].generatorId == terraneGenerator.id)
            {
                renderersToDisable[terraneGenerator.id].Add(i);
            }
        }
    }

    public void removeNotNeededChunks(TerrainInterpreter terraneGenerator)
    {
        foreach (var i in renderersToDisable[terraneGenerator.id])
        {
            allChunks[i].rend.enabled = false;
            allChunks[i].state = 0;
        }

        renderersToDisable[terraneGenerator.id].Clear();
    }

    public int initiatenewChunkLoad(TerrainInterpreter terrainInterpreter)
    {
        int stateCase = 0;
        Vector3 key = new Vector3();
        bool foundAvalableChunk = false;
        bool foundChunkToCreate = false;

        if (cleanMesh != 0)
        {
            return stateCase;
        }

        // Find chunks to unload
        for (int i = 0; i < allChunks.Count; i++)
        {
            if (allChunks[i] == null)
            {
                break;
            }

            if (
                allChunks[i] != null &&
                allChunks[i].state == 1 &&
                !chunksToCreate.ContainsKey(allChunks[i].chunkLocation) &&
                allChunks[i].generatorId == terrainInterpreter.id &&
                allChildrenDone(allChunks[i].chunkLocation, allChunks[i].chunkR) &&
                parentIsDone(allChunks[i].chunkLocation, allChunks[i].chunkR) &&
                true
            )
            {
                allChunks[i].state = 2;
                createdChunks.Remove(allChunks[i].chunkLocation);
            }
        }

        foreach (var data in chunksToCreate)
        {
            if (!createdChunks.ContainsKey(data.Value.location) && data.Value.queueNumber == terrainInterpreter.id && !data.Value.done)
            {
                foundChunkToCreate = true;
                key = data.Key;
                break;
            }
        }

        if (!foundChunkToCreate)
        {
            return stateCase;
        }

        var nextChunkData = chunksToCreate[key];

        // Find next chunk that can be used
        for (int i = 0; i < allChunks.Count; i++)
        {
            if (allChunks[i] != null && allChunks[i].state == 0 && allChunks[i].generatorId == terrainInterpreter.id)
            {
                foundAvalableChunk = true;

                int chunkR = nextChunkData.chunkR / 2;

                Vector3 chunkStartPoint2 = new Vector3(nextChunkData.location.x - chunkR, nextChunkData.location.y - chunkR, nextChunkData.location.z - chunkR);

                allChunks[i].chunkLocation.x = nextChunkData.location.x;
                allChunks[i].chunkLocation.y = nextChunkData.location.y;
                allChunks[i].chunkLocation.z = nextChunkData.location.z;

                allChunks[i].state = 3;
                allChunks[i].chunkR = nextChunkData.chunkR;

                terrainInterpreter.chunkDetailMultiplier = nextChunkData.chunkR;
                terrainInterpreter.allChunkId = i;
                terrainInterpreter.setStartData(allChunks[i].go, allChunks[i].mesh, nextChunkData.location);
                nextChunkData.done = true;

                chunksToCreate[key] = nextChunkData;
                createdChunks.Add(allChunks[i].chunkLocation, i);
                stateCase = 1;

                break;
            }
        }

        if (!foundAvalableChunk)
        {
            stateCase = 2;
        }

        return stateCase;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="inputVector"></param>
    /// <param name="chunkSize">"inputVector" size</param>
    /// <returns></returns>
    private Vector3 getParentChunk(Vector3 inputVector, int chunkSize)
    {
        chunkSize = chunkSize * 2;

        var scaledX = (inputVector.x < 0) ? -1 : 1;
        var scaledY = (inputVector.y < 0) ? -1 : 1;
        var scaledZ = (inputVector.z < 0) ? -1 : 1;

        var nX = ((int)inputVector.x / chunkSize) * chunkSize + (chunkSize / 2 * scaledX);
        var nY = ((int)inputVector.y / chunkSize) * chunkSize + (chunkSize / 2 * scaledY);
        var nZ = ((int)inputVector.z / chunkSize) * chunkSize + (chunkSize / 2 * scaledZ);

        Vector3 output = new Vector3(nX, nY, nZ);

        return output;
    }

    private Vector3 getPlayerBigGridLoc(Vector3 curLoc)
    {
        int x = (int)curLoc.x;
        int y = (int)curLoc.y;
        int z = (int)curLoc.z;
        int chunkR = 1;
        int cr = chunkR * 2 + 1;

        x = (x - chunkR) / cr;
        y = (y - chunkR) / cr;
        z = (z - chunkR) / cr;

        return new Vector3(x, y, z);
    }

    Vector3 getPlayerLoc()
    {
        int x = (int)(player.transform.position.x / triangleCountPerChunkVertex);
        int y = (int)(player.transform.position.y / triangleCountPerChunkVertex);
        int z = (int)(player.transform.position.z / triangleCountPerChunkVertex);

        if (player.transform.position.x < 0) { x--; }
        if (player.transform.position.y < 0) { y--; }
        if (player.transform.position.z < 0) { z--; }

        var newLoc = new Vector3(x, y, z);

        if (newLoc != playerSmallGridLoc)
        {
            if (cleanMesh == 0 && getPlayerBigGridLoc(newLoc) != getPlayerBigGridLoc(playerSmallGridLoc))
            {
                cleanMesh = 1;
            }
        }

        return newLoc;
    }
}
