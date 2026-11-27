using UnityEngine;

namespace IGIRebuilt
{
    [RequireComponent(typeof(Camera))]
    public sealed class WeaponScopeEffect : MonoBehaviour
    {
        public WeaponController owner;
        private Material material;
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var definition = owner != null ? owner.Active : null;
            if (owner == null || !owner.IsScoped || definition.scopeShader == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            if (material == null || material.shader != definition.scopeShader)
            {
                if (material != null) Destroy(material);
                material = new Material(definition.scopeShader);
            }
            material.SetTexture("_Mask", definition.scopeMask);
            material.SetTexture("_Horizontal", definition.scopeHorizontal);
            material.SetTexture("_Vertical", definition.scopeVertical);
            material.SetVector("_PixelSize", new Vector4(1f / source.width, 1f / source.height, 0, 0));
            Graphics.Blit(source, destination, material);
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
