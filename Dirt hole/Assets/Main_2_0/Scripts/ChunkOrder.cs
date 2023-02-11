using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System.Linq;

public class ChunkOrder
{
    public Vector3 playerLoc;
    TerrainData terrainData;
    List<Vector3> foundEdgeBigChunks = new List<Vector3>();
    List<IEnumerator<Chunk>> chunkSpaceIterators = new List<IEnumerator<Chunk>>();

    public void SetUpVariables(TerrainData _terrainData)
    {
        terrainData = _terrainData;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public IEnumerable<Chunk> TestGetNextChunk(Vector3 playerSmallGridLoc)
    {
        //Chunk chunk2 = new Chunk();
        //chunk2.chunkR = 2;
        //chunk2.location = new Vector3(-2, 2, 2);
        //chunk2.SetUpVariables(terrainData.segemntCountPerDimension);

        //yield return chunk2;

        //Chunk chunk = new Chunk();
        //chunk.chunkR = 1;
        //chunk.location = new Vector3(1, 1, 1);
        //chunk.SetUpVariables(terrainData.segemntCountPerDimension);

        //yield return chunk;

        //Chunk chunk3 = new Chunk();
        //chunk3.chunkR = 1;
        //chunk3.location = new Vector3(1, 3, 1);
        //chunk3.SetUpVariables(terrainData.segemntCountPerDimension);

        //yield return chunk3;

        //Chunk chunk4 = new Chunk();
        //chunk4.chunkR = 1;
        //chunk4.location = new Vector3(3, 1, 1);
        //chunk4.SetUpVariables(terrainData.segemntCountPerDimension);

        //yield return chunk4;

        //Chunk chunk5 = new Chunk();
        //chunk5.chunkR = 1;
        //chunk5.location = new Vector3(3, 3, 1);
        //chunk5.SetUpVariables(terrainData.segemntCountPerDimension);

        //yield return chunk5;

        //Chunk chunk6 = new Chunk();
        //chunk6.chunkR = 1;
        //chunk6.location = new Vector3(-1, 1, 1);
        //chunk6.SetUpVariables(terrainData.segemntCountPerDimension);

        //yield return chunk6;

        yield break;
    }


    void PrintVector(Vector3 vec)
    {
        Debug.Log(vec.x.ToString() + "_" + vec.y.ToString() + "_" + vec.z.ToString());
    }

    string StringVector(Vector3 vec)
    {
        return (vec.x.ToString() + "_" + vec.y.ToString() + "_" + vec.z.ToString());
    }


    public IEnumerable<Chunk> GetNextChunk(Vector3 playerSmallGridLoc)
    {
        //Chunk chunk = new Chunk();

        //chunk.chunkR = 1;
        //chunk.position = new Vector3(1, 1, 1);
        //chunk.state = Chunk.State.New;

        //yield return chunk;
        //yield break;

        //if (Input.GetKey(KeyCode.P))
        //{
        //    Debug.Log(
        //        StringVector(_player.transform.position) + ":" +
        //        StringVector(new Vector3(x, y, z))
        //    );
        //}

        playerLoc = playerSmallGridLoc;
        //PrintVector(playerSmallGridLoc);

        foundEdgeBigChunks.Clear();
        chunkSpaceIterators.Clear();
        var oneStartChunk = new Vector3();
        int chunkStartR = (int)Mathf.Pow(2, Mathf.Floor(Mathf.Log(terrainData.newChunkR, 2)));
        if (chunkStartR < 2) chunkStartR = 2;

        for (int xSign = -1; xSign <= 1; xSign += 2)
        {
            for (int ySign = -1; ySign <= 1; ySign += 2)
            {
                for (int zSign = -1; zSign <= 1; zSign += 2)
                {
                    oneStartChunk.x = xSign * (chunkStartR - 1) + playerSmallGridLoc.x;
                    oneStartChunk.y = ySign * (chunkStartR - 1) + playerSmallGridLoc.y;
                    oneStartChunk.z = zSign * (chunkStartR - 1) + playerSmallGridLoc.z;

                    if (oneStartChunk.x == 0) { oneStartChunk.x += playerSmallGridLoc.x > 0 ? 1 : -1; }
                    if (oneStartChunk.y == 0) { oneStartChunk.y += playerSmallGridLoc.y > 0 ? 1 : -1; }
                    if (oneStartChunk.z == 0) { oneStartChunk.z += playerSmallGridLoc.z > 0 ? 1 : -1; }

                    string str = StringVector(oneStartChunk);

                    oneStartChunk = getParentChunk(oneStartChunk, chunkStartR);

                    //Debug.Log(str + ":" + StringVector(oneStartChunk));

                    foundEdgeBigChunks.Add(oneStartChunk);
                }
            }
        }

        //foreach (var v in foundEdgeBigChunks) {
        //    PrintVector(v);
        //}

        foreach (var item in foundEdgeBigChunks.Distinct())
        {
            chunkSpaceIterators.Add(
                SectorRecursiveChildrenFind(playerSmallGridLoc, item, chunkStartR).GetEnumerator()
            );
        }

        bool hasThingsToDoo = true;

        while (hasThingsToDoo)
        {
            hasThingsToDoo = false;

            foreach (var chunkIterator in chunkSpaceIterators)
            {
                if (chunkIterator.MoveNext())
                {
                    hasThingsToDoo = true;

                    if ((chunkIterator.Current.position - playerSmallGridLoc).magnitude <= terrainData.newChunkR)
                    {
                        yield return chunkIterator.Current;
                    }
                }
            }
        }

        yield break;
    }

    public IEnumerable<Chunk> SectorRecursiveChildrenFind(Vector3 origin, Vector3 target, int size)
    {
        var children = getChildren(target, size);
        int halfSize = size / 2;

        List<Vector3> foundChildren = new List<Vector3>();
        List<Vector3> subdevidableChunks = new List<Vector3>();

        foreach (var child in children)
        {
            if (halfSize == 1)
            {
                string x = StringVector(child);

                //Debug.Log(x + ":" + StringVector(target));
            }

            switch (getNextAction(origin, child, halfSize))
            {
                case 0:
                    // Do nothing - out of bounds chunk
                    break;
                case 1:
                    foundChildren.Add(child);
                    break;
                case 2:
                    subdevidableChunks.Add(child);
                    break;
            }
        }

        List<IEnumerator<Chunk>> chunkIterators = new List<IEnumerator<Chunk>>();

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
            Chunk chunk = new Chunk();

            chunk.chunkR = size / 2;
            chunk.position = child;
            chunk.state = Chunk.State.New;

            yield return chunk;
        }

        yield break;
    }

