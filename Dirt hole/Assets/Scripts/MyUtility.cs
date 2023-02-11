using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class MyUtility
{
    public string ToString(NativeArray<float> nativeArray)
    {
        string str = "";

        foreach (var item in nativeArray)
        {

            if (str != "")
            {
                str += ", ";
            }

            str += item.ToString();
        }

        return str;
    }
}
