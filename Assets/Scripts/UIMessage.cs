using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

[Serializable]
public class Message
{
    public string Name;
    public string Desc;
    public DateTime ClinetSendTime;
    public DateTime ServerCheckTime;
}

public class UIMessage : MonoBehaviour
{

    [SerializeField]
    private Text Desc;

    [SerializeField]
    private Text Sender;

    [SerializeField]
    private Text RecodedTime;



    public void Initialize(in string senderName, in string message, in DateTime time)
    {
        Sender.text = senderName;
        Desc.text = message;
        RecodedTime.text = time.ToLocalTime().ToString("HH:mm:ss");
    }

}
