using UnityEngine;

namespace Rendering
{
    [DisallowMultipleComponent]
    public class SBSVideoRenderer : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string textureProperty = "_MainTex";
        [SerializeField] private string sbsModeProperty = "_SBSMode";
        [SerializeField] private string eyeSwapProperty = "_SwapEyes";
        [SerializeField] private bool sbsEnabled;
        [SerializeField] private bool swapEyes;
        [SerializeField] private Texture sourceTexture;

        private MaterialPropertyBlock propertyBlock;
        private bool lastSbsEnabled;
        private bool lastSwapEyes;
        private Texture lastTexture;

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        private void Awake()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        private void OnEnable()
        {
            ApplyProperties(force: true);
        }

        public void SetSbsEnabled(bool enabled)
        {
            if (sbsEnabled == enabled)
            {
                return;
            }

            sbsEnabled = enabled;
            ApplyProperties(force: false);
        }

        public void SetTexture(Texture texture)
        {
            if (sourceTexture == texture)
            {
                return;
            }

            sourceTexture = texture;
            ApplyProperties(force: false);
        }

        public void SetEyeSwap(bool enabled)
        {
            if (swapEyes == enabled)
            {
                return;
            }

            swapEyes = enabled;
            ApplyProperties(force: false);
        }

        public void ApplyProperties(bool force)
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (!force && lastSbsEnabled == sbsEnabled && lastSwapEyes == swapEyes && lastTexture == sourceTexture)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(sbsModeProperty, sbsEnabled ? 1f : 0f);
            propertyBlock.SetFloat(eyeSwapProperty, swapEyes ? 1f : 0f);
            if (sourceTexture != null)
            {
                propertyBlock.SetTexture(textureProperty, sourceTexture);
            }
            targetRenderer.SetPropertyBlock(propertyBlock);

            lastSbsEnabled = sbsEnabled;
            lastSwapEyes = swapEyes;
            lastTexture = sourceTexture;
        }
    }
}
