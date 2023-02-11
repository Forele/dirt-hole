using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainData))]
public class TerrainDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        //TestChunk testChunk = (TerrainData)target.;

        // When value changes
        if (DrawDefaultInspector())
        {
            //Debug.Log("Change");
            //testChunk.ShowChanges();
        }

        // When button is pressed
        //if (GUILayout.Button("Show Changes"))
        //{
        //    //testChunk.ShowChanges();
        //}

        //// When button is pressed
        //if (GUILayout.Button("Try to show?"))
        //{
        //    testChunk.Magic();
        //}
    }
}
