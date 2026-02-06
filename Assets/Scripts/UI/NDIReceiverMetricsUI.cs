using NDI;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [DisallowMultipleComponent]
    public class NDIReceiverMetricsUI : MonoBehaviour
    {
        [SerializeField] private NDIReceiver receiver;
        [SerializeField] private Text latencyText;
        [SerializeField] private Text resolutionText;
        [SerializeField] private Text fpsText;

        private void OnEnable()
        {
            if (receiver != null)
            {
                receiver.MetricsUpdated += HandleMetricsUpdated;
                HandleMetricsUpdated(receiver.Metrics);
            }
        }

        private void OnDisable()
        {
            if (receiver != null)
            {
                receiver.MetricsUpdated -= HandleMetricsUpdated;
            }
        }

        private void HandleMetricsUpdated(NDIFrameMetrics metrics)
        {
            if (latencyText != null)
            {
                latencyText.text = $"Latency: {metrics.LatencyMilliseconds:0.0} ms";
            }

            if (resolutionText != null)
            {
                resolutionText.text = $"Resolution: {metrics.Width}x{metrics.Height}";
            }

            if (fpsText != null)
            {
                fpsText.text = $"FPS: {metrics.FramesPerSecond:0.0}";
            }
        }
    }
}
