// NDI SDK for Unity is licensed for non-commercial use only.
// By using this script you agree to the NDI SDK non-commercial license terms.

using System.Collections.Generic;
using NDI;
using UnityEngine;
using UnityEngine.UI;

namespace NDIUI
{
    public sealed class NDIUIController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private NDIDiscovery discovery;
        [SerializeField] private NDIReceiver receiver;

        [Header("UI")]
        [SerializeField] private Dropdown sourcesDropdown;
        [SerializeField] private Button connectButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text errorText;
        [SerializeField] private Text metricsText;

        private readonly List<NDISourceInfo> sources = new List<NDISourceInfo>();
        private int selectedIndex;

        private void OnEnable()
        {
            if (discovery != null)
            {
                discovery.SourcesUpdated += HandleSourcesUpdated;
                HandleSourcesUpdated(discovery.Sources);
            }

            if (receiver != null)
            {
                receiver.StateChanged += HandleStateChanged;
                receiver.ErrorChanged += HandleErrorChanged;
                receiver.MetricsUpdated += HandleMetricsUpdated;
                HandleStateChanged(receiver.State);
                HandleErrorChanged(receiver.ErrorMessage);
            }

            if (sourcesDropdown != null)
            {
                sourcesDropdown.onValueChanged.AddListener(HandleSourceSelectionChanged);
            }

            if (connectButton != null)
            {
                connectButton.onClick.AddListener(HandleConnectClicked);
            }
        }

        private void OnDisable()
        {
            if (discovery != null)
            {
                discovery.SourcesUpdated -= HandleSourcesUpdated;
            }

            if (receiver != null)
            {
                receiver.StateChanged -= HandleStateChanged;
                receiver.ErrorChanged -= HandleErrorChanged;
                receiver.MetricsUpdated -= HandleMetricsUpdated;
                receiver.Disconnect();
            }

            if (sourcesDropdown != null)
            {
                sourcesDropdown.onValueChanged.RemoveListener(HandleSourceSelectionChanged);
            }

            if (connectButton != null)
            {
                connectButton.onClick.RemoveListener(HandleConnectClicked);
            }
        }

        private void OnDestroy()
        {
            if (receiver != null)
            {
                receiver.Disconnect();
            }
        }

        private void OnApplicationQuit()
        {
            if (receiver != null)
            {
                receiver.Disconnect();
            }
        }

        private void HandleSourcesUpdated(IReadOnlyList<NDISourceInfo> updatedSources)
        {
            sources.Clear();
            if (updatedSources != null)
            {
                sources.AddRange(updatedSources);
            }

            var hasSources = sources.Count > 0;
            if (sourcesDropdown != null)
            {
                sourcesDropdown.ClearOptions();
                var options = new List<string>();
                foreach (var source in sources)
                {
                    options.Add(source.ToString());
                }

                sourcesDropdown.AddOptions(options);
                sourcesDropdown.interactable = hasSources;

                if (!hasSources)
                {
                    selectedIndex = 0;
                }
                else
                {
                    selectedIndex = Mathf.Clamp(selectedIndex, 0, sources.Count - 1);
                    sourcesDropdown.SetValueWithoutNotify(selectedIndex);
                }
            }

            if (connectButton != null)
            {
                connectButton.interactable = hasSources;
            }

            if (!hasSources)
            {
                UpdateErrorMessage("No sources found.");
            }
            else
            {
                UpdateErrorMessage(string.Empty);
            }

            if (!hasSources && receiver != null)
            {
                receiver.Disconnect();
            }
        }

        private void HandleSourceSelectionChanged(int index)
        {
            selectedIndex = index;
            if (receiver != null)
            {
                receiver.Disconnect();
            }

            UpdateStatusText();
        }

        private void HandleConnectClicked()
        {
            if (receiver == null)
            {
                UpdateErrorMessage("NDI Receiver is not assigned.");
                return;
            }

            if (sources.Count == 0)
            {
                UpdateErrorMessage("No sources found.");
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, sources.Count - 1);
            receiver.Connect(sources[selectedIndex]);
            UpdateStatusText();
        }

        private void HandleStateChanged(NDIReceiverState state)
        {
            UpdateStatusText();
            if (state == NDIReceiverState.Error)
            {
                UpdateErrorMessage(receiver != null ? receiver.ErrorMessage : "Connection failed.");
            }
        }

        private void HandleErrorChanged(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                UpdateErrorMessage(string.Empty);
                return;
            }

            UpdateErrorMessage(message);
        }

        private void HandleMetricsUpdated(NDIFrameMetrics metrics)
        {
            if (metricsText == null)
            {
                return;
            }

            metricsText.text = $"Resolution: {metrics.Width}x{metrics.Height}\n" +
                               $"FPS: {metrics.FramesPerSecond:0.##}\n" +
                               $"Latency: {metrics.LatencyMilliseconds:0.##} ms";
        }

        private void UpdateStatusText()
        {
            if (statusText == null)
            {
                return;
            }

            var stateText = receiver != null ? receiver.State.ToString() : "Receiver Missing";
            var sourceText = sources.Count > 0 && selectedIndex >= 0 && selectedIndex < sources.Count
                ? sources[selectedIndex].Name
                : "None";
            statusText.text = $"State: {stateText}\nSource: {sourceText}";
        }

        private void UpdateErrorMessage(string message)
        {
            if (errorText == null)
            {
                return;
            }

            errorText.text = message;
        }
    }
}
