using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

using System.Collections;
using System.Collections.Generic;

public class Test : MonoBehaviour
{
    public struct ListPopulatorJob : IJob
    {
        public NativeList<int> list;

        public void Execute()
        {
            for (int i = list.Length; i < list.Capacity; i++)
            {
                list.Add(i);
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
