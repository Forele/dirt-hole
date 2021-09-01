using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class TerrainEdit
{
    public Queue<DigData> digRequests;
    public Queue<ChangeRequest> changeRequests;
    public TerrainData terrainData;
    public List<Chunk> allActiveChunks;
    public Queue<Chunk> chunkWaiteForNative;
    int oneDim;

    public TerrainEdit(
        ref List<Chunk> _allActiveChunks, 
        TerrainData _terrainData,
        ref Queue<Chunk> _chunkWaiteForNative
    )
    {
        digRequests = new Queue<DigData>();
        changeRequests = new Queue<ChangeRequest>();

        allActiveChunks = _allActiveChunks;
        terrainData = _terrainData;
        chunkWaiteForNative = _chunkWaiteForNative;
        oneDim = terrainData.segemntCountPerDimension + 1;
    }

    public struct ChangeRequest
    {
        public List<Vector3> points;
        public List<float> strengths;
        public Chunk chunk;
    }

    public struct DigData
    {
        public Vector3 digPos;
        public Vector3 chunkPos;
    }

    string StringVector(Vector3 vec)
    {
        return (vec.x.ToString() + "_" + vec.y.ToString() + "_" + vec.z.ToString());
    }

    // Update is called once per frame
    public void Update()
    {
        ProcessDigChangeRequests();
        ProcessChangeRequests();
    }

    public void ProcessChangeRequests()
    {
        if (changeRequests.Count > 0)
        {
            ChangeRequest changeRequest = changeRequests.Dequeue();
            Chunk chunk = changeRequest.chunk;

            ThreadDataRequest.RequestData(() =>
            {
                //while (chunk.state != Chunk.State.Done) ;

                if (chunk.state != Chunk.State.Done)
                {
                    changeRequests.Enqueue(changeRequest);
                    return chunk;
                }

                int i = 0;

                for (i = 0; i < changeRequest.points.Count; i++)
                {
                    var point = changeRequest.points[i];
                    var strength = changeRequest.strengths[i];

                    chunk.strengths[(int)(point.x + point.y * oneDim + point.z * oneDim * oneDim)] = strength;
                }

                chunk.nextStep = PopulateNativeStrengths;
                chunk.state = Chunk.State.WaitingNative;
                chunkWaiteForNative.Enqueue(chunk);

                return chunk;
            }, (object _chunk) =>
            {
                Chunk chunk = (Chunk)_chunk;

            });
        }
    }

    void PopulateNativeStrengths(Chunk chunk)
    {
        chunk.chunkNativeData.strengths.CopyFrom(chunk.strengths.ToArray());
        chunk.InterpretStrengths(chunk);
    }

    public void ProcessDigChangeRequests()
    {
        if (digRequests.Count > 0)
        {
            DigData digData = digRequests.Dequeue();

            Vector3 chunkL;
            chunkL.x = digData.chunkPos.x / terrainData.smallestChunkWidth;
            chunkL.y = digData.chunkPos.y / terrainData.smallestChunkWidth;
            chunkL.z = digData.chunkPos.z / terrainData.smallestChunkWidth;

            Chunk chunk = allActiveChunks.SingleOrDefault(x => x.position == chunkL);

            if (chunk == null)
            {
                Debug.LogError("Click on non existing chunk");
            }

            Vector3 localAbsolutePoint = digData.digPos - (digData.chunkPos - Vector3.one * chunk.chunkR * terrainData.smallestChunkWidth);
            Vector3 triangleIndex = localAbsolutePoint / (chunk.chunkR * 2f * (terrainData.smallestChunkWidth / (float)terrainData.segemntCountPerDimension));

            Vector3 unrounded = triangleIndex;

            triangleIndex.x = Mathf.RoundToInt(triangleIndex.x);
            triangleIndex.y = Mathf.RoundToInt(triangleIndex.y);
            triangleIndex.z = Mathf.RoundToInt(triangleIndex.z);

            List<ChunkDataFetcher.StrengthNode> strengthNodeList = new List<ChunkDataFetcher.StrengthNode>();

            for (int x = -4; x <= 4; x++)
            {
                for (int y = -4; y <= 4; y++)
                {
                    for (int z = -4; z <= 4; z++)
                    {
                        var strengthNode = new ChunkDataFetcher.StrengthNode();

                        strengthNode.pos = new Vector3(triangleIndex.x + x, triangleIndex.y + y, triangleIndex.z + z);

                        if (
                            strengthNode.pos.x < 0 || strengthNode.pos.y < 0 || strengthNode.pos.z < 0 ||
                            strengthNode.pos.x >= oneDim || strengthNode.pos.y >= oneDim || strengthNode.pos.z >= oneDim
                        )
                        {
                            continue;
                        }


                        strengthNode.strength = chunk.strengths[
                            (int)(strengthNode.pos.x + strengthNode.pos.y * oneDim + strengthNode.pos.z * oneDim * oneDim)
                        ];

                        strengthNode.dist = Vector3.Distance(strengthNode.pos, unrounded);


                        strengthNodeList.Add(strengthNode);
                    }
                }
            }

            bool found = false;
            Vector3 targetIndex = new Vector3(-1, -1, -1);
            float minDist = 100f;

            foreach (var item in strengthNodeList)
            {
                if (item.dist < minDist && item.strength > 0.5f)
                {
                    found = true;
                    targetIndex = item.pos;
                    minDist = item.dist;
                }
            }


            if (found)
            {
                int scpd = terrainData.segemntCountPerDimension;

                //AddToChangeRequest(chunkL, targetIndex, 0);

                if (targetIndex.x >= scpd)
                {
                    targetIndex.x -= terrainData.segemntCountPerDimension;
                    chunkL.x += 2;
                }

                if (targetIndex.y >= scpd)
                {
                    targetIndex.y -= terrainData.segemntCountPerDimension;
                    chunkL.y += 2;
                }

                if (targetIndex.z >= scpd)
                {
                    targetIndex.z -= terrainData.segemntCountPerDimension;
                    chunkL.z += 2;
                }

                RecursiveAddToChangeRequest(chunkL, targetIndex, 0);
            }
        }
    }

    void RecursiveAddToChangeRequest(Vector3 chunkLoc, Vector3 targetIndex, float strength)
    {
        AddToChangeRequest(chunkLoc, targetIndex, 0);

        if (targetIndex.x == 0)
        {
            RecursiveAddToChangeRequest(
                new Vector3(chunkLoc.x - 2, chunkLoc.y, chunkLoc.z),
                new Vector3(targetIndex.x + terrainData.segemntCountPerDimension, targetIndex.y, targetIndex.z),
                strength
            );
        }

        if (targetIndex.y == 0)
        {
            RecursiveAddToChangeRequest(
                new Vector3(chunkLoc.x, chunkLoc.y - 2, chunkLoc.z),
                new Vector3(targetIndex.x, targetIndex.y + terrainData.segemntCountPerDimension, targetIndex.z),
                strength
            );
        }

        if (targetIndex.z == 0)
        {
            RecursiveAddToChangeRequest(
                new Vector3(chunkLoc.x, chunkLoc.y, chunkLoc.z - 2),
                new Vector3(targetIndex.x, targetIndex.y, targetIndex.z + terrainData.segemntCountPerDimension),
                strength
            );
        }
    }

    void AddToChangeRequest(Vector3 chunkLoc, Vector3 targetIndex, float strength)
    {
        Chunk chunk = allActiveChunks.SingleOrDefault(x => x.position == chunkLoc);

        if (chunk != null)
        {
            ChangeRequest changeRequest = new ChangeRequest();

            changeRequest.strengths = new List<float>();
            changeRequest.points = new List<Vector3>();

            changeRequest.chunk = chunk;
            changeRequest.strengths.Add(strength);
            changeRequest.points.Add(targetIndex);

            changeRequests.Enqueue(changeRequest);
        }
    }
}
