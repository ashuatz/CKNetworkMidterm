using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;


/// <summary>
/// instance. use and release NEVER recycle
/// </summary>
public class Connection 
{
    public TcpClient Client { get; private set; }

    public event Action<Connection, string> OnMessageReceived;
    public event Action<Connection, Exception> OnExceptionThrown;

    public ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    private Thread ReceiveThread;
    private Thread SendThread;
    public bool IsConnected { get => Client != null && Client.Connected; }

    public bool IsRunning { get; private set; }

    private bool isClosing { get; set; } = false;

    public Connection(in TcpClient client)
    {
        Client = client;
    }

    public void Run()
    {
        IsRunning = true;

        ReceiveThread = new Thread(Receive);
        ReceiveThread.Start();

        SendThread = new Thread(Send);
        SendThread.Start();
    }

    private void Receive()
    {
        using (var sr = new StreamReader(Client.GetStream()))
        {
            while (Client.Connected && sr.BaseStream.CanRead)
            {
                try
                {
                    Thread.Sleep(Config.NetworkUpdateTime);
                }
                catch (ThreadInterruptedException e)
                {
                    //sleep 도중 interrupt가 걸렸을 경우
                    Debug.Log("ThreadInterruptedException  : " + e);
                }

                try
                {
                    var data = sr.ReadLine();
                    //if (!string.IsNullOrEmpty(data))
                    {
                        OnMessageReceived?.Invoke(this, data);
                    }
                }
                catch (Exception e)
                {
                    OnExceptionThrown?.Invoke(this, e);
                }
            }
        }
        
    }

    public void Send()
    {
        using (var sw = new StreamWriter(Client.GetStream()))
        {
            while (Client.Connected && sw.BaseStream.CanWrite)
            {
                try
                {
                    Thread.Sleep(Config.NetworkUpdateTime);
                }
                catch (ThreadInterruptedException e)
                {
                    //sleep 도중 interrupt가 걸렸을 경우
                    Debug.Log("ThreadInterruptedException  : " + e);
                    return;
                }


                if (messageQueue.Count <= 0)
                    continue;

                if (messageQueue.TryDequeue(out var message))
                {
                    try
                    {
                        sw.WriteLine(message);
                        sw.Flush();
                    }
                    catch (Exception e)
                    {
                        OnExceptionThrown?.Invoke(this, e);
                    }
                }
            }
        }
    }

    public bool TryClose()
    {
        if (isClosing)
            return false;

        if (!IsRunning)
            return true;

        isClosing = true;

        if (Client.Connected)
        {
            try
            {
                Client.Client.LingerState = new LingerOption(true, 0);
                Client.Client.Close();
            }
            catch (SocketException e)
            {
                Debug.Log("SocketException in shutdown : " + e);
            }
            finally
            {
                Client.Close();
            }
        }

        if (ReceiveThread.IsAlive)
        {
            ReceiveThread.Abort();
            ReceiveThread.Join();
        }

        if (SendThread.IsAlive)
        {
            SendThread.Join();
        }

        return true;
    }
}


public class ServerModule : MonoSingleton<ServerModule>
{
    public bool IsInitialized { get; private set; }

    public bool IsRunning { get; private set; }

    public TcpListener Listener { get; private set; }

    private List<Connection> connections = new List<Connection>();

    private ConcurrentQueue<(Connection,Exception)> Exceptions = new ConcurrentQueue<(Connection, Exception)>();
    private ConcurrentQueue<(Connection,string)> RequestDatas = new ConcurrentQueue<(Connection, string)>();

    public event Action OnStart;
    public event Action<Message> OnReceived;

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
        if (IsRunning)
            return;

        AcceptThread = new Thread(StartAccept);
        AcceptThread.Start();

        OnStart?.Invoke();
        IsRunning = true;
    }

    private void Update()
    {
        while(Exceptions.TryDequeue(out var pair))
        {
            ProcessOnException(pair.Item1, pair.Item2);
        }

        while(RequestDatas.TryDequeue(out var pair))
        {
            ProcessRequest(pair.Item1, pair.Item2);
        }
    }

    public void StartAccept()
    {
        while (IsInitialized)
        {
            TcpClient client = Listener.AcceptTcpClient();

            var connection = new Connection(client);
            connection.Run();

            connection.OnExceptionThrown += Connection_OnExceptionThrown;
            connection.OnMessageReceived += Connection_OnMessageReceived;

            connections.Add(connection);

            //Connect message
            var message_b = new Message();
            message_b.ServerCheckTimeTick = DateTime.UtcNow.Ticks;
            message_b.Desc = $"connected with {(client.Client.RemoteEndPoint as IPEndPoint).Address} at port {(client.Client.RemoteEndPoint as IPEndPoint).Port}";
            message_b.Name = "Server Log";

            OnReceived?.Invoke(message_b);
        }
    }

    private void Connection_OnMessageReceived(Connection senderConnection, string json)
    {
        //해당 커넥션의 Received Thread 위에서 작동한다.
        //따라서 Thread를 abort하거나 할 수 없을 수 있으므로, 해당동작을 담아둔 뒤, 메인쓰레드에서 처리한다
        RequestDatas.Enqueue((senderConnection, json));
    }

    private void Connection_OnExceptionThrown(Connection connection, Exception e)
    {
        //해당 커넥션의 exception이 발생한 쓰레드에서 동작한다.
        //따라서 Thread를 abort하거나 할 수 없을 수 있으므로, 해당동작을 담아둔 뒤, 메인쓰레드에서 처리한다
        Exceptions.Enqueue((connection, e));
    }

    private void ProcessRequest(in Connection senderConnection,in string json)
    {
        var request = JsonUtility.FromJson<Request>(json);

        if (request == null)
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

                message.ServerCheckTimeTick = DateTime.UtcNow.Ticks;
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

    private void ProcessOnException(in Connection connection,in Exception e)
    {
        switch (e)
        {
            case IOException ioException:
                if (connection.TryClose())
                {
                    connection.OnMessageReceived -= Connection_OnMessageReceived;
                    connection.OnExceptionThrown -= Connection_OnExceptionThrown;

                    connections.Remove(connection);
                    ShowDisconnectMessage();
                }
                break;


            case ThreadAbortException abortException:
                if (connection.TryClose())
                {
                    connection.OnMessageReceived -= Connection_OnMessageReceived;
                    connection.OnExceptionThrown -= Connection_OnExceptionThrown;

                    connections.Remove(connection);
                    ShowDisconnectMessage();
                }
                break;

            case ObjectDisposedException disposeException:

                if (connection.TryClose())
                {
                    connection.OnMessageReceived -= Connection_OnMessageReceived;
                    connection.OnExceptionThrown -= Connection_OnExceptionThrown;

                    connections.Remove(connection);
                    ShowDisconnectMessage();
                }
                break;

            default:
                Debug.LogError("Exception invoked : " + e);
                break;
        }

        void ShowDisconnectMessage()
        {
            var message_b = new Message();
            message_b.ServerCheckTimeTick = DateTime.UtcNow.Ticks;
            message_b.Desc = $"Disconnected Client";
            message_b.Name = "Server Log";

            OnReceived?.Invoke(message_b);
        }
    }

    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();

        foreach (var connection in connections)
        {
            if (connection.TryClose())
            {
                connection.OnMessageReceived -= Connection_OnMessageReceived;
                connection.OnExceptionThrown -= Connection_OnExceptionThrown;

                connections.Remove(connection);
            }
        }
    }
}
