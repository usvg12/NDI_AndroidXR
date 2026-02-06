using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRBaseInteractable))]
    public class ResizeHandle : MonoBehaviour
    {
        [SerializeField] private WindowManipulator windowManipulator;

        private XRBaseInteractable interactable;

        private void Reset()
        {
            windowManipulator = GetComponentInParent<WindowManipulator>();
        }

        private void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            if (windowManipulator == null)
            {
                windowManipulator = GetComponentInParent<WindowManipulator>();
            }
        }

        private void OnEnable()
        {
            Register(windowManipulator);
        }

        private void OnDisable()
        {
            Unregister(windowManipulator);
        }

        public void Register(WindowManipulator manipulator)
        {
            if (interactable == null || manipulator == null)
            {
                return;
            }

            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
        }

        public void Unregister(WindowManipulator manipulator)
        {
            if (interactable == null)
            {
                return;
            }

            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (windowManipulator == null)
            {
                return;
            }

            windowManipulator.BeginResize(this, args.interactorObject);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (windowManipulator == null)
            {
                return;
            }

            windowManipulator.EndResize(this, args.interactorObject);
        }
    }
}
