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
