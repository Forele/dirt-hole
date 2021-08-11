using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public struct ChunkOrderData
{
    public Vector3 location;
    public int chunkR;
    public float distance;
    public int queueNumber;
    public bool done;

    public ChunkOrderData(Vector3 _location = new Vector3(), int _chunkR = 0, float _distance = 0, int _queueNumber = 0, bool _done = false)
    {
        location = _location;
        chunkR = _chunkR;
        distance = _distance;
        queueNumber = _queueNumber;
        done = _done;
    }
}

public class FindChunkOrder
{
    int newChunkR = 64;
    Vector3 playerSmallGridLoc;
    int generatorCount = 32; //4 //32

    Dictionary<Vector3, ChunkOrderData> newChunksToCreate = new Dictionary<Vector3, ChunkOrderData>();
    Dictionary<Vector3, ChunkOrderData> chunksToCreate; // = new Dictionary<Vector3, ChunkOrderData>();

    Vector3 signV = new Vector3();
    List<IEnumerator<ChunkOrderData>> chunkSpaceIterators = new List<IEnumerator<ChunkOrderData>>();
    List<Vector3> foundEdgeBigChunks = new List<Vector3>();

    public FindChunkOrder(
        ref Dictionary<Vector3, ChunkOrderData> _chunksToCreate,
        int _newChunkR,
        int _generatorCount
    )
    {
        chunksToCreate = _chunksToCreate;
        newChunkR = _newChunkR;
        generatorCount = _generatorCount;
    }

