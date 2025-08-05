using UnityEngine;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

public class VRStreamReceiverTCP : MonoBehaviour
{
    public RawImage displayImage;
    public int port = 9999;

    private TcpListener listener;
    private Texture2D texture;
    private byte[] receivedImage;

    void Start()
    {
        Debug.Log("[Receiver] Starting listener on port " + port);
        texture = new Texture2D(2, 2);
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        Thread thread = new Thread(ListenLoop);
        thread.IsBackground = true;
        thread.Start();
    }

    void ListenLoop()
    {
        try
        {
            Debug.Log("[Receiver] Waiting for client connection...");
            TcpClient client = listener.AcceptTcpClient();
            Debug.Log("[Receiver] Client connected!");

            NetworkStream stream = client.GetStream();

            while (true)
            {
                byte[] lengthBuffer = new byte[4];
                int read = stream.Read(lengthBuffer, 0, 4);
                if (read < 4)
                {
                    Debug.LogWarning("[Receiver] Disconnected or incomplete data.");
                    break;
                }

                int imageLength = System.BitConverter.ToInt32(lengthBuffer, 0);
                receivedImage = new byte[imageLength];

                int readBytes = 0;
                while (readBytes < imageLength)
                    readBytes += stream.Read(receivedImage, readBytes, imageLength - readBytes);

                Debug.Log("[Receiver] Image received. Size: " + imageLength);

                MainThreadDispatcher.Enqueue(() =>
                {
                    Texture2D frameTexture = new Texture2D(2, 2);
                    bool loaded = frameTexture.LoadImage(receivedImage);

                    if (!loaded)
                    {
                        Debug.LogError("[Receiver] Failed to load image.");
                    }
                    else
                    {
                        displayImage.texture = frameTexture;
                        Debug.Log("[Receiver] Frame applied to RawImage.");
                    }
                });

            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Receiver] Error: " + ex.Message);
        }
    }

    void OnApplicationQuit()
    {
        listener?.Stop();
    }
}
