using NDI;
using UnityEngine;

namespace Rendering
{
    [DisallowMultipleComponent]
    public class NDIReceiverVideoRenderer : MonoBehaviour
    {
        [SerializeField] private NDIReceiver receiver;
        [SerializeField] private SBSVideoRenderer sbsRenderer;

        private void OnEnable()
        {
            if (receiver != null)
            {
                receiver.VideoFrameReady += HandleFrameUpdated;
                if (receiver.VideoTexture != null)
                {
                    HandleFrameUpdated(receiver.VideoTexture);
                }
            }
        }

        private void OnDisable()
        {
            if (receiver != null)
            {
                receiver.VideoFrameReady -= HandleFrameUpdated;
            }
        }

        private void HandleFrameUpdated(Texture texture)
        {
            if (sbsRenderer == null)
            {
                return;
            }

            sbsRenderer.SetTexture(texture);
        }
    }
}
