using UnityEngine;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Video;
using System;

public class TCPClientManager : MonoBehaviour
{
    private TcpClient client;
    private StreamReader reader;
    private StreamWriter writer;
    private Thread clientThread;

    public string serverIP = "192.168.1.199"; // ✅ Replace with Quest local IP
    private int port = 7777;
    public TMP_InputField messageInputField;
    public List<ButtonInfo> buttonInfos;
    public GameObject activityPanel;
    public GameObject homePanel;
    public Button goBtn;
    public Button backBtn;
    public TMP_InputField IpInputField;
    private void Awake()
    {
        if (goBtn != null) goBtn.onClick.AddListener(WorkOnGoButton);
           
        if (backBtn != null) backBtn.onClick.AddListener(() => { OpenClosePanel(false); });

    }

    private void OpenClosePanel(bool value)
    {
        activityPanel.SetActive(value);
        homePanel.SetActive(!value);
    }

    void WorkOnGoButton()
    {
            if (!string.IsNullOrWhiteSpace(IpInputField.text))
            {
                serverIP = IpInputField.text;
                OpenClosePanel(true);
                StartServer();
            }
    }

    void StartServer()
    {
        clientThread = new Thread(ConnectToServer);
        clientThread.IsBackground = true;
        clientThread.Start();

        foreach (ButtonInfo item in buttonInfos)
        {
            item.button.gameObject.GetComponent<Image>().sprite = item.tumbnailImage;
            item.highlightImage.gameObject.SetActive(false);
            item.button.onClick.AddListener(() => { SendVideoRequest(item.videoName); });
        }

    }

    void ConnectToServer()
    {
        try
        {
            client = new TcpClient(serverIP, port);
            Debug.Log("Connected to VR server!");

            NetworkStream stream = client.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream) { AutoFlush = true };

            // ✅ Send test message
           // MessageInfo messageInfo = new MessageInfo("mobile", "Hello from Android client!");

           // writer.WriteLine(JsonUtility.ToJson(messageInfo));

            while (true)
            {
                string message = reader.ReadLine();
                if (!string.IsNullOrEmpty(message))
                {
                    Debug.Log("Received from server: " + message);
                }
            }
        }
        catch (SocketException ex)
        {
            Debug.LogError("Socket Error: " + ex.Message);
        }
    }

    public void SendChatMessage()
    {
        if (writer != null && !string.IsNullOrWhiteSpace(messageInputField.text))
        {
            string msg = messageInputField.text;
            MessageInfo messageInfo = new MessageInfo("mobile", msg);
            writer.WriteLine(JsonUtility.ToJson(messageInfo));
            Debug.Log($"Sent msg: {msg} and messageInfo: {JsonUtility.ToJson(messageInfo)}");
            messageInputField.text = ""; // clear input
        }
    }

    public void SendVideoRequest(VideoName videoId)
    {
        foreach (ButtonInfo item in buttonInfos)
        {
            item.highlightImage.gameObject.SetActive(false);
        }


        ButtonInfo buttonInfo = buttonInfos.Find(x => x.videoName == videoId);
        buttonInfo.highlightImage.gameObject.SetActive(true);
        if (writer != null)
        {
            try
            {
                Thread.Sleep(200);
                writer.WriteLine(videoId);
                Debug.Log($"videoid - {videoId}");
            }
            catch (IOException ex)
            {
                Debug.LogError("Send error: " + ex.Message);
                // Optionally, reconnect if the connection is lost:
                Reconnect();
            }
        }
    }
    void Reconnect()
    {
        // Close current connection
        client?.Close();
        // Optionally wait a moment, then:
        clientThread = new Thread(ConnectToServer);
        clientThread.IsBackground = true;
        clientThread.Start();
    }
    void OnApplicationQuit()
    {
        client?.Close();
        clientThread?.Abort();
    }
    private void OnDestroy()
    {
        goBtn.onClick.RemoveAllListeners();
        backBtn.onClick.RemoveAllListeners();
    }

}

[System.Serializable]
public class MessageInfo
{
    public string sender; //either 'vr' or 'mobile'
    public string message;

    public MessageInfo(string sender, string message)
    {
        this.sender = sender;
        this.message = message;
    }
}

[System.Serializable]
public class ButtonInfo
{
    public Button button;
    public VideoName videoName;
    public Sprite tumbnailImage;
    public VideoClip videoClip;
    public Image highlightImage;
}

public enum VideoName
{
    cementProduction,
    fabrication,
    fishing,
    manufacturingProcess,
}

