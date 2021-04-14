using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

using System;
using System.Net;
using System.Net.Sockets;

using System.IO;
using System.Threading;

using UnityEngine;



public class ClientModule : MonoSingleton<ClientModule>
{
    public bool IsInitialized { get; private set; }

    public bool IsConnected { get; private set; }
    public bool isRunning { get; private set; }

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<Message> OnReceived;


    private TcpClient Client { get; set; }
    private NetworkStream ns { get; set; }
    private StreamReader sr { get; set; }
    private StreamWriter sw { get; set; }

    private ConcurrentQueue<string> Requests = new ConcurrentQueue<string>();

    private Thread ReceiveThread;
    private Thread SendThread;


    public void Initialize(in string ipAddress)
    {
        if (IsInitialized)
            return;

        base.InvokeInitialized();

        IPEndPoint serverPoint = new IPEndPoint(IPAddress.Parse(ipAddress), 9050);

        Client = new TcpClient(new IPEndPoint(IPAddress.Loopback, 0));
        try
        {
            Client.Connect(serverPoint);
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

        ns = Client.GetStream();
        sr = new StreamReader(ns);
        sw = new StreamWriter(ns);

        IsConnected = true;
        OnConnected?.Invoke();
    }

    public void Run()
    {
        if (!IsConnected || isRunning)
            return;

        isRunning = true;

        SendThread = new Thread(WriteRoutine);
        ReceiveThread = new Thread(ReceiveRoutine);

        ReceiveThread.Start();
        SendThread.Start();
    }

    public void Close()
    {
        if (!isRunning)
            return;

        isRunning = false;

        SendThread.Interrupt();
        ReceiveThread.Interrupt();

        SendThread.Join();
        ReceiveThread.Join();


        sw.Close();
        sr.Close();
        ns.Close();

        Client.Close();

        IsConnected = false;

        OnDisconnected?.Invoke();
    }

    public void WriteRoutine()
    {
        while (isRunning)
        {
            Thread.Sleep(Config.NetworkUpdateTime);

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
            catch (Exception e)
            {
                WriteExceptionMessage(e);
            }
        }
    }

    void ReceiveRoutine()
    {
        while (isRunning)
        {
            Thread.Sleep(Config.NetworkUpdateTime);

            try
            {
                   var json = sr.ReadLine();
                ReceiveMessage(json);
            }
            catch (IOException e)
            {
                isRunning = false;
            }
            catch (Exception e)
            {
                WriteExceptionMessage(e);
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

    private void WriteExceptionMessage(in Exception e)
    {
        var message_b = new Message();
        message_b.ServerCheckTimeTick = DateTime.UtcNow.Ticks;
        message_b.Desc = $"Exception thrown. : { e.Message }";
        message_b.Name = "Log";

        OnReceived?.Invoke(message_b);
    }
}
