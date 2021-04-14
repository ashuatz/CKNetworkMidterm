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

    public bool IsConnected { get => Client != null && Client.Connected; }
    public bool isRunning { get; private set; }
    private bool isClosing { get; set; } = false;

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<Message> OnReceived;


    private TcpClient Client { get; set; }

    private ConcurrentQueue<string> Requests = new ConcurrentQueue<string>();

    private ConcurrentQueue<Exception> Exceptions = new ConcurrentQueue<Exception>();
    private ConcurrentQueue<string> Responses = new ConcurrentQueue<string>();

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

        isClosing = false;
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
        if (!isRunning || isClosing)
            return;

        isClosing = true;

        //ReceiveThread는 readLine이 해당 쓰레드를 block 하기때문에 Abort를 해야함.

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
                //TCPClient.Close는 내부적으로 NetworkStream.Close를 호출함
                Client.Close();
            }
        }

        if (ReceiveThread.IsAlive)
        {
            //IOBlocking으로 인한 ReceiveThread abort
            ReceiveThread.Abort();
            ReceiveThread.Join();
        }

        if (SendThread.IsAlive)
            SendThread.Join();

        isRunning = false;

        OnReceived?.Invoke(BuildSimpleMessage($"Disconnected"));

        OnDisconnected?.Invoke();
    }

    public void WriteRoutine()
    {
        //알아서 dispose 하도록
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
                    return;
                }

                if (Requests.Count <= 0)
                    continue;

                if (!Requests.TryDequeue(out var data))
                    continue;

                try
                {
                    sw.WriteLine(data);
                    sw.Flush();
                }
                catch (Exception e)
                {
                    Exceptions.Enqueue(e);
                }
            }
        }
    }

    void ReceiveRoutine()
    {
        //알아서 dispose 하도록
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
                    return;
                }

                try
                {
                    var json = sr.ReadLine();
                    //if (!string.IsNullOrEmpty(json))
                    Responses.Enqueue(json);
                }
                catch(Exception e)
                {
                    Exceptions.Enqueue(e);
                }
            }
        }
    }


    private void Update()
    {
        while (Responses.TryDequeue(out var json))
        {
            ProcessResponse(json);
        }
        while(Exceptions.TryDequeue(out var e))
        {
            ProcessException(e);
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

    private void ProcessResponse(in string json)
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

    private void ProcessException(in Exception e)
    {
        switch (e)
        {
            case IOException ioException:
                Close();
                break;

            default:
                WriteExceptionMessage(e);
                break;
        }
    }

    private void WriteExceptionMessage(in Exception e)
    {
        OnReceived?.Invoke(BuildSimpleMessage($"Exception thrown. : { e.Message }"));
    }

    private Message BuildSimpleMessage(in string Desc)
    {
        var message_b = new Message();
        message_b.ServerCheckTimeTick = DateTime.UtcNow.Ticks;
        message_b.Desc = Desc;
        message_b.Name = "Log";

        return message_b;
    }
}
