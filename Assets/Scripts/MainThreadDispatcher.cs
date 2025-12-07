using UnityEngine;
using System;
using System.Collections.Generic;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher _instance;
    private readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static MainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            // Find an existing instance in the scene
            _instance = FindObjectOfType<MainThreadDispatcher>();

            // If no instance exists, create a new GameObject and add the dispatcher to it
            if (_instance == null)
            {
                GameObject singleton = new GameObject("MainThreadDispatcher");
                _instance = singleton.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(singleton);
            }
        }
        return _instance;
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
}
