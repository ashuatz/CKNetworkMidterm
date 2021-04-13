using System.Collections;
using UnityEngine;
using System;
using System.Text;
using System.Collections.Concurrent;

using System.Net;
using System.Net.Sockets;

using System.Threading;
using System.IO;
using System.Collections.Generic;

public abstract class Singleton<T> where T : class, new()
{
    private static readonly Lazy<T> instance = new Lazy<T>(() => new T());

    public static T Instance
    {
        get
        {
            return instance.Value;
        }
    }

    protected Singleton() { }
}

public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    protected static T _instance;

    private static object _lock = new object();

    public static T Instance
    {
        get
        {
            if (applicationIsQuitting)
            {
                Debug.LogWarning("[Singleton] Instance '" + typeof(T) +
                    "' already destroyed on application quit." +
                    " Won't create again - returning null.");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = (T)FindObjectOfType(typeof(T));

                    if (FindObjectsOfType(typeof(T)).Length > 1)
                    {
                        Debug.LogError("[Singleton] Something went really wrong " +
                            " - there should never be more than 1 singleton!" +
                            " Reopening the scene might fix it.");
                        return _instance;
                    }

                    if (_instance == null)
                    {
                        GameObject singleton = new GameObject();
                        _instance = singleton.AddComponent<T>();
                        singleton.name = "(singleton) " + typeof(T).ToString();

                        DontDestroyOnLoad(singleton);
                        Debug.Log("[Singleton] An instance of " + typeof(T) +
                            " is needed in the scene, so '" + singleton +
                            "' was created with DontDestroyOnLoad.");
                    }
                    else
                    {
                        Debug.Log("[Singleton] Using instance already created: " +
                            _instance.gameObject.name);
                    }
                }

                return _instance;
            }
        }
    }

    private static event Action<T> onInitialized;
    public static event Action<T> OnInitialized
    {
        add
        {
            if (_instance != null)
            {
                value?.Invoke(_instance);
                return;
            }

            onInitialized += value;
        }
        remove
        {
            onInitialized -= value;
        }
    }

    private static bool applicationIsQuitting = false;
    /// <summary>
    /// When Unity quits, it destroys objects in a random order.
    /// In principle, a Singleton is only destroyed when application quits.
    /// If any script calls Instance after it have been destroyed, 
    ///   it will create a buggy ghost object that will stay on the Editor scene
    ///   even after stopping playing the Application. Really bad!
    /// So, this was made to be sure we're not creating that buggy ghost object.
    /// </summary>
    protected virtual void OnApplicationQuit()
    {
        applicationIsQuitting = true;
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
    }

    protected void InvokeInitialized()
    {
        if (_instance == null)
            return;

        //invoke added handler
        onInitialized?.Invoke(_instance);
        onInitialized = null;
    }
}

//public interface IBaseModule
//{
//    public event Action<Message> OnReceived;
//    public bool IsInitialized { get; }

//    public void Initialize();

//    public void Run();

//    public void Send(in string data);

//    public void WriteRoutine();
//}


//public class ServerModule
//{
//}




////client dummy
//public class NetworkModule : Singleton<NetworkModule>
//{
//    public const int MAX_PACKET_SIZE = 1024;

//    public event Action<Message> OnMessageReceived;

//    public bool isInitialized { get; private set; }

//    public string myName { get; private set; }

//    private IBaseModule baseModule;

//    public void Initialize(bool isClient)
//    {
//        if (isClient)
//        {
//            baseModule = new ClientModule();
//        }
//        else
//        {
//            baseModule = new ServerModule();
//        }

//        baseModule.Initialize();
//    }


//    public void SendMessage(in OpCode opcode, Message message)
//    {
//        var request = new Request();
//        request.opCode = OpCode.SendMessage;

//        request.data = JsonUtility.ToJson(message);

//        var json = JsonUtility.ToJson(request);

//        //var data = Encoding.UTF8.GetBytes(json);

//        baseModule.Send(json);
//    }

//    public void ReceiveMessage(in string json)
//    {
//        //var json = Encoding.UTF8.GetString(data);
//        var response = JsonUtility.FromJson<Response>(json);
//        if (response.errorCode != ErrorCode.kOk)
//        {
//            Debug.LogError($"error :{response.opCode} with {response.errorCode}. {response.data}");
//            return;
//        }

//        switch (response.opCode)
//        {
//            case OpCode.SendMessage:
//                var messageString = (response.data);
//                var message = JsonUtility.FromJson<Message>(messageString);

//                OnMessageReceived?.Invoke(message);
//                break;
//        }
//    }

//    public void ReceiveMessage_server(in string json)
//    {
//        //var json = Encoding.UTF8.GetString(data);
//        var request = JsonUtility.FromJson<Request>(json);
//        if (request.opCode != OpCode.None)
//        {
//        }

//        switch (request.opCode)
//        {
//            case OpCode.None:
//                Debug.LogError($"error :{request.opCode} . {request.data}");


//                return;

//            case OpCode.SendMessage:
//                var messageString = Encoding.UTF8.GetString(request.data);
//                var message = JsonUtility.FromJson<Message>(messageString);

//                OnMessageReceived?.Invoke(message);
//                break;
//        }
//    }

//}