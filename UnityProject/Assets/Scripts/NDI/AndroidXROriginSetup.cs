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

        private void Reset()
        {
            EnsureSetup();
        }

        private void OnEnable()
        {
            EnsureSetup();
        }

        private void EnsureSetup()
        {
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
