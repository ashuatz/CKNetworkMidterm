using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIStart : MonoBehaviour
{
    public void OnClickServer()
    {
        SceneManager.LoadScene(1);
    }

    public void OnClickClient()
    {
        SceneManager.LoadScene(2);
    }
}
