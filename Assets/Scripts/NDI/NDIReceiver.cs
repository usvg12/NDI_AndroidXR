// NDI SDK for Unity is licensed for non-commercial use only.
// By using this script you agree to the NDI SDK non-commercial license terms.

using System;
using System.Collections;
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

        public event Action<NDIReceiverState> StateChanged;
        public event Action<string> ErrorChanged;
        public event Action<NDIFrameMetrics> MetricsUpdated;
        public event Action<Texture> VideoFrameReady;

        public NDIReceiverState State { get; private set; } = NDIReceiverState.Idle;
        public string ErrorMessage { get; private set; }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public NDIFrameMetrics Metrics { get; private set; }
        public NDISourceInfo SelectedSource { get; private set; }
        public Texture VideoTexture => videoTexture;

        private Coroutine reconnectCoroutine;
        private Texture2D videoTexture;

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
                var result = NDIlib.recv_capture_v2(receiverInstance, ref videoFrame, ref audioFrame, ref metadataFrame, 1000);
                if (result == NDIlib.frame_type_e.frame_type_video)
                {
                    UpdateMetricsFromFrame(videoFrame);
                    UpdateVideoTextureFromFrame(videoFrame);
                    NDIlib.recv_free_video_v2(receiverInstance, ref videoFrame);
                }
                else if (result == NDIlib.frame_type_e.frame_type_none)
                {
                    SetError("No NDI frames received.");
                    UpdateState(NDIReceiverState.Reconnecting);
                    StartReconnectLoop();
                    yield break;
                }

                yield return null;
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
        }

#if NDI_SDK_ENABLED
        private void UpdateMetricsFromFrame(NDIlib.video_frame_v2_t frame)
        {
            var metrics = new NDIFrameMetrics
            {
                Width = frame.xres,
                Height = frame.yres,
                FramesPerSecond = frame.frame_rate_N > 0 ? frame.frame_rate_N / (float)frame.frame_rate_D : 0f,
                LatencyMilliseconds = frame.timestamp / 10000f
            };

            Metrics = metrics;
            MetricsUpdated?.Invoke(metrics);
        }

        private void UpdateVideoTextureFromFrame(NDIlib.video_frame_v2_t frame)
        {
            if (frame.p_data == IntPtr.Zero || frame.xres <= 0 || frame.yres <= 0)
            {
                return;
            }

            if (!TryGetTextureFormat(frame.FourCC, out var textureFormat))
            {
                return;
            }

            if (videoTexture == null
                || videoTexture.width != frame.xres
                || videoTexture.height != frame.yres
                || videoTexture.format != textureFormat)
            {
                if (videoTexture != null)
                {
                    Destroy(videoTexture);
                }

                videoTexture = new Texture2D(frame.xres, frame.yres, textureFormat, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            var dataSize = frame.line_stride_in_bytes * frame.yres;
            videoTexture.LoadRawTextureData(frame.p_data, dataSize);
            videoTexture.Apply(false, false);
            VideoFrameReady?.Invoke(videoTexture);
        }

        private static bool TryGetTextureFormat(NDIlib.FourCC_type_e fourCC, out TextureFormat textureFormat)
        {
            switch (fourCC)
            {
                case NDIlib.FourCC_type_e.FourCC_type_BGRA:
                    textureFormat = TextureFormat.BGRA32;
                    return true;
                case NDIlib.FourCC_type_e.FourCC_type_RGBA:
                    textureFormat = TextureFormat.RGBA32;
                    return true;
                default:
                    textureFormat = TextureFormat.RGBA32;
                    return false;
            }
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
