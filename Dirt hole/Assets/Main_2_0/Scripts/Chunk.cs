using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class Chunk
{
    public enum State
    {
        New,
        DoneChunkJobs, AfterAllInits, NMT, FirstRun, Queuing, RuningCommand,
        WaitingNative, GotNative, Populating, Interpreting,
        FillingStrengths, Colliding, Digging, Making, Done, Free
    };

    public enum Command { None, Freed };

    public ChunkNativeData chunkNativeData;

    public Vector3 position;
    public Vector3 subchunkIndex;
    public int chunkR = 2;
    public State state = State.New;
    public Command command = Command.None;
    public GameObject gameObject;
    public MeshCollider meshCollider;
    public Renderer renderer;
    public MeshFilter meshFilter;
    public Mesh mesh;
    public bool firstSetUpDone = false;
    public int meshInstanceId;
    public bool finished = false;

    public List<float> strengths;
    public ChunkDataFetcher chunkDataFetcher;
    public ChunkDataInterpreter chunkDataInterpreter;

    public Action<Chunk> nextStep;

    public ConcurrentQueue<Action> commandQueue = new ConcurrentQueue<Action>();

    bool once = true;
    string StringVector(Vector3 vec)
    {
        return (vec.x.ToString() + "_" + vec.y.ToString() + "_" + vec.z.ToString());
    }

    public void RunNextQueueCommand()
    {
        if (commandQueue.Count > 0 && (state == State.Done || state == State.Free || state == State.New))
        {
            Action command = null;

            commandQueue.TryDequeue(out command);

            if (command == null)
            {
                return;
            }

            var lastState = state;

            state = State.RuningCommand;

            command();
        }
    }

    //public void SetParent(GameObject pGameObject)
    //{
    //    if (pGameObject != null)
    //    {
    //        gameObject = new GameObject();
    //        gameObject.layer = LayerMask.NameToLayer("Terrain");
    //        gameObject.transform.SetParent(pGameObject.transform);
    //    } else
    //    {
    //        Debug.LogWarning("SetParent called with null GameObject");
    //    }
    //}

    public void NewInit(GameObject parentGameObject, ChunkDataFetcher _chunkDataFetcher, ChunkDataInterpreter _chunkDataInterpreter)
    {
        //gameObject = new GameObject();
        //gameObject.layer = LayerMask.NameToLayer("Terrain");
        //gameObject.transform.SetParent(parentGameObject.transform);
        //gameObject.AddComponent<MeshRenderer>();
        //meshCollider = gameObject.AddComponent<MeshCollider>();

        //meshFilter = gameObject.AddComponent<MeshFilter>();
        //renderer = gameObject.GetComponent<Renderer>();
        //renderer.material = Resources.Load<Material>("Materials/TerrainMaterial");
        //meshFilter.mesh.indexFormat = IndexFormat.UInt32;
        //mesh = meshFilter.mesh;

        InitMT(parentGameObject, _chunkDataFetcher, _chunkDataInterpreter);
        chunkNativeData.SetRunData(this);
    }

    public bool InitMT(
        GameObject pGameObject,
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
            meshCollider = gameObject.AddComponent<MeshCollider>();

            meshFilter = gameObject.AddComponent<MeshFilter>();
            renderer = gameObject.GetComponent<Renderer>();
            renderer.material = Resources.Load<Material>("Materials/TerrainMaterial");
            meshFilter.mesh.indexFormat = IndexFormat.UInt32;
            mesh = meshFilter.mesh;

            int oneDim = chunkNativeData.terrainData.segemntCountPerDimension + 1;

            strengths = new List<float>(new float[oneDim * oneDim * oneDim]);
            chunkDataFetcher = _chunkDataFetcher;
            chunkDataInterpreter = _chunkDataInterpreter;

            firstSetUpDone = true;
        }

        return chunkNativeData != null;
    }

    public void InitNMT(/*TerrainData terrainData, MarchingCubeInstructions marchingCubeInstructions*/)
    {
        state = State.Making;
        chunkNativeData.SetRunData(this);
    }

    public void CompleteJobHandle()
    {
        chunkNativeData.meshMakingHandle.Complete();
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
                chunk.state = Chunk.State.DoneChunkJobs;
                chunk.nextStep(chunk);
            }
        });
    }

    // MT
    public void InterpretStrengths(Chunk chunk)
    {
        chunk.chunkDataInterpreter.ScheduleChunkInterpretation(chunk.chunkNativeData);
        chunk.nextStep = CreateCollider;
        WaitForChunkJobs(chunk);
    }

    public void CreateCollider(Chunk chunk)
    {
        chunk.state = State.Colliding;
        chunk.CompleteJobHandle();

        if (chunk.chunkNativeData.triangles.Length <= 0)
        {
            // Debug.Log("thing happened");
            LastSteps(chunk);
            return;
        }

        chunkDataInterpreter.SetChunkData(chunk);

        ThreadDataRequest.RequestData(() =>
        {
            //Physics.BakeMesh(chunk.meshInstanceId, false); // HERE temp alert fix (commented out to fix)
            return chunk;
        }, (object _chunk) =>
        {
            Chunk chunk = (Chunk)_chunk;

            chunk.strengths = chunk.chunkNativeData.strengths.ToList();
            //string ss = "";

            //foreach (var item in chunk.chunkNativeData.vertices)
            //{
            //    ss += ", " + StringVector(item);
            //}

            //File.WriteAllText("D://Test.txt", ss);

            if (false && once)
            {
                //string s = "{" + string.Join(", ", chunk.chunkNativeData.strengths.ToArray()) + "}";
                //File.WriteAllText("D://Test.txt", s);
                once = false;
            }

            if (Input.GetKey(KeyCode.X) && chunk.strengths.Count > 0)
            {
                //foreach (var vert in chunk.strengths)
                //{
                //    Debug.Log((vert));
                //}

            }

            if ((false && chunk.position.y < 0))
            {
                string s = "{" + string.Join(", ", (chunk.strengths).ToArray()) + "}";
                s += "  " + StringVector(chunk.position);
                File.WriteAllText("D://Test.txt", s);

            }

            ////chunk.mesh.vertices
            //string re = "";

            //foreach (var v3 in chunk.chunkNativeData.cubeMarch.triangleCounts)
            //{
            //    re += v3.ToString() + "|||";
            //}
            
            ////chunk.mesh.vertices
            //string re = "";

            //foreach (var v3 in chunk.mesh.vertices)
            //{
            //    re += StringVector(v3) + "|||";
            //}

            //string re = "";

            //foreach (var v3 in chunk.mesh.triangles)
            //{
            //    re += v3 + "_";
            //}

            //Debug.Log(re);
            //Debug.Log(StringVector(chunk.position));

            

            chunk.meshCollider.sharedMesh = chunk.mesh;

            chunk.renderer.enabled = true;
            LastSteps(chunk);
        });
    }

    public void LastSteps(Chunk chunk)
    {
        chunk.chunkNativeData.meshBakeHandle.Complete();
        chunk.chunkNativeData.meshMakingHandle.Complete();
        chunk.chunkNativeData.userCount--;
        chunk.chunkNativeData.inUse = false;
        chunk.finished = true;

        chunk.state = Chunk.State.Done;
    }
}

