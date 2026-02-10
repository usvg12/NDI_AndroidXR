using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace NDI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AndroidXROriginSetup : MonoBehaviour
    {
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Transform cameraOffset;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private bool allowEditModeAutoSetup = false;

        private void Reset()
        {
            if (xrOrigin == null)
            {
                xrOrigin = GetComponent<XROrigin>();
            }

            if (cameraOffset == null)
            {
                var offsetTransform = transform.Find("Camera Offset");
                if (offsetTransform != null)
                {
                    cameraOffset = offsetTransform;
                }
            }

            if (xrCamera == null)
            {
                xrCamera = GetComponentInChildren<Camera>(true);
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying || allowEditModeAutoSetup)
            {
                EnsureSetup();
            }
        }

        [ContextMenu("Apply Setup Now")]
        private void ApplySetupNow()
        {
            EnsureSetup(forceInEditMode: true);
        }

        private void EnsureSetup(bool forceInEditMode = false)
        {
            if (!Application.isPlaying && !allowEditModeAutoSetup && !forceInEditMode)
            {
                return;
            }
            if (xrOrigin == null)
            {
                xrOrigin = GetComponent<XROrigin>();
                if (xrOrigin == null)
                {
                    xrOrigin = gameObject.AddComponent<XROrigin>();
                }
            }

            if (cameraOffset == null)
            {
                var offsetTransform = transform.Find("Camera Offset");
                if (offsetTransform != null)
                {
                    cameraOffset = offsetTransform;
                }
            }

            if (xrCamera == null)
            {
                xrCamera = GetComponentInChildren<Camera>(true);
            }

            if (xrOrigin != null)
            {
                if (xrCamera != null)
                {
                    xrOrigin.Camera = xrCamera;
                }

                if (cameraOffset != null)
                {
                    xrOrigin.CameraFloorOffsetObject = cameraOffset.gameObject;
                }

                xrOrigin.RequestedTrackingOriginMode = TrackingOriginModeFlags.Floor;
            }
        }
    }
}
