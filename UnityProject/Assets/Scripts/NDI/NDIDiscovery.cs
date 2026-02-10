// NDI SDK for Unity is licensed for non-commercial use only.
// By using this script you agree to the NDI SDK non-commercial license terms.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if NDI_SDK_ENABLED
using NewTek.NDI;
#endif

namespace NDI
{
    public sealed class NDIDiscovery : MonoBehaviour
    {
        [SerializeField] private float pollIntervalSeconds = 1f;
        [SerializeField] private bool autoStart = true;

        private readonly List<NDISourceInfo> sources = new List<NDISourceInfo>();
        private Coroutine pollCoroutine;

        public event Action<IReadOnlyList<NDISourceInfo>> SourcesUpdated;

        public IReadOnlyList<NDISourceInfo> Sources => sources;
        public bool IsServiceAvailable { get; private set; }
        public string LastError { get; private set; }

#if NDI_SDK_ENABLED
        private NDIlib.finder_instance_t finderInstance;
        private bool hasSdkLifetimeAcquire;
#endif

        private void OnEnable()
        {
            if (autoStart)
            {
                StartDiscovery();
            }
        }

        private void OnDisable()
        {
            StopDiscovery();
        }

        public void StartDiscovery()
        {
            if (pollCoroutine != null)
            {
                return;
            }

            InitializeNdi();
            pollCoroutine = StartCoroutine(PollSourcesLoop());
        }

        public void StopDiscovery()
        {
            if (pollCoroutine != null)
            {
                StopCoroutine(pollCoroutine);
                pollCoroutine = null;
            }

            CleanupFinder();
        }

        private IEnumerator PollSourcesLoop()
        {
            while (true)
            {
                var discoveredSources = FetchSources();
                UpdateSources(discoveredSources);
                yield return new WaitForSeconds(pollIntervalSeconds);
            }
        }

        private void UpdateSources(List<NDISourceInfo> discoveredSources)
        {
            if (!HasChanged(discoveredSources))
            {
                return;
            }

            sources.Clear();
            sources.AddRange(discoveredSources);
            SourcesUpdated?.Invoke(sources);
        }

        private bool HasChanged(List<NDISourceInfo> discoveredSources)
        {
            if (sources.Count != discoveredSources.Count)
            {
                return true;
            }

            for (var i = 0; i < sources.Count; i++)
            {
                if (!sources[i].Equals(discoveredSources[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private List<NDISourceInfo> FetchSources()
        {
#if NDI_SDK_ENABLED
            LastError = string.Empty;
            IsServiceAvailable = finderInstance != IntPtr.Zero;

            if (!IsServiceAvailable)
            {
                LastError = "NDI discovery is unavailable. SDK initialization failed.";
                return new List<NDISourceInfo>();
            }

            NDIlib.finder_wait_for_sources(finderInstance, 0);
            var ndiSources = NDIlib.finder_get_current_sources(finderInstance, out var sourceCount);

            var results = new List<NDISourceInfo>(sourceCount);
            for (var i = 0; i < sourceCount; i++)
            {
                var source = ndiSources[i];
                results.Add(new NDISourceInfo(source.p_ndi_name, source.p_url_address));
            }

            return results;
#else
            IsServiceAvailable = false;
            LastError = "NDI SDK is not enabled. Define NDI_SDK_ENABLED to activate discovery.";
            return new List<NDISourceInfo>();
#endif
        }

        private void InitializeNdi()
        {
#if NDI_SDK_ENABLED
            if (!NdiSdkLifetime.TryAcquire())
            {
                LastError = "Failed to initialize NDI SDK global lifetime.";
                Debug.LogError(LastError);
                IsServiceAvailable = false;
                return;
            }

            hasSdkLifetimeAcquire = true;

            finderInstance = NDIlib.finder_create_v2();
            IsServiceAvailable = finderInstance != IntPtr.Zero;
            if (!IsServiceAvailable)
            {
                if (hasSdkLifetimeAcquire)
                {
                    NdiSdkLifetime.Release();
                    hasSdkLifetimeAcquire = false;
                }

                LastError = "Failed to create NDI finder instance.";
            }
#endif
        }

        private void CleanupFinder()
        {
#if NDI_SDK_ENABLED
            if (finderInstance != IntPtr.Zero)
            {
                NDIlib.finder_destroy(finderInstance);
                finderInstance = IntPtr.Zero;
            }

            if (hasSdkLifetimeAcquire)
            {
                NdiSdkLifetime.Release();
                hasSdkLifetimeAcquire = false;
            }
#endif
        }
    }
}
