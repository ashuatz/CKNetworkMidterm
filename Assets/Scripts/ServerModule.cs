using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;


public class Connection : IDisposable
{
    public TcpClient Client { get; private set; }

    public NetworkStream ns { get; private set; }
    public StreamWriter sw { get; private set; }
    public StreamReader sr { get; private set; }

    public event Action<Connection, string> OnMessageReceived;
    public event Action<Connection, Exception> OnExceptionThrown;

    public ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    private Thread ReceiveThread;
    private Thread SendThread;

    public bool IsRunning { get; private set; }

    public Connection(in TcpClient client)
    {
        Client = client;

        ns = Client.GetStream();
        sr = new StreamReader(ns);
        sw = new StreamWriter(ns);
    }

    public void Run()
    {
        IsRunning = true;

        ReceiveThread = new Thread(Receive);
        ReceiveThread.Start();

        SendThread = new Thread(Send);
        SendThread.Start();
    }

    public void Stop(Action OnComplete = null)
    {
        IsRunning = false;

        ReceiveThread.Interrupt();
        SendThread.Interrupt();

        ReceiveThread.Join();
        SendThread.Join();
    }

    private void Receive()
    {
        string data = string.Empty;

        while (IsRunning)
        {
            try
            {
                data = sr.ReadLine();
                OnMessageReceived?.Invoke(this, data);
            }
            catch (IOException e)
            {
                OnExceptionThrown?.Invoke(this, e);
            }
            catch (OutOfMemoryException e)
            {
                OnExceptionThrown?.Invoke(this, e);
            }

        }
    }

    public void Send()
    {
        while (IsRunning)
        {
            if (messageQueue.Count <= 0)
                continue;

            if (messageQueue.TryDequeue(out var message))
            {
                try
                {
                    sw.WriteLine(message);
                    sw.Flush();
                }
                catch (IOException e)
                {
                    OnExceptionThrown?.Invoke(this, e);
                }
                catch (ObjectDisposedException e)
                {
                    OnExceptionThrown?.Invoke(this, e);
                }
            }
        }
    }

    public void Close()
    {
        sw.Close();
        sr.Close();
        ns.Close();

        Client.Close();
    }

    public void Dispose()
    {
        sw.Dispose();
        sr.Dispose();
        ns.Dispose();

        Client.Dispose();
    }
}


public class ServerModule : MonoSingleton<ServerModule>
{
    public Socket listenSocket { get; private set; }
    public TcpListener Listener { get; private set; }

    private List<Connection> connections = new List<Connection>();

    public bool IsInitialized { get; private set; }

    public event Action<Message> OnReceived;

    private Coroutine AcceptRoutine;

    private Thread AcceptThread;


    public void Initialize()
    {
        if (IsInitialized)
            return;

        base.InvokeInitialized();

        IPEndPoint serverPoint = new IPEndPoint(IPAddress.Any, 9050);

        Listener = new TcpListener(serverPoint);
        Listener.Start();

        IsInitialized = true;
    }

    public void StartAcceptRoutine()
    {
        if (AcceptRoutine != null)
            return;

        AcceptThread = new Thread(StartAccept);
        AcceptThread.Start();
    }


    public void StartAccept()
    {
        while (IsInitialized)
        {
            TcpClient client = Listener.AcceptTcpClient();

            var message_b = new Message();
            message_b.ServerCheckTime = DateTime.UtcNow;
            message_b.Desc = $"connected with {(client.Client.RemoteEndPoint as IPEndPoint).Address} at port {(client.Client.RemoteEndPoint as IPEndPoint).Port}";
            message_b.Name = "Server Log";

            OnReceived?.Invoke(message_b);

            var connection = new Connection(client);
            connection.Run();

            connection.OnExceptionThrown += Connection_OnExceptionThrown;
            connection.OnMessageReceived += Connection_OnMessageReceived;

            connections.Add(connection);
        }
    }

    private void Connection_OnMessageReceived(Connection senderConnection, string json)
    {
        var request = JsonUtility.FromJson<Request>(json);

        if(request == null)
        {
            var response_b = new Response();
            response_b.opCode = OpCode.None;
            response_b.errorCode = ErrorCode.kBadPacket;

            var response = JsonUtility.ToJson(response_b);

            senderConnection.messageQueue.Enqueue(response);
            return;
        }


        switch (request.opCode)
        {
            case OpCode.None:
            {
                Debug.LogError($"error :{request.opCode} . {request.data}");

                var response_b = new Response();
                response_b.opCode = request.opCode;
                response_b.errorCode = ErrorCode.kFieldMissing;

                var response = JsonUtility.ToJson(response_b);
                senderConnection.messageQueue.Enqueue(response);
                return;
            }

            case OpCode.SendMessage:
            {
                var response_b = new Response();
                response_b.opCode = request.opCode;
                response_b.errorCode = ErrorCode.kOk;

                var messageString = request.data;
                var message = JsonUtility.FromJson<Message>(messageString);

                message.ServerCheckTime = DateTime.UtcNow;
                response_b.data = JsonUtility.ToJson(message);

                var response = JsonUtility.ToJson(response_b);

                foreach (var con in connections)
                {
                    con.messageQueue.Enqueue(response);
                }

                //for logging
                OnReceived?.Invoke(message);
                break;
            }
        }
    }

    private void Connection_OnExceptionThrown(Connection connection, Exception e)
    {
        switch (e)
        {
            case IOException ioException:

                var message_b = new Message();
                message_b.ServerCheckTime = DateTime.UtcNow;
                message_b.Desc = $"Disconnected from { (connection.Client.Client.RemoteEndPoint as IPEndPoint).Address}";
                message_b.Name = "Server Log";

                OnReceived?.Invoke(message_b);

                connection.Stop();
                connection.OnMessageReceived -= Connection_OnMessageReceived;
                connection.OnExceptionThrown -= Connection_OnExceptionThrown;

                connections.Remove(connection);
                break;

            case ObjectDisposedException disposException:

                connection.Stop();
                connections.Remove(connection);
                break;
        }

    }
}
