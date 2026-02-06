// NDI SDK for Unity is licensed for non-commercial use only.
// By using this script you agree to the NDI SDK non-commercial license terms.

using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

#if NDI_SDK_ENABLED
using NewTek.NDI;
#endif

namespace NDI
{
    public enum NDIReceiverState
    {
        Idle,
        Connecting,
        Connected,
        Reconnecting,
        Error
    }

    [Serializable]
    public struct NDIFrameMetrics
    {
        public int Width;
        public int Height;
        public float FramesPerSecond;
        public float LatencyMilliseconds;
    }

    public sealed class NDIReceiver : MonoBehaviour
    {
        [SerializeField] private float reconnectDelaySeconds = 2f;
        [SerializeField] private int maxReconnectAttempts = 5;
        [SerializeField] private float noFrameTimeoutSeconds = 2f;

        public event Action<NDIReceiverState> StateChanged;
        public event Action<string> ErrorChanged;
        public event Action<NDIFrameMetrics> MetricsUpdated;
        public event Action<Texture2D> FrameUpdated;

        public NDIReceiverState State { get; private set; } = NDIReceiverState.Idle;
        public string ErrorMessage { get; private set; }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public NDIFrameMetrics Metrics { get; private set; }
        public NDISourceInfo SelectedSource { get; private set; }
        public Texture2D VideoTexture => videoTexture;

        private Coroutine reconnectCoroutine;
        private Texture2D videoTexture;
        private byte[] frameBuffer;
        private float lastFrameReceivedTime;

#if NDI_SDK_ENABLED
        private NDIlib.recv_instance_t receiverInstance;
        private NDIlib.recv_create_v3_t receiverConfig;
        private NDIlib.video_frame_v2_t videoFrame;
        private NDIlib.audio_frame_v2_t audioFrame;
        private NDIlib.metadata_frame_t metadataFrame;
#endif

        private void OnDisable()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void Connect(NDISourceInfo source)
        {
            if (SelectedSource == source && State == NDIReceiverState.Connected)
            {
                return;
            }

            SelectedSource = source;
            Disconnect();
            StartReconnectLoop();
        }

        public void Disconnect()
        {
            StopReconnectLoop();
            CleanupReceiver();
            UpdateState(NDIReceiverState.Idle);
        }

        private void StartReconnectLoop()
        {
            StopReconnectLoop();
            reconnectCoroutine = StartCoroutine(ReconnectLoop());
        }

        private void StopReconnectLoop()
        {
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }
        }

        private IEnumerator ReconnectLoop()
        {
            UpdateState(NDIReceiverState.Connecting);
            ClearError();
            lastFrameReceivedTime = Time.realtimeSinceStartup;

#if !NDI_SDK_ENABLED
            SetError("NDI SDK is not enabled. Define NDI_SDK_ENABLED to activate receiving.");
            UpdateState(NDIReceiverState.Error);
            yield break;
#else
            var attempts = 0;
            while (attempts < maxReconnectAttempts)
            {
                if (TryConnectReceiver())
                {
                    UpdateState(NDIReceiverState.Connected);
                    StartCoroutine(ReceiveLoop());
                    yield break;
                }

                attempts++;
                UpdateState(NDIReceiverState.Reconnecting);
                yield return new WaitForSeconds(reconnectDelaySeconds);
            }

            SetError("Failed to connect to NDI source after multiple attempts.");
            UpdateState(NDIReceiverState.Error);
#endif
        }

#if NDI_SDK_ENABLED
        private IEnumerator ReceiveLoop()
        {
            while (State == NDIReceiverState.Connected)
            {
                var result = NDIlib.recv_capture_v2(receiverInstance, ref videoFrame, ref audioFrame, ref metadataFrame, 0);
                if (result == NDIlib.frame_type_e.frame_type_video)
                {
                    var latestFrame = videoFrame;
                    DrainToLatestFrame(ref latestFrame);
                    UpdateMetricsFromFrame(latestFrame);
                    UpdateVideoTexture(latestFrame);
                    NDIlib.recv_free_video_v2(receiverInstance, ref latestFrame);
                    lastFrameReceivedTime = Time.realtimeSinceStartup;
                }
                else if (result == NDIlib.frame_type_e.frame_type_audio)
                {
                    NDIlib.recv_free_audio_v2(receiverInstance, ref audioFrame);
                }
                else if (result == NDIlib.frame_type_e.frame_type_metadata)
                {
                    NDIlib.recv_free_metadata(receiverInstance, ref metadataFrame);
                }
                else if (result == NDIlib.frame_type_e.frame_type_none)
                {
                    if (Time.realtimeSinceStartup - lastFrameReceivedTime >= noFrameTimeoutSeconds)
                    {
                        SetError("No NDI frames received.");
                        UpdateState(NDIReceiverState.Reconnecting);
                        StartReconnectLoop();
                        yield break;
                    }
                }

                yield return null;
            }
        }

