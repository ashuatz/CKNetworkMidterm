using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System;
using System.Text;
using System.Collections.Concurrent;


public class ChatMessageContainer : MonoBehaviour
{
    [SerializeField]
    private GameObject ReceivedOrigin;

    [SerializeField]
    private GameObject SendOrigin;

    [SerializeField]
    private ScrollRect scrollRect;

    [SerializeField]
    private RectTransform ContainerRoot;

    [SerializeField]
    private InputField inputField;

    [SerializeField]
    private InputField nameField;

    private ConcurrentQueue<Message> receivedMessage = new ConcurrentQueue<Message>();


    private void Awake()
    {
        ClientModule.OnInitialized += (instance) => instance.OnReceived += Instance_OnMessageReceived;
        ServerModule.OnInitialized += (instance) => instance.OnReceived += Instance_OnMessageReceived;

        inputField.onEndEdit.AddListener(OnEndEdit);
    }

    private void Instance_OnMessageReceived(Message message)
    {
        //call by some Thread
        receivedMessage.Enqueue(message);
    }

    //Acess in main Thread
    private void Update()
    {
        if (receivedMessage.Count <= 0)
            return;

        if (!receivedMessage.TryDequeue(out var message))
            return;

        var instance = GameObject.Instantiate(GetMessageBox(message.Name));
        if (!instance.TryGetComponent<UIMessage>(out var target))
        {
            Debug.LogError("Component not found");
            return;
        }

        target.Initialize(message.Name, message.Desc, message.ServerCheckTime);
        target.transform.SetParent(ContainerRoot, false);
        target.transform.localPosition = Vector3.one;
        target.transform.localScale = Vector3.one;


        LayoutRebuilder.ForceRebuildLayoutImmediate(ContainerRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(ContainerRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(ContainerRoot);

        scrollRect.normalizedPosition = new Vector2(0, 0);
    }

    private GameObject GetMessageBox(in string senderName)
    {
        if (senderName.Equals(nameField.text))
        {
            return SendOrigin;
        }
        else
        {
            return ReceivedOrigin;
        }
    }

    private void OnEndEdit(string str)
    {
        var message = new Message();
        message.Name = nameField.text;
        message.ClinetSendTime = DateTime.UtcNow;
        message.Desc = str;

        ClientModule.Instance.SendMessage(OpCode.SendMessage, message);

        inputField.text = string.Empty;
    }

    private void OnDestroy()
    {
        ServerModule.OnInitialized += (instance) => instance.OnReceived -= Instance_OnMessageReceived;
        ClientModule.OnInitialized += (instance) => instance.OnReceived -= Instance_OnMessageReceived;

        inputField.onEndEdit.RemoveListener(OnEndEdit);
    }


}
