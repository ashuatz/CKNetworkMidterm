using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;


public class UIMessage : MonoBehaviour
{

    [SerializeField]
    private Text Desc;

    [SerializeField]
    private Text Sender;

    [SerializeField]
    private Text RecodedTime;


    public void Initialize(in string senderName, in string message, in long tick)
    {
        Sender.text = senderName;
        Desc.text = message;
        RecodedTime.text = new DateTime(tick).ToLocalTime().ToString("HH:mm:ss");
    }

}
