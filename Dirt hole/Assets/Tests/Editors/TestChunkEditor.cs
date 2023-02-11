using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TestChunk))]
public class MapPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TestChunk testChunk = (TestChunk)target;

        // When value changes
        if (DrawDefaultInspector())
        {
            testChunk.ShowChanges();
        }

        // When button is pressed
        if (GUILayout.Button("Show Changes"))
        {
            testChunk.ShowChanges();
        }

        //// When button is pressed
        //if (GUILayout.Button("Try to show?"))
        //{
        //    testChunk.Magic();
        //}
    }
}
