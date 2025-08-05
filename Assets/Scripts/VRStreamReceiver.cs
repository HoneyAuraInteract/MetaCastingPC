using UnityEngine;
using Unity.WebRTC;
using UnityEngine.UI;
using System.Collections;
using System;
using System.Reflection;
using System.Collections.Generic;  

public class VRStreamReceiver : MonoBehaviour
{
    [Header("Paste offer from sender here")]
    [TextArea(10, 30)]
    public string offerSDP;

    [Header("UI to preview stream")]
    public RawImage videoDisplay;

    private RTCPeerConnection peer;
    private VideoStreamTrack remoteVideoTrack;
    private Coroutine webRtcUpdateCoroutine;
    private Texture2D receivedTexture;
    public WebSocketSignaler signaler;
    private MethodInfo updateTextureMethod;

    private List<RTCIceCandidate> pendingCandidates = new List<RTCIceCandidate>();
    private bool peerReady = false;


void Start()
    {
        updateTextureMethod = typeof(VideoStreamTrack).GetMethod("UpdateTexture", BindingFlags.Instance | BindingFlags.NonPublic);
        // Start WebRTC update loop
        signaler.OnMessageReceived += OnMessageReceived;

        webRtcUpdateCoroutine = StartCoroutine(WebRTC.Update());

        // Start receiver setup
    }

    void OnMessageReceived(string json)
    {
        Debug.Log("Receiver got signaling message: " + json);
        SignalMessage msg = JsonUtility.FromJson<SignalMessage>(json);
    
            if (msg.type == "offer")
        {
            Debug.Log("Received offer SDP: " + msg.sdp);

            offerSDP = msg.sdp; // ✅ Set offerSDP

            // Start receiving only after getting offer

            StartCoroutine(SetUpReceiver());
        }
        else if (msg.type == "candidate")
        {
            RTCIceCandidateInit iceInit = new RTCIceCandidateInit
            {
                candidate = msg.candidate,
                sdpMid = msg.sdpMid,
                sdpMLineIndex = msg.sdpMLineIndex
            };

            RTCIceCandidate candidate = new RTCIceCandidate(iceInit);

            if (peer != null)
            {
                Debug.Log("✅ ICE candidate added immediately.");
                peer.AddIceCandidate(candidate);
            }
            else
            {
                Debug.Log("🕐 ICE candidate queued (peer not ready yet).");
                pendingCandidates.Add(candidate);
            }
        }

    }
    void Update()
    {
        if (remoteVideoTrack != null && updateTextureMethod != null)
        {
            updateTextureMethod.Invoke(remoteVideoTrack, null);
            Debug.Log("🌀 UpdateTexture() invoked manually.");
        }
    }

    private IEnumerator SetUpReceiver()
    {
        // Create peer connection
        peer = new RTCPeerConnection();
        peer.OnIceCandidate = candidate =>
        {
            SignalMessage msg = new SignalMessage
            {
                type = "candidate",
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex ?? 0
            };

            string json = JsonUtility.ToJson(msg);
            signaler.SendMessage(json);
            Debug.Log("📤 Sent ICE candidate (receiver).");
        };

        // Handle incoming track
        peer.OnTrack = e =>
        {
            Debug.Log("Received remote track.");

            if (e.Track is VideoStreamTrack videoTrack)
            {
                remoteVideoTrack = videoTrack;

                // 🔧 Call internal UpdateTexture() via reflection
                var method = typeof(VideoStreamTrack)
                    .GetMethod("UpdateTexture", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (method != null)
                {
                    method.Invoke(videoTrack, null);
                    Debug.Log($"🎯 Is Decoding: {videoTrack.Decoding}");  // true means decoder created

                    Debug.Log("✅ Called UpdateTexture() via reflection.");
                }
                else
                {
                    Debug.LogWarning("⚠️ UpdateTexture() method not found via reflection.");
                }

                // Subscribe to video frames
                videoTrack.OnVideoReceived += OnRemoteVideoFrame;
                Debug.Log("✅ Video track attached.");
            }
        };
   
        // Set the remote offer description (paste from VR device)
        var remoteDesc = new RTCSessionDescription
        {
            type = RTCSdpType.Offer,
            sdp = offerSDP
        };

        var setRemoteOp = peer.SetRemoteDescription(ref remoteDesc);
        yield return setRemoteOp;
        peerReady = true;

        // ⏬ Process queued ICE candidates
        foreach (var candidate in pendingCandidates)
        {
            peer.AddIceCandidate(candidate);
            Debug.Log("✅ Queued ICE candidate added.");
        }
        pendingCandidates.Clear();

        if (setRemoteOp.IsError)
        {
            Debug.LogError("SetRemoteDescription failed: " + setRemoteOp.Error.message);
            yield break;
        }

        Debug.Log("Remote description set. Creating answer...");

        // Create answer
        var answerOp = peer.CreateAnswer();
        yield return answerOp;

        if (answerOp.IsError)
        {
            Debug.LogError("CreateAnswer failed: " + answerOp.Error.message);
            yield break;
        }

        var answerDesc = answerOp.Desc;
        var setLocalOp = peer.SetLocalDescription(ref answerDesc);
        yield return setLocalOp;

        if (setLocalOp.IsError)
        {
            Debug.LogError("SetLocalDescription failed: " + setLocalOp.Error.message);
            yield break;
        }

        Debug.Log("✅ Answer SDP:\n" + answerDesc.sdp);
        SignalMessage answer = new SignalMessage()
        {
            type = "answer",
            sdp = answerDesc.sdp
        };

        string json = JsonUtility.ToJson(answer);
        signaler.SendMessage(json);
    }
    void OnRemoteVideoFrame(Texture texture)
    {

        Debug.Log($"Remote video frame received: {texture.width}x{texture.height}");

        if (videoDisplay != null && texture != null)
        {
            videoDisplay.texture = texture;
        }
    }

    void OnDestroy()
    {
        if (webRtcUpdateCoroutine != null)
            StopCoroutine(webRtcUpdateCoroutine);

        remoteVideoTrack?.Dispose();
        peer?.Close();
        peer?.Dispose();
        signaler.OnMessageReceived -= OnMessageReceived;

    }
}
[System.Serializable]
public class SignalMessage
{
    public string type;
    public string sdp;
    public string candidate;
    public string sdpMid;
    public int sdpMLineIndex;
}
