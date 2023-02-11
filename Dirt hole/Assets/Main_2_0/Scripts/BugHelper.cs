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

public class BugHelper : MonoBehaviour
{
    public TerrainData terrainData;
    public float testVar;

    // Start is called before the first frame update
    void Start()
    {
        // Debug.Log("weex");
        MarchingCubeInstructions marchingCubeInstructions = new MarchingCubeInstructions();
        ChunkDataInterpreter chunkDataInterpreter = new ChunkDataInterpreter();
        ChunkDataFetcher chunkDataFetcher = new ChunkDataFetcher();

        chunkDataInterpreter.SetUpVariables(marchingCubeInstructions, terrainData);
        Chunk chunk = new Chunk();
        ChunkNativeData chunkNativeData = new ChunkNativeData(terrainData, marchingCubeInstructions);
        chunk.chunkNativeData = chunkNativeData;

        chunk.InitMT(gameObject, chunkDataFetcher, chunkDataInterpreter);
        chunk.InitNMT(/*terrainData, marchingCubeInstructions*/);
        chunk.strengths = new List<float> {1f,0f,0f,1f};
        chunkDataInterpreter.ScheduleChunkInterpretation(chunk.chunkNativeData);
        chunk.CompleteJobHandle();
        chunk.LastSteps(chunk);
    }

    bool t = true;

    // Update is called once per frame
    void Update()
    {
        if (t)
        {
            t = false;
            //Debug.Log(terrainData.smallestChunkWidth.ToString());
            //Debug.Log(terrainData.smallestChunkWidth.ToString());

            

            //Debug.Log("End of test 'start'");
        }
    }
}
