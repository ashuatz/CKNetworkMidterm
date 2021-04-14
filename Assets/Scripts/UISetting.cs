using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UISetting : MonoBehaviour
{
    [SerializeField]
    private InputField addressField;

    [SerializeField]
    private Button connectButton;

    [SerializeField]
    private Button disconnectButton;

    private void Awake()
    {
        gameObject.SetActive(false);

        if (disconnectButton != null)
            disconnectButton.interactable = false;
    }

    public void OnClickConnect()
    {
        ClientModule.Instance.OnConnected += Instance_OnConnected;
        ClientModule.Instance.OnDisconnected += Instance_OnDisconnected;

        ClientModule.Instance.Initialize(addressField.text);
    }

    public void OnClickDisconnnect()
    {
        if (disconnectButton != null)
            disconnectButton.interactable = false;

        ClientModule.Instance.Close();
    }

    private void Instance_OnDisconnected()
    {
        addressField.interactable = true;
        connectButton.interactable = true;

        ClientModule.Instance.OnDisconnected -= Instance_OnDisconnected;
    }

    public void OnClickServerInitialize()
    {
        ServerModule.Instance.Initialize();
        ServerModule.Instance.StartAcceptRoutine();
    }

    private void Instance_OnConnected()
    {
        addressField.interactable = false;
        connectButton.interactable = false;


        if (disconnectButton != null)
            disconnectButton.interactable = true;

        ClientModule.Instance.OnConnected -= Instance_OnConnected;

        ClientModule.Instance.Run();
    }
}
