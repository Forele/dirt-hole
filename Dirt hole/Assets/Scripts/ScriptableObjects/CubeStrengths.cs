using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/CubeStrengths")]
public class CubeStrengths : ScriptableObject
{
    [Range(0f, 1f)]
    public float p1 = 0f;
    [Range(0f, 1f)]
    public float p2 = 0f;
    [Range(0f, 1f)]
    public float p3 = 0f;
    [Range(0f, 1f)]
    public float p4 = 0f;
    [Range(0f, 1f)]
    public float p5 = 0f;
    [Range(0f, 1f)]
    public float p6 = 0f;
    [Range(0f, 1f)]
    public float p7 = 0f;
    [Range(0f, 1f)]
    public float p8 = 0f;
}
