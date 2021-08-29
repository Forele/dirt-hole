using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MeshGenerator
{
    public static MeshData GenerateTerraneMesh(float[,] heightMap, MeshSettings meshSettings, int levelOfDetail)
    {
        int skipIncrement = levelOfDetail == 0 ? 1 : levelOfDetail * 2;
        int numVertsPerLine = meshSettings.numVertsPerLine;

        Vector2 topLeft = new Vector2(-1, 1) * meshSettings.meshWorldSize / 2f;

        MeshData meshData = new MeshData(numVertsPerLine, skipIncrement, meshSettings.useFlatShading);

        int[,] vertexIndecesMap = new int[numVertsPerLine, numVertsPerLine];
        int meshVertexIndex = 0;
        int outOfMeshVertexIndex = -1;

        for (int y = 0; y < numVertsPerLine; y ++)
        {
            for (int x = 0; x < numVertsPerLine; x ++)
            {
                bool isOutOfMeshVertex = y == 0 || y == numVertsPerLine - 1 || x == 0 || x == numVertsPerLine - 1;
                bool isSkippedVertex = 
                    x > 2 && x < numVertsPerLine - 3 && 
                    y > 2 && y < numVertsPerLine - 3 && 
                    ((x - 2) % skipIncrement != 0 || (y - 2) % skipIncrement != 0);

                if (isOutOfMeshVertex)
                {
                    vertexIndecesMap[x, y] = outOfMeshVertexIndex;
                    outOfMeshVertexIndex--;
                } else if (!isSkippedVertex)
                {
                    vertexIndecesMap[x, y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }


        for (int y = 0; y < numVertsPerLine; y ++)
        {
            for (int x = 0; x < numVertsPerLine; x ++)
            {
                bool isSkippedVertex =
                       x > 2 && x < numVertsPerLine - 3 &&
                       y > 2 && y < numVertsPerLine - 3 &&
                       ((x - 2) % skipIncrement != 0 || (y - 2) % skipIncrement != 0);

                if (isSkippedVertex)
                {
                    continue;
                }

                bool isOutOfMeshVertex = y == 0 || y == numVertsPerLine - 1 || x == 0 || x == numVertsPerLine - 1;
                bool isMeshEdgeVertex = (y == 1 || y == numVertsPerLine - 2 || x == 1 || x == numVertsPerLine - 2) && !isOutOfMeshVertex;
                bool isMainVertex = (x - 2) % skipIncrement == 0 && (y - 2) % skipIncrement == 0 && !isOutOfMeshVertex && !isMeshEdgeVertex;
                bool isEdgeConnectionVertex =
                    (y == 2 || y == numVertsPerLine - 3 || x == 2 || x == numVertsPerLine - 3) &&
                    !isOutOfMeshVertex && !isMeshEdgeVertex && !isMainVertex;

                int vertexIndex = vertexIndecesMap[x, y];
                Vector2 percent = new Vector2(x - 1, y - 1) / (numVertsPerLine - 3);
                Vector2 vertexPosition2D = topLeft + new Vector2(percent.x, -percent.y) * meshSettings.meshWorldSize;
                float height = heightMap[x, y];

                if (isEdgeConnectionVertex)
                {
                    bool isVertical = x == 2 || x == numVertsPerLine - 3;
                    int dstToMainVertexA = (isVertical ? y - 2 : x - 2) % skipIncrement;
                    int dstToMainVertexB = skipIncrement - dstToMainVertexA;
                    float dstPercentFromAToB = dstToMainVertexA / (float)skipIncrement;

                    float heightMainVertexA = heightMap[(isVertical) ? x : x - dstToMainVertexA, (isVertical ? y - dstToMainVertexA : y)];
                    float heightMainVertexB = heightMap[(isVertical) ? x : x + dstToMainVertexB, (isVertical ? y + dstToMainVertexB : y)];

                    height = heightMainVertexA * (1 - dstPercentFromAToB) + heightMainVertexB * dstPercentFromAToB;
                }

                meshData.AddVertex(new Vector3(vertexPosition2D.x, height, vertexPosition2D.y), percent, vertexIndex);

                bool createTriangle = x < numVertsPerLine - 1 && y < numVertsPerLine - 1 && (!isEdgeConnectionVertex || (x!=2&&y!=2));

                if (createTriangle)
                {
                    int currentIncrement = (isMainVertex && x != numVertsPerLine - 3 && y != numVertsPerLine - 3) ? skipIncrement : 1;

                    int a = vertexIndecesMap[x, y];
                    int b = vertexIndecesMap[x + currentIncrement, y];
                    int c = vertexIndecesMap[x, y + currentIncrement];
                    int d = vertexIndecesMap[x + currentIncrement, y + currentIncrement];

                    meshData.AddTriangle(a, d, c);
                    meshData.AddTriangle(d, a, b);
                }
            }
        }

        meshData.ProcessMesh();

        return meshData;
    }
}

public class MeshData
{
    Vector3[] vertaces;
    int[] triangles;
    Vector2[] uvs;
    Vector3[] bakedNormals;

    Vector3[] outOfMeshVertaces;
    int[] outOfMeshTriangles;

    int triangleIndex;
    int outOfMeshTriangleIndex;

    bool useFlatShadeing;

    public MeshData(int numVertsPerLine, int skipIncrement, bool useFlatShadeing)
    {
        this.useFlatShadeing = useFlatShadeing;

        int numMeshEdgeVertaces = (numVertsPerLine - 2) * 4 - 4;
        int numEdgeConnectionVertaces = (skipIncrement - 1) * (numVertsPerLine - 5) / skipIncrement * 4;
        int numMainVertivesPerLine = (numVertsPerLine - 5) / skipIncrement + 1;
        int numMainVertaces = numMainVertivesPerLine * numMainVertivesPerLine;

        vertaces = new Vector3[numMeshEdgeVertaces + numEdgeConnectionVertaces + numMainVertaces];
        uvs = new Vector2[vertaces.Length];

        int numMeshEdgeTriangles = ((numVertsPerLine - 3) * 4 - 4) * 2;
        int numMainTriangles = (numVertsPerLine - 1) * (numVertsPerLine - 1) * 2;

        triangles = new int[(numMeshEdgeTriangles + numMainTriangles) * 3];

        outOfMeshVertaces = new Vector3[numVertsPerLine * 4 - 4];
        outOfMeshTriangles = new int[((numVertsPerLine - 1) * 4 - 4) * 2 * 3];
    }

    public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex)
    {
        if (vertexIndex < 0)
        {
            outOfMeshVertaces[- vertexIndex - 1] = vertexPosition;
        } else
        {
            vertaces[vertexIndex] = vertexPosition;
            uvs[vertexIndex] = uv;
        }
    }

    public void AddTriangle(int a, int b, int c)
    {
        if (a < 0 || b < 0 || c < 0)
        {
            outOfMeshTriangles[outOfMeshTriangleIndex] = a;
            outOfMeshTriangles[outOfMeshTriangleIndex + 1] = b;
            outOfMeshTriangles[outOfMeshTriangleIndex + 2] = c;
            outOfMeshTriangleIndex += 3;
        } else
        {
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }
    }

    Vector3[] CalculateNormals()
    {
        Vector3[] vertexNormals = new Vector3[vertaces.Length];
        int triangleCount = triangles.Length / 3;

        for (int i = 0; i < triangleCount; i++)
        {
            int normaltriangleIndex = i * 3;
            int vertexIndexA = triangles[normaltriangleIndex];
            int vertexIndexB = triangles[normaltriangleIndex + 1];
            int vertexIndexC = triangles[normaltriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;
        }

        int borderTriangleCount = outOfMeshTriangles.Length / 3;

        for (int i = 0; i < borderTriangleCount; i++)
        {
            int normaltriangleIndex = i * 3;
            int vertexIndexA = outOfMeshTriangles[normaltriangleIndex];
            int vertexIndexB = outOfMeshTriangles[normaltriangleIndex + 1];
            int vertexIndexC = outOfMeshTriangles[normaltriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);

            if (vertexIndexA >= 0)
            {
                vertexNormals[vertexIndexA] += triangleNormal;
            }

            if (vertexIndexB >= 0)
            {
                vertexNormals[vertexIndexB] += triangleNormal;
            }

            if (vertexIndexC >= 0)
            {
                vertexNormals[vertexIndexC] += triangleNormal;
            }
        }

        for (int i = 0; i < vertexNormals.Length; i++)
        {
            vertexNormals[i].Normalize();
        }

        return vertexNormals;
    }

    Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC)
    {
        Vector3 pointA = indexA < 0 ? outOfMeshVertaces[-indexA - 1] : vertaces[indexA];
        Vector3 pointB = indexB < 0 ? outOfMeshVertaces[-indexB - 1] : vertaces[indexB];
        Vector3 pointC = indexC < 0 ? outOfMeshVertaces[-indexC - 1] : vertaces[indexC];

        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }

    public void ProcessMesh()
    {
        if (useFlatShadeing)
        {
            FlatShading();
        } else
        {
            BakeNormals();
        }
    }

    void BakeNormals()
    {
        bakedNormals = CalculateNormals();
    }

    void FlatShading()
    {
        Vector3[] flatShadedVertaces = new Vector3[triangles.Length];
        Vector2[] flatShadedUvs = new Vector2[triangles.Length];


        for (int i = 0; i < triangles.Length; i++)
        {
            flatShadedVertaces[i] = vertaces[triangles[i]];
            flatShadedUvs[i] = uvs[triangles[i]];

            triangles[i] = i;
        }

        vertaces = flatShadedVertaces;
        uvs = flatShadedUvs;
    }

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertaces;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        if (useFlatShadeing)
        {
            mesh.RecalculateNormals();
        } else
        {
            mesh.normals = bakedNormals;
        }

        return mesh;
    }
}
