using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TestChunk : MonoBehaviour
{
    [Range(0f, 1f)]
    public float strengthTest;

    [Range(0f, 40f)]
    public float testNumber;



    public TerrainData terrainData;
    public CubeStrengths cubeStrengths;

    public MyUtility myUtility;

    private Chunk chunk;
    private ChunkDataFetcher chunkDataFetcher;
    private ChunkDataInterpreter chunkDataInterpreter;
    private ChunkNativeData chunkNativeData;
    private MarchingCubeInstructions marchingCubeInstructions;

    // Start is called before the first frame update
    void Start()
    {
        myUtility = new MyUtility();

        chunk = new Chunk();
        chunk.position = new Vector3(0, 0, 0);
        //Debug.Log("x");
        marchingCubeInstructions = new MarchingCubeInstructions();
        chunkDataInterpreter = new ChunkDataInterpreter();
        chunkDataFetcher = new ChunkDataFetcher();
        chunkNativeData = new ChunkNativeData(terrainData, marchingCubeInstructions);
        chunk.chunkNativeData = chunkNativeData;
        ShowChanges();

        //chunkDataInterpreter.SetUpVariables(marchingCubeInstructions, terrainData);

        //
        //chunkNativeData.SetRunData(chunk);
        //chunk.chunkDataInterpreter = chunkDataInterpreter;

        //chunk.NewInit(gameObject, chunkDataFetcher, chunkDataInterpreter);

        //chunk.chunkNativeData.dataGeneration.strengthTest = strengthTest;
        //chunk.chunkNativeData.dataGeneration.testNumber = testNumber;
        ////chunk.chunkNativeData.cubeMarch.testNumber = testNumber;

        //chunkDataFetcher.SetTerrainData(terrainData);


        //chunkDataFetcher.ScheduleChunkData(chunk.chunkNativeData);
        //chunkDataInterpreter.ScheduleChunkInterpretation(chunk.chunkNativeData);
        //chunk.chunkNativeData.meshMakingHandle.Complete();
        //chunk.chunkDataInterpreter.SetChunkData(chunk);

        //chunk.gameObject.transform.localScale = Vector3.one * 100;

    }

    // Update is called once per frame
    public void ShowChanges()
    {
        chunk.chunkNativeData.DisposeInstances();

        chunk.firstSetUpDone = false;
        Destroy(chunk.gameObject);

        chunkNativeData = new ChunkNativeData(terrainData, marchingCubeInstructions);

        chunk.chunkNativeData = chunkNativeData;
        chunkNativeData.SetRunData(chunk);

        chunk.chunkNativeData.dataGeneration.strengthTest = strengthTest;
        chunk.chunkNativeData.dataGeneration.testNumber = testNumber;
        //chunk.chunkNativeData.cubeMarch.testNumber = testNumber;

        chunkDataInterpreter.SetUpVariables(marchingCubeInstructions, terrainData);
        chunk.chunkDataInterpreter = chunkDataInterpreter;

        chunk.NewInit(gameObject, chunkDataFetcher, chunkDataInterpreter);

        chunkDataFetcher.SetTerrainData(terrainData);

        chunkDataFetcher.ScheduleChunkData(chunk.chunkNativeData);


        chunk.chunkNativeData.meshMakingHandle.Complete();

        chunk.chunkNativeData.strengths[0] = cubeStrengths.p1;
        chunk.chunkNativeData.strengths[1] = cubeStrengths.p2;
        chunk.chunkNativeData.strengths[2] = cubeStrengths.p3;
        chunk.chunkNativeData.strengths[3] = cubeStrengths.p4;
        chunk.chunkNativeData.strengths[4] = cubeStrengths.p5;
        chunk.chunkNativeData.strengths[5] = cubeStrengths.p6;
        chunk.chunkNativeData.strengths[6] = cubeStrengths.p7;
        chunk.chunkNativeData.strengths[7] = cubeStrengths.p8;

        chunkDataInterpreter.ScheduleChunkInterpretation(chunk.chunkNativeData);
        chunk.chunkNativeData.meshMakingHandle.Complete();
        chunk.chunkDataInterpreter.SetChunkData(chunk);

        chunk.gameObject.transform.localScale = Vector3.one * 100;

        //Debug.Log(chunk.chunkNativeData.vertices.Length.ToString());
    }

    //public void Magic()
    //{
    //    chunkDataFetcher.ScheduleChunkData(chunk.chunkNativeData);
    //    chunkDataInterpreter.ScheduleChunkInterpretation(chunk.chunkNativeData);
    //    chunk.chunkNativeData.meshMakingHandle.Complete();
    //    chunk.chunkDataInterpreter.SetChunkData(chunk);

    //    chunk.gameObject.transform.localScale = Vector3.one;

    //}
}
