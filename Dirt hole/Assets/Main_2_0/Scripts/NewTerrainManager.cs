using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using System;
using System.IO;

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

    ConcurrentQueue<Chunk> chunkWaiteForNative = new ConcurrentQueue<Chunk>();
    Thread chunkWaiteForNativeThread;
    public TerrainEdit terrainEdit;

    public Camera fpsCam;

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

        //if (Input.GetKey(KeyCode.T))
        //{
        //    Debug.Log(allActiveChunks.Count().ToString());
        //}
    }

    void Start()
    {
        //return;
        RenderTerraneStart();
    }

    bool shown = false; // Fot P to display showableChunks as cubes

    private void Update()
    {
        //return;

        RenderTerrain();

        if (Input.GetButtonDown("Fire2"))
        {
            terrainEdit.Shoot();
        }

        if (Input.GetButtonDown("Fire1"))
        {
            terrainEdit.Shoot(-1);
        }

        terrainEdit.Update();
    }

    void RenderTerraneStart()
    {
        Application.targetFrameRate = Mathf.Clamp(targetFps, 10, 1000);
        marchingCubeInstructions = new MarchingCubeInstructions();

        player = GameObject.FindWithTag("Player");
        allActiveChunks = new List<Chunk>();

        //Debug.Log(terrainData.smallestChunkWidth.ToString());

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

        terrainEdit = new TerrainEdit(ref allActiveChunks, terrainData, ref chunkWaiteForNative, fpsCam);
    }

    void RenderTerrain()
    {
        oldPlayerGridLoc = playerGridLoc;
        playerGridLoc = GetPlayerLoc(player);

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

            Chunk isSet = allActiveChunks.FirstOrDefault(x => x.position == chunkToCreate.position);
            showableChunks.Add(chunkToCreate.position);

            //Debug.Log(StringVector(chunkToCreate.location) + "_" +  chunkToCreate.chunkR);

            if (isSet == null)
            {
                Chunk freeChunk = allActiveChunks.FirstOrDefault(x => x.state == Chunk.State.Free);

                if (freeChunk != null)
                {
                    freeChunk.position = chunkToCreate.position;
                    freeChunk.chunkR = chunkToCreate.chunkR;
                }
                else
                {
                    freeChunk = chunkToCreate;
                    allActiveChunks.Add(freeChunk);
                }

                //freeChunk.state = Chunk.State.Queuing;

                freeChunk.commandQueue.Enqueue(() =>
                {
                    freeChunk.state = Chunk.State.WaitingNative;
                    freeChunk.command = Chunk.Command.None;
                    freeChunk.finished = false;
                    freeChunk.nextStep = FirstChunkRun;

                    chunkWaiteForNative.Enqueue(freeChunk);
                });
            }
            else if (isSet.renderer != null)
            {
                isSet.renderer.enabled = true;
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
                }

                if (chunk.state != Chunk.State.Free && chunk.command != Chunk.Command.Freed && Vector3.Distance(chunk.position, playerGridLoc) > terrainData.newChunkR + 4)
                {
                    chunk.commandQueue.Enqueue(() =>
                    {
                        // Assumed no further interraction possible

                        Action item;

                        while (chunk.commandQueue.TryDequeue(out item))
                        {
                            // do nothing
                        }

                        chunk.command = Chunk.Command.Freed;
                        chunk.state = Chunk.State.Free;
                    });
                }
            }
        }

        foreach (Chunk chunk in allActiveChunks)
        {
            chunk.RunNextQueueCommand();
        }

        // Debug.Log(allActiveChunks.Count.ToString());
    }

    //+ Whenever chunk is edied (create chunk, dig chunk)
    void ChunkWaiteForNativeManager()
    {
        while (true)
        {
            if (chunkWaiteForNative.Count > 0)
            {
                Chunk chunk = null;
                chunkWaiteForNative.TryDequeue(out chunk);

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
                            chunkNativeData.userCount++;

                            if (chunkNativeData.userCount > 1)
                            {
                                Debug.Log("Too many users");
                            }

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
        chunk.state = Chunk.State.FirstRun;

        ThreadDataRequest.RequestData(() =>
        {
            return chunk;
        }, (object _chunk) =>
        {
            Chunk chunk = (Chunk)_chunk;
            chunk.state = Chunk.State.Making;
            chunk.InitMT(gameObject, chunkDataFetcher, chunkDataInterpreter);

            ThreadDataRequest.RequestData(() => {
                chunk.state = Chunk.State.NMT;
                chunk.InitNMT(/*terrainData, marchingCubeInstructions*/);

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

        chunk.state = Chunk.State.AfterAllInits;

        chunkDataFetcher.ScheduleChunkData(chunk.chunkNativeData);
        chunkDataInterpreter.ScheduleChunkInterpretation(chunk.chunkNativeData);

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


