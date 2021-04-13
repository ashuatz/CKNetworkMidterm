using System.Collections;
using System;

using UnityEngine;

public enum OpCode
{
    None,
    SendMessage,
}
public enum ErrorCode
{
    None,

    kOk = 100,

    kBadPacket = 201,
    kFieldMissing = 202,


    kNullReference = 301,
}


[Serializable]
public class Request
{
    public OpCode opCode;

    public string data;

}

[Serializable]
public class Response
{
    public OpCode opCode;

    public ErrorCode errorCode;

    public string data;
}