        private void DrainToLatestFrame(ref NDIlib.video_frame_v2_t latestFrame)
        {
            while (true)
            {
                var result = NDIlib.recv_capture_v2(receiverInstance, ref videoFrame, ref audioFrame, ref metadataFrame, 0);
                if (result == NDIlib.frame_type_e.frame_type_video)
                {
                    NDIlib.recv_free_video_v2(receiverInstance, ref latestFrame);
                    latestFrame = videoFrame;
                    continue;
                }

                if (result == NDIlib.frame_type_e.frame_type_audio)
                {
                    NDIlib.recv_free_audio_v2(receiverInstance, ref audioFrame);
                    continue;
                }

                if (result == NDIlib.frame_type_e.frame_type_metadata)
                {
                    NDIlib.recv_free_metadata(receiverInstance, ref metadataFrame);
                    continue;
                }

                break;
            }
        }
#endif

        private bool TryConnectReceiver()
        {
#if NDI_SDK_ENABLED
            CleanupReceiver();
            ClearError();

            if (!NDIlib.initialize())
            {
                SetError("Failed to initialize NDI SDK.");
                return false;
            }

            receiverConfig = new NDIlib.recv_create_v3_t
            {
                source_to_connect_to = new NDIlib.source_t
                {
                    p_ndi_name = SelectedSource.Name,
                    p_url_address = SelectedSource.Address
                },
                color_format = NDIlib.recv_color_format_e.recv_color_format_fastest,
                bandwidth = NDIlib.recv_bandwidth_e.recv_bandwidth_highest,
                allow_video_fields = false
            };

            receiverInstance = NDIlib.recv_create_v3(ref receiverConfig);
            if (receiverInstance == IntPtr.Zero)
            {
                SetError("Failed to create NDI receiver.");
                return false;
            }

            return true;
#else
            return false;
#endif
        }

        private void CleanupReceiver()
        {
#if NDI_SDK_ENABLED
            if (receiverInstance != IntPtr.Zero)
            {
                NDIlib.recv_destroy(receiverInstance);
                receiverInstance = IntPtr.Zero;
            }
#endif

            if (videoTexture != null)
            {
                Destroy(videoTexture);
                videoTexture = null;
            }

            frameBuffer = null;
        }

#if NDI_SDK_ENABLED
        private void UpdateMetricsFromFrame(NDIlib.video_frame_v2_t frame)
        {
            var latencyTicks = NDIlib.util_clock() - frame.timestamp;
            var metrics = new NDIFrameMetrics
            {
                Width = frame.xres,
                Height = frame.yres,
                FramesPerSecond = frame.frame_rate_N > 0 ? frame.frame_rate_N / (float)frame.frame_rate_D : 0f,
                LatencyMilliseconds = Mathf.Max(0f, latencyTicks / 10000f)
            };

            Metrics = metrics;
            MetricsUpdated?.Invoke(metrics);
        }

        private void UpdateVideoTexture(NDIlib.video_frame_v2_t frame)
        {
            if (frame.xres <= 0 || frame.yres <= 0 || frame.p_data == IntPtr.Zero)
            {
                return;
            }

            var bytesPerPixel = frame.line_stride_in_bytes / frame.xres;
            if (bytesPerPixel != 4)
            {
                return;
            }

            var expectedSize = frame.xres * frame.yres * bytesPerPixel;
            if (frameBuffer == null || frameBuffer.Length != expectedSize)
            {
                frameBuffer = new byte[expectedSize];
            }

            if (frame.line_stride_in_bytes == frame.xres * bytesPerPixel)
            {
                Marshal.Copy(frame.p_data, frameBuffer, 0, frameBuffer.Length);
            }
            else
            {
                for (var row = 0; row < frame.yres; row++)
                {
                    var offset = row * frame.line_stride_in_bytes;
                    Marshal.Copy(IntPtr.Add(frame.p_data, offset), frameBuffer, row * frame.xres * bytesPerPixel, frame.xres * bytesPerPixel);
                }
            }

            if (videoTexture == null || videoTexture.width != frame.xres || videoTexture.height != frame.yres)
            {
                if (videoTexture != null)
                {
                    Destroy(videoTexture);
                }

                videoTexture = new Texture2D(frame.xres, frame.yres, TextureFormat.BGRA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            videoTexture.LoadRawTextureData(frameBuffer);
            videoTexture.Apply(false);
            FrameUpdated?.Invoke(videoTexture);
        }
#endif

        private void UpdateState(NDIReceiverState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }

        private void SetError(string message)
        {
            if (ErrorMessage == message)
            {
                return;
            }

            ErrorMessage = message;
            ErrorChanged?.Invoke(message);
        }

        private void ClearError()
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = string.Empty;
                ErrorChanged?.Invoke(ErrorMessage);
            }
        }
    }
}
