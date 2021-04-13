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

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void OnClickConnect()
    {
        ClientModule.Instance.OnConnected += Instance_OnConnected;
        ClientModule.Instance.Initialize(addressField.text);
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

        ClientModule.Instance.OnConnected -= Instance_OnConnected;

        ClientModule.Instance.Run();
    }
}
