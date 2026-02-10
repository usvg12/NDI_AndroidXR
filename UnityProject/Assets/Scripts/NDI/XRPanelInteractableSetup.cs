using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace NDI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class XRPanelInteractableSetup : MonoBehaviour
    {
        [SerializeField] private bool ensureCollider = true;
        [SerializeField] private Vector3 colliderSize = new Vector3(1f, 1f, 0.02f);
        [SerializeField] private bool allowEditModeAutoSetup = false;

        private void Reset()
        {
            var boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                colliderSize = boxCollider.size;
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
            if (ensureCollider && GetComponent<Collider>() == null)
            {
                var boxCollider = gameObject.AddComponent<BoxCollider>();
                boxCollider.size = colliderSize;
            }

            var grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable == null)
            {
                grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
            }

            grabInteractable.trackPosition = true;
            grabInteractable.trackRotation = true;
            grabInteractable.movementType = XRGrabInteractable.MovementType.VelocityTracking;

            if (GetComponent<XRSingleGrabFreeTransformer>() == null)
            {
                gameObject.AddComponent<XRSingleGrabFreeTransformer>();
            }

            if (GetComponent<XRDualGrabFreeTransformer>() == null)
            {
                gameObject.AddComponent<XRDualGrabFreeTransformer>();
            }
        }
    }
}
