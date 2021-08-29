using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UpdatebleData), true)]
public class UpdateableDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        UpdatebleData data = (UpdatebleData)target;

        if (GUILayout.Button("Update"))
        {
            data.NotifyOfUpdatedValues();
            EditorUtility.SetDirty(target);
        }
    }
}
