using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class MarchingCubeInstructions
{
    private NativeList<int> instructions = new NativeList<int>(Allocator.Persistent);
    private NativeList<int> notations = new NativeList<int>(Allocator.Persistent);

    private NativeArray<float3> marchingCube = new NativeArray<float3>(8, Allocator.Persistent);

    private int maxInstructionLength = 15;
    private float threshold = 0.5f;

    private struct InBetween
    {
        public Vector3 pOne;
        public Vector3 pTwo;
        public Vector3 between;
        public InBetween(Vector3 _pOne, Vector3 _pTwo)
        {
            pOne = _pOne;
            pTwo = _pTwo;
            between = new Vector3();
        }
    }

    private struct NodeData
    {
        public float strength;

        public NodeData(float _strength)
        {
            strength = _strength;
        }
    }

    public MarchingCubeInstructions()
    {
        GenerateMarchingCube();
        GenerateNotations();
        GenerateInstructions();
    }

    public NativeArray<float3> GetMarchingCube()
    {
        return marchingCube;
    }

    public NativeList<int> GetNotations()
    {
        return notations;
    }

    public NativeList<int> GetInstructions()
    {
        return instructions;
    }

    /// <summary>
    /// Marching cube template - array of cube vertices.
    /// </summary>
    private void GenerateMarchingCube()
    {
        for (int z = 0; z < 2; z++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    marchingCube[x + y * 2 + z * 4] = new float3(x, y, z);
                }
            }
        }
    }

    /// <summary>
    /// Relation between edge number and its two vertices
    /// </summary>
    void GenerateNotations()
    {
        for (int firstV = 0; firstV < marchingCube.Length; firstV++)
        {
            for (int secondV = firstV; secondV < marchingCube.Length; secondV++)
            {
                if (CommonCordCount(marchingCube[firstV], marchingCube[secondV]) == 2)
                {
                    notations.Add(firstV);
                    notations.Add(secondV);
                }
            }
        }
    }

    int CommonCordCount(Vector3 pOne, Vector3 pTwo)
    {
        int sum = 0;
        sum += pOne.x == pTwo.x ? 1 : 0;
        sum += pOne.y == pTwo.y ? 1 : 0;
        sum += pOne.z == pTwo.z ? 1 : 0;

        return sum;
    }


    void GenerateInstructions()
    {
        NodeData[,,] nodeAttributes = new NodeData[2, 2, 2];
        Vector3[] cubeVertaces = new Vector3[8];
        bool cubeVertacesDone = false;

        for (int nr = 0; nr < 256; nr++)
        {
            var caseNumberCoppy = nr;
            var fromBiggestVertexNumber = 128;

            for (int z = 1; z >= 0; z--)
            {
                for (int y = 1; y >= 0; y--)
                {
                    for (int x = 1; x >= 0; x--)
                    {
                        if (caseNumberCoppy / fromBiggestVertexNumber >= 1)
                        {
                            caseNumberCoppy = caseNumberCoppy - fromBiggestVertexNumber;
                            nodeAttributes[x, y, z] = new NodeData(1f);
                        }
                        else
                        {
                            nodeAttributes[x, y, z] = new NodeData(0f);
                        }

                        fromBiggestVertexNumber = fromBiggestVertexNumber / 2;

                        if (!cubeVertacesDone)
                        {
                            cubeVertaces[x + y * 2 + z * 4] = new Vector3(x, y, z);
                        }
                    }
                }
            }

            cubeVertacesDone = true;

            var orderedinstructions = GetTriangleVertexOrders(cubeVertaces, nodeAttributes);

            for (int i = 0; i < maxInstructionLength; i++)
            {
                if (i < orderedinstructions.Count)
                {
                    instructions.Add(orderedinstructions[i]);
                }
                else
                {
                    // Filler. If instruction is finished then remainig spaces are set to -1
                    instructions.Add(-1);
                }
            }
        }
    }


    List<int> GetTriangleVertexOrders(Vector3[] cubeVertaces, NodeData[,,] nodeAttributes)
    {
        var triangleVertexOrders = new List<int>();

        int idnumber = 0;
        List<int> inBetweenIds = new List<int>();
        List<InBetween> inBetween = new List<InBetween>();

        for (int pointOneId = 0; pointOneId < cubeVertaces.Length; pointOneId++)
        {
            for (int pointTwoId = pointOneId; pointTwoId < cubeVertaces.Length; pointTwoId++)
            {
                if (CommonCordCount(cubeVertaces[pointOneId], cubeVertaces[pointTwoId]) == 2)
                {
                    if (IsStrong(cubeVertaces[pointOneId], nodeAttributes) != IsStrong(cubeVertaces[pointTwoId], nodeAttributes))
                    {
                        var betweenPoint = new InBetween(cubeVertaces[pointOneId], cubeVertaces[pointTwoId]);
                        betweenPoint.between = CalculateBetweenJagged(betweenPoint);
                        inBetween.Add(betweenPoint);
                        inBetweenIds.Add(idnumber);
                    }

                    idnumber++;
                }
            }
        }

        if (inBetween.Count == 0)
        {
            return triangleVertexOrders;
        }

        List<List<int>> edgeOrder = GetEdgeOrder(inBetween, cubeVertaces, nodeAttributes);

        foreach (var loop in edgeOrder)
        {
            var zroInBtw = inBetween[loop[0]];

            if (!FaceIsCorrect(
                inBetween[loop[0]].between,
                inBetween[loop[1]].between,
                inBetween[loop[2]].between,
                IsStrong(zroInBtw.pOne, nodeAttributes) ? zroInBtw.pOne : zroInBtw.pTwo)
            )
            {
                loop.Reverse();
            }

            for (int i = 2; i < loop.Count; i++)
            {
                triangleVertexOrders.Add(inBetweenIds[loop[0]]);
                triangleVertexOrders.Add(inBetweenIds[loop[i - 1]]);
                triangleVertexOrders.Add(inBetweenIds[loop[i]]);
            }
        }

        return triangleVertexOrders;
    }

    bool IsStrong(Vector3 point, NodeData[,,] nodeAttributs)
    {
        return nodeAttributs[(int)point.x, (int)point.y, (int)point.z].strength > threshold;
    }

    Vector3 CalculateBetweenJagged(InBetween input)
    {
        return (input.pOne + input.pTwo) / 2;
    }


    List<List<int>> GetEdgeOrder(List<InBetween> inBetween, Vector3[] cubeVertaces, NodeData[,,] nodeAttributes)
    {
        List<int> takenEdges = new List<int>();
        List<List<int>> allSortedLists = new List<List<int>>();

        int failsafe = 8;

        while (takenEdges.Count != inBetween.Count)
        {
            bool hasChange = true;
            int currentEdge = 0;
            int iNr = 0;
            List<int> sortedList = new List<int>();

            // Add firs
            foreach (var i in inBetween)
            {
                if (!takenEdges.Contains(iNr))
                {
                    sortedList.Add(iNr);
                    takenEdges.Add(iNr);
                    currentEdge = iNr;
                    break;
                }

                iNr++;
            }

            while (hasChange)
            {
                hasChange = false;

                for (int i = 0; i < inBetween.Count; i++)
                {
                    if (!takenEdges.Contains(i) &&
                        (
                            CommonCordCount(inBetween[i].pOne, inBetween[currentEdge].pOne) +
                            CommonCordCount(inBetween[i].pTwo, inBetween[currentEdge].pTwo) == 4
                            ||
                            CommonCordCount(inBetween[i].pOne, inBetween[currentEdge].pTwo) +
                            CommonCordCount(inBetween[i].pTwo, inBetween[currentEdge].pOne) == 4
                        ) && (
                            CanBeCrossed(inBetween[i], inBetween[currentEdge], cubeVertaces, nodeAttributes)
                        )
                    )
                    {
                        hasChange = true;
                        sortedList.Add(i);
                        takenEdges.Add(i);
                        currentEdge = i;
                    }
                }

                if (failsafe <= 0)
                {
                    Debug.Log("Problem in: GetEdgeOrder");
                    break;
                }
                else
                {
                    failsafe--;
                }
            }

            allSortedLists.Add(sortedList);
        }

        return allSortedLists;
    }

    bool CanBeCrossed(InBetween one, InBetween two, Vector3[] cubeVertaces, NodeData[,,] nodeAttributs)
    {
        if (
            !(CommonCordCount(one.pOne, two.pOne) == 3 ||
            CommonCordCount(one.pOne, two.pTwo) == 3 ||
            CommonCordCount(one.pTwo, two.pOne) == 3 ||
            CommonCordCount(one.pTwo, two.pTwo) == 3)
        )
        {
            // If paralel check if strong is same side
            Vector3 str1 = GetNodeData(one.pOne, nodeAttributs).strength > threshold ? one.pOne : one.pTwo;
            Vector3 str2 = GetNodeData(two.pOne, nodeAttributs).strength > threshold ? two.pOne : two.pTwo;

            if (CommonCordCount(str1, str2) != 2)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        Vector3 common = CommonCordCount(one.pOne, two.pOne) == 3 || CommonCordCount(one.pOne, two.pTwo) == 3 ? one.pOne : one.pTwo;
        var other1 = CommonCordCount(one.pOne, common) == 3 ? one.pTwo : one.pOne;
        var other2 = CommonCordCount(two.pOne, common) == 3 ? two.pTwo : two.pOne;

        Vector3 cross = new Vector3();

        foreach (var item in cubeVertaces)
        {
            if (CommonCordCount(item, other1) == 2 && CommonCordCount(item, other2) == 2 && CommonCordCount(item, common) != 3)
            {
                cross = item;
            }
        }

        if (GetNodeData(common, nodeAttributs).strength > threshold && GetNodeData(cross, nodeAttributs).strength > threshold)
        {
            return false;
        }

        return true;
    }

    NodeData GetNodeData(Vector3 point, NodeData[,,] nodeAttributs)
    {
        return nodeAttributs[(int)point.x, (int)point.y, (int)point.z];
    }

    bool FaceIsCorrect(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 strongPoint)
    {
        var side1 = p2 - p1;
        var side2 = p3 - p1;
        var perp = Vector3.Cross(side1, side2);

        if (Vector3.Distance(p1 + perp, strongPoint) > Vector3.Distance(p1 - perp, strongPoint))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
