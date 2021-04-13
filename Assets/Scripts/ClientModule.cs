using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;



public class ClientModule : MonoSingleton<ClientModule>
{
    public bool IsInitialized { get; private set; }

    public event Action OnConnected;
    public event Action<Message> OnReceived;

    private NetworkStream ns { get; set; }
    private StreamReader sr { get; set; }
    private StreamWriter sw { get; set; }

    private ConcurrentQueue<string> Requests = new ConcurrentQueue<string>();

    private bool isRunning { get; set; }

    public void Initialize(in string ipAddress)
    {
        if (IsInitialized)
            return;

        base.InvokeInitialized();

        IPEndPoint serverPoint = new IPEndPoint(IPAddress.Parse(ipAddress), 9050);

        var client = new TcpClient(new IPEndPoint(IPAddress.Loopback, 0));
        try
        {
            client.Connect(serverPoint);
        }
        catch (SocketException e)
        {
            Debug.LogError("Unable to connect to server.");
            Debug.LogError(e.ToString());
            return;
        }
        catch (Exception e)
        {
            Debug.LogError("Exception thrown.." + e.Message);
            return;
        }

        ns = client.GetStream();
        sr = new StreamReader(ns);
        sw = new StreamWriter(ns);

        OnConnected?.Invoke();
    }

    public void Run()
    {
        isRunning = true;

        var wthread = new Thread(WriteRoutine);
        var rthread = new Thread(ReceiveRoutine);

        wthread.Start();
        rthread.Start();

    }

    public void WriteRoutine()
    {
        while (isRunning)
        {
            if (Requests.Count <= 0)
                continue;

            if (!Requests.TryDequeue(out var data))
                continue;

            try
            {
                sw.WriteLine(data);
                sw.Flush();
            }
            catch (IOException e)
            {
                Debug.LogError("IOException Thrown : " + e.Message);
                isRunning = false;
                break;
            }
        }
    }

    void ReceiveRoutine()
    {
        while (isRunning)
        {
            try
            {
                var json = sr.ReadLine();
                ReceiveMessage(json);

            }
            catch (IOException e)
            {
                isRunning = false;
            }
        }
    }


    public void SendMessage(in OpCode opcode, Message message)
    {
        var request = new Request();
        request.opCode = OpCode.SendMessage;

        request.data = JsonUtility.ToJson(message);

        var json = JsonUtility.ToJson(request);

        //var data = Encoding.UTF8.GetBytes(json);

        Requests.Enqueue(json);
    }

    public void ReceiveMessage(in string json)
    {
        var response = JsonUtility.FromJson<Response>(json);
        if (response.errorCode != ErrorCode.kOk)
        {
            Debug.LogError($"error :{response.opCode} with {response.errorCode}. {response.data}");
            return;
        }

        switch (response.opCode)
        {
            case OpCode.SendMessage:
                var messageString = response.data;
                var message = JsonUtility.FromJson<Message>(messageString);

                OnReceived?.Invoke(message);
                break;
        }
    }
}
