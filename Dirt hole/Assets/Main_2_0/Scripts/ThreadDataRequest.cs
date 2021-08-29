using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class ThreadDataRequest : MonoBehaviour
{
    static ThreadDataRequest instance;
    Queue<ThreadInfo> dataQueue = new Queue<ThreadInfo>();

    private void Awake()
    {
        instance = FindObjectOfType<ThreadDataRequest>();
    }

    /// <summary>
    /// Wraps the task at hand in a thread
    /// </summary>
    public static void RequestData(Func<object> generateData, Action<object> callback)
    {
        ThreadStart threadStart = delegate
        {
            instance.DataThread(generateData, callback);
        };

        new Thread(threadStart).Start();
    }

    /// <summary>
    /// Sets task as done and ready for a callback function
    /// </summary>
    void DataThread(Func<object> generateData, Action<object> callBack)
    {
        object data = generateData();

        lock (dataQueue)
        {
            dataQueue.Enqueue(new ThreadInfo(callBack, data));
        }
    }

    /// <summary>
    /// Triggers callbacks when elements found in any queue
    /// </summary>
    private void Update()
    {
        if (dataQueue.Count > 0)
        {
            for (int i = 0; i < dataQueue.Count; i++)
            {
                ThreadInfo threadInfo = dataQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
                break;
            }
        }
    }

    struct ThreadInfo
    {
        public readonly Action<object> callback;
        public readonly object parameter;

        public ThreadInfo(Action<object> callback, object parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}