    /// <summary>
    /// New function version for finding chunk rendering order.
    /// </summary>
    public void FindChunksToCreate(Vector3 _playerSmallGridLoc)
    {
        playerSmallGridLoc = _playerSmallGridLoc;
        newChunksToCreate.Clear();


        signV.x = playerSmallGridLoc.x >= 0 ? 1 : -1;
        signV.y = playerSmallGridLoc.y >= 0 ? 1 : -1;
        signV.z = playerSmallGridLoc.z >= 0 ? 1 : -1;

        chunkSpaceIterators.Clear();
        foundEdgeBigChunks.Clear();

        int chunkStartR = (int)Mathf.Pow(2, Mathf.Floor(Mathf.Log(newChunkR, 2))) * 2;

        var oneStartChunk = new Vector3();

        for (int xSign = -1; xSign <= 1; xSign += 2)
        {
            for (int ySign = -1; ySign <= 1; ySign += 2)
            {
                for (int zSign = -1; zSign <= 1; zSign += 2)
                {
                    oneStartChunk.x = xSign * chunkStartR + playerSmallGridLoc.x;
                    oneStartChunk.y = ySign * chunkStartR + playerSmallGridLoc.y;
                    oneStartChunk.z = zSign * chunkStartR + playerSmallGridLoc.z;

                    oneStartChunk = getParentChunk(oneStartChunk, chunkStartR);

                    foundEdgeBigChunks.Add(oneStartChunk);
                }
            }
        }

        foundEdgeBigChunks.Select(x => x).Distinct();

        foreach (var item in foundEdgeBigChunks)
        {
            chunkSpaceIterators.Add(
                SectorRecursiveChildrenFind(playerSmallGridLoc, item, chunkStartR).GetEnumerator()
            );
        }

        bool hasThingsToDoo = true;
        int queueNumber = 0;

        newChunksToCreate.Clear();

        while (hasThingsToDoo)
        {
            hasThingsToDoo = false;

            foreach (var chunkIterator in chunkSpaceIterators)
            {
                if (chunkIterator.MoveNext())
                {
                    hasThingsToDoo = true;
                    var chunkData = chunkIterator.Current;

                    if (!newChunksToCreate.ContainsKey(chunkData.location))
                    {
                        newChunksToCreate.Add(
                            chunkData.location,
                            new ChunkOrderData(
                                chunkData.location,
                                chunkData.chunkR,
                                chunkData.distance,
                                queueNumber,
                                false
                            )
                        );

                        queueNumber++;

                        if (queueNumber >= generatorCount)
                        {
                            queueNumber = queueNumber % generatorCount;
                        }
                    }
                }
            }
        }

        // Add missing Vector3
        foreach (var newChunk in newChunksToCreate.OrderBy(key => key.Value.distance))
        {
            if (!chunksToCreate.ContainsKey(newChunk.Key) && newChunkR > newChunk.Value.distance)
            {
                chunksToCreate.Add(newChunk.Key, newChunk.Value);
            }
        }

        List<Vector3> noLongerNeeded = new List<Vector3>();

        // Remove not needed Vector3
        foreach (var chunk in chunksToCreate)
        {
            if (!newChunksToCreate.ContainsKey(chunk.Key))
            {
                noLongerNeeded.Add(chunk.Key);
            }
        }

        foreach (var thing in noLongerNeeded)
        {
            chunksToCreate.Remove(thing);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="inputVector"></param>
    /// <param name="chunkR">"inputVector" size</param>
    /// <returns></returns>
    private Vector3 getParentChunk(Vector3 inputVector, int chunkR)
    {
        chunkR = chunkR * 2;

        var scaledX = (inputVector.x < 0) ? -1 : 1;
        var scaledY = (inputVector.y < 0) ? -1 : 1;
        var scaledZ = (inputVector.z < 0) ? -1 : 1;

        var nX = ((int)inputVector.x / chunkR) * chunkR + (chunkR / 2 * scaledX);
        var nY = ((int)inputVector.y / chunkR) * chunkR + (chunkR / 2 * scaledY);
        var nZ = ((int)inputVector.z / chunkR) * chunkR + (chunkR / 2 * scaledZ);

        Vector3 output = new Vector3(nX, nY, nZ);

        return output;
    }

    public IEnumerable<ChunkOrderData> SectorRecursiveChildrenFind(Vector3 origin, Vector3 target, int size)
    {
        var children = getChildren(target, size);
        int halfSize = size / 2;

        List<Vector3> foundChildren = new List<Vector3>();
        List<Vector3> subdevidableChunks = new List<Vector3>();

        foreach (var child in children)
        {
            switch (getNextAction(origin, child, halfSize))
            {
                case 0:
                    // Do do nothing - out of bounds chunk
                    break;
                case 1:
                    foundChildren.Add(child);
                    break;
                case 2:
                    subdevidableChunks.Add(child);
                    break;
            }
        }

        List<IEnumerator<ChunkOrderData>> chunkIterators = new List<IEnumerator<ChunkOrderData>>();
        foreach (var child in subdevidableChunks)
        {
            chunkIterators.Add(
                SectorRecursiveChildrenFind(origin, child, halfSize).GetEnumerator()
            );
        }

        if (chunkIterators.Count > 0)
        {
            bool hasThingsToDoo = true;

            while (hasThingsToDoo)
            {
                hasThingsToDoo = false;

                foreach (var chunkIterator in chunkIterators)
                {
                    if (chunkIterator.MoveNext())
                    {
                        hasThingsToDoo = true;
                        yield return chunkIterator.Current;
                    }
                }
            }
        }

        foreach (var child in foundChildren)
        {
            yield return new ChunkOrderData(child, size, (child - origin).magnitude);
        }

        yield break;
    }

    public List<Vector3> getChildren(Vector3 inpV, int size)
    {
        int halfSize = size / 2;
        List<Vector3> children = new List<Vector3>();
        Vector3 foundChild = new Vector3();

        for (int xSign = -1; xSign <= 1; xSign += 2)
        {
            for (int ySign = -1; ySign <= 1; ySign += 2)
            {
                for (int zSign = -1; zSign <= 1; zSign += 2)
                {
                    foundChild.x = inpV.x + xSign * halfSize;
                    foundChild.y = inpV.y + ySign * halfSize;
                    foundChild.z = inpV.z + zSign * halfSize;

                    children.Add(foundChild);
                }
            }
        }

        return children;
    }

    public int getNextAction(Vector3 origin, Vector3 child, int currentChunkR)
    {
        var dist = (child - origin).magnitude / 4;
        var power = (int)(Mathf.Log(dist, 2));
        var allowedChunkR = Mathf.Pow(2, power);

        if (allowedChunkR < 2)
        {
            allowedChunkR = 2;
        }

        if (dist > newChunkR)
        {
            return 0;
        }

        return currentChunkR <= 2 || allowedChunkR >= currentChunkR ? 1 : 2;
    }
}
