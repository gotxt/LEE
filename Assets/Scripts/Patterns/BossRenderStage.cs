using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace NHN.TraceStrike.Patterns
{
    // A transparent sprite/particle pass composited into the existing UI battlefield.
    public sealed class BossRenderStage : IDisposable
    {
        public readonly BossPresentation Presentation;
        public readonly RenderTexture Texture;
        private readonly GameObject root;
        private readonly Camera camera;
        private bool disposed;
        private static int index;
#if UNITY_EDITOR
        private Scene previewScene;
#endif
        public BossRenderStage(BossVisualDefinition definition, int gridSize, bool preview)
        {
            root = new GameObject("Boss render stage") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.position = new Vector3(10000 + (++index % 1000) * 128, 10000, 0);
            try
            {
#if UNITY_EDITOR
                if (preview)
                { previewScene = EditorSceneManager.NewPreviewScene(); SceneManager.MoveGameObjectToScene(root, previewScene); }
#endif
                var view = new GameObject("Boss camera"); view.transform.SetParent(root.transform, false);
                camera = view.AddComponent<Camera>();
#if UNITY_EDITOR
                if (preview)
                {
                    camera.scene = previewScene;
                    camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(previewScene);
                }
#endif
                camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = gridSize * 0.5f;
                camera.transform.localPosition = new Vector3((gridSize - 1) * 0.5f, (gridSize - 1) * 0.5f, -30);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 31; camera.nearClipPlane = 0.1f; camera.farClipPlane = 60;
                camera.allowHDR = false; camera.allowMSAA = false;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.renderShadows = false;
                int pixels = Mathf.Clamp(Mathf.NextPowerOfTwo(gridSize * 48), 512, 2048);
                Texture = new RenderTexture(pixels, pixels, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
                Texture.Create(); camera.targetTexture = Texture;
                Presentation = new BossPresentation(definition, root.transform, preview);
            }
            catch { Dispose(); throw; }
        }
        public void Render()
        {
            if (disposed) return;
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = Texture });
            else camera.Render();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Presentation?.Dispose();
            if (camera != null) camera.targetTexture = null;
            if (Texture != null) { Texture.Release(); BossPresentation.Destroy(Texture); }
            BossPresentation.Destroy(root);
#if UNITY_EDITOR
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
#endif
        }
    }
}
