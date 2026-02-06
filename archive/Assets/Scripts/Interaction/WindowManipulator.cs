using System;
using System.Collections.Generic;
using Rendering;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.OpenXR.Input;

namespace Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class WindowManipulator : MonoBehaviour
    {
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        [Header("Targets")]
        [SerializeField] private Transform targetTransform;
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private SBSVideoRenderer sbsVideoRenderer;
        [SerializeField] private List<Renderer> sbsRenderers = new List<Renderer>();
        [SerializeField] private List<ResizeHandle> resizeHandles = new List<ResizeHandle>();

        [Header("Sizing")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(3840f, 1080f);
        [SerializeField] private float minScale = 0.25f;
        [SerializeField] private float maxScale = 2.5f;

        [Header("OpenXR Interaction Profiles")]
        [SerializeField]
        private List<string> allowedInteractionProfiles = new List<string>
        {
            "\/interaction_profiles\/khr\/simple_controller",
            "\/interaction_profiles\/oculus\/touch_controller",
            "\/interaction_profiles\/valve\/index_controller",
            "\/interaction_profiles\/microsoft\/motion_controller",
            "\/interaction_profiles\/hp\/mixed_reality_controller"
        };

        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseScale;
        private float aspectRatio;
        private bool isResizing;
        private float resizeStartHalfWidth;
        private IXRSelectInteractor resizeInteractor;

        private void Reset()
        {
            targetTransform = transform;
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        private void Awake()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            CacheBaseScale();
            ApplySbsMaterialOverrides();
        }

        private void OnEnable()
        {
            foreach (var handle in resizeHandles)
            {
                if (handle != null)
                {
                    handle.Register(this);
                }
            }
        }

        private void OnDisable()
        {
            foreach (var handle in resizeHandles)
            {
                if (handle != null)
                {
                    handle.Unregister(this);
                }
            }
        }

        private void Update()
        {
            if (!isResizing || resizeInteractor == null)
            {
                return;
            }

            var currentHalfWidth = GetInteractorHalfWidth(resizeInteractor);
            if (currentHalfWidth <= Mathf.Epsilon || resizeStartHalfWidth <= Mathf.Epsilon)
            {
                return;
            }

            var scaleFactor = currentHalfWidth / resizeStartHalfWidth;
            var targetWidth = Mathf.Clamp(baseScale.x * scaleFactor, minScale, maxScale);
            var clampedFactor = targetWidth / baseScale.x;

            var newScale = baseScale * clampedFactor;
            targetTransform.localScale = newScale;

            ApplySbsMaterialOverrides();
        }

        public void BeginResize(ResizeHandle handle, IXRSelectInteractor interactor)
        {
            if (isResizing || !IsInteractorAllowed(interactor))
            {
                return;
            }

            resizeInteractor = interactor;
            resizeStartHalfWidth = GetInteractorHalfWidth(interactor);
            isResizing = resizeStartHalfWidth > Mathf.Epsilon;

            if (grabInteractable != null)
            {
                grabInteractable.enabled = false;
            }
        }

        public void EndResize(ResizeHandle handle, IXRSelectInteractor interactor)
        {
            if (!isResizing || interactor != resizeInteractor)
            {
                return;
            }

            isResizing = false;
            resizeInteractor = null;

            if (grabInteractable != null)
            {
                grabInteractable.enabled = true;
            }
        }

        public void CacheBaseScale()
        {
            aspectRatio = referenceResolution.x / Mathf.Max(referenceResolution.y, Mathf.Epsilon);

            baseScale = targetTransform.localScale;
            var expectedHeight = baseScale.x / aspectRatio;
            if (!Mathf.Approximately(baseScale.y, expectedHeight))
            {
                baseScale.y = expectedHeight;
            }

            targetTransform.localScale = baseScale;
        }

        private float GetInteractorHalfWidth(IXRSelectInteractor interactor)
        {
            var interactorTransform = interactor != null ? interactor.transform : null;
            if (interactorTransform == null)
            {
                return 0f;
            }

            var localPosition = targetTransform.InverseTransformPoint(interactorTransform.position);
            var halfWidth = Mathf.Abs(localPosition.x);
            var halfHeight = Mathf.Abs(localPosition.y);

            var desiredHalfWidthFromHeight = halfHeight * aspectRatio;
            return Mathf.Max(halfWidth, desiredHalfWidthFromHeight);
        }

        private bool IsInteractorAllowed(IXRSelectInteractor interactor)
        {
            if (allowedInteractionProfiles == null || allowedInteractionProfiles.Count == 0)
            {
                return true;
            }

            if (TryGetInteractionProfile(interactor, out var profile))
            {
                return allowedInteractionProfiles.Contains(profile);
            }

            return false;
        }

        private static bool TryGetInteractionProfile(IXRSelectInteractor interactor, out string profile)
        {
            profile = string.Empty;
            if (interactor == null)
            {
                return false;
            }

            if (interactor is XRBaseControllerInteractor controllerInteractor)
            {
                var device = controllerInteractor.xrController.inputDevice;
                if (!device.isValid)
                {
                    return false;
                }

                return OpenXRInput.TryGetInteractionProfile(device, out profile);
            }

            return false;
        }

        private void ApplySbsMaterialOverrides()
        {
            if (sbsVideoRenderer != null)
            {
                sbsVideoRenderer.ApplyProperties(force: true);
            }

            if (sbsRenderers == null || sbsRenderers.Count == 0)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            foreach (var rendererTarget in sbsRenderers)
            {
                if (rendererTarget == null)
                {
                    continue;
                }

                rendererTarget.GetPropertyBlock(propertyBlock);
                propertyBlock.SetVector(MainTexStId, new Vector4(1f, 1f, 0f, 0f));
                rendererTarget.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