    public int getNextAction(Vector3 origin, Vector3 child, int currentChunkR)
    {
        //if (currentChunkR == 1)
        //{
        //    return 1;
        //} else
        //{
        //    return 2;
        //}


        if (currentChunkR <= 1)
        {
            return 1;
        }

        int dist = (int)(child - origin).magnitude - 2;

        var power = (int)(Mathf.Log(dist, 2));
        var maxChunkR = Mathf.Pow(2, power);

        //Debug.Log(
        //    StringVector(child) + " # " + 
        //    StringVector(origin) + " # " +
        //    currentChunkR.ToString() + " ## " +
        //    dist.ToString() + " # " +
        //    power.ToString() + " # " +
        //    maxChunkR.ToString()
        //);

        return maxChunkR > currentChunkR ? 1 : 2;
    }

    public List<Vector3> getChildren(Vector3 inpV, int size)
    {
        string x = StringVector(inpV);

        

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

                    if (size == 4)
                    {
                        //Debug.Log(x + ":" + StringVector(foundChild));
                    }

                    children.Add(foundChild);
                }
            }
        }

        return children;
    }

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


    //public Chunk[] getChunkOrder(Vector3 playerGridLoc)
    //{
    //    List<Chunk> chunkOrderList = new List<Chunk>();

    //    Chunk chunk = new Chunk();
    //    chunk.chunkR = 1;
    //    chunk.location = new Vector3(-1, -1, -1);
    //    chunk.SetUpVariables(terrainData.segemntCountPerDimension);
    //    chunkOrderList.Add(chunk);

    //    Chunk chunk2 = new Chunk();
    //    chunk2.chunkR = 1;
    //    chunk2.location = new Vector3(1, -1, -1);
    //    chunk2.SetUpVariables(terrainData.segemntCountPerDimension);
    //    chunkOrderList.Add(chunk2);

    //    return chunkOrderList.ToArray();
    //}

    public IEnumerable<Chunk> getNextChunk()
    {
        Chunk chunk = new Chunk();
        chunk.chunkR = 2;
        chunk.position = new Vector3(2, 2, 2);

        yield return chunk;


        yield break;
    }
}
