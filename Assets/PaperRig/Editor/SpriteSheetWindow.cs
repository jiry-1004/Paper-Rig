using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PaperRig
{
    // メニュー「Tools > PaperRig > 4. コマに書き出す」で開くウィンドウ
    public class SpriteSheetWindow : EditorWindow
    {
        int clipIndex;
        int frameCount = 8;
        int cellSize = 256;
        string log = "";

        [MenuItem("Tools/PaperRig/4. コマに書き出す")]
        static void Open()
        {
            GetWindow<SpriteSheetWindow>("コマ書き出し");
        }

        void OnSelectionChange()
        {
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("モーションをコマ（PNG）に書き出す", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "作ったモーションを、コマを並べた1枚の画像（スプライトシート）にします。\n" +
                "Unity以外のゲームエンジンや、コマ送りで動かすゲームでも使えます。",
                MessageType.Info);

            GameObject rig = MotionMaker.SelectedRig(out string status);
            EditorGUILayout.LabelField("選択中", status);

            Animator animator = rig != null ? rig.GetComponent<Animator>() : null;
            AnimationClip[] clips = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.Distinct().ToArray()
                : new AnimationClip[0];
            if (clips.Length == 0)
            {
                EditorGUILayout.HelpBox("先に「2. モーションを作る」でモーションを作ってください。", MessageType.Warning);
                return;
            }

            clipIndex = Mathf.Clamp(clipIndex, 0, clips.Length - 1);
            clipIndex = EditorGUILayout.Popup("モーション", clipIndex, clips.Select(c => c.name).ToArray());
            frameCount = EditorGUILayout.IntSlider("コマ数", frameCount, 2, 32);
            cellSize = EditorGUILayout.IntPopup("1コマの大きさ", cellSize,
                new[] { "128", "256", "512" }, new[] { 128, 256, 512 });

            if (GUILayout.Button("PNGに書き出す", GUILayout.Height(32)))
            {
                try
                {
                    string path = SpriteSheetExporter.Export(rig, clips[clipIndex], frameCount, cellSize);
                    log = "保存先: " + path;
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                }
                catch (System.Exception e)
                {
                    log = "エラー: " + e.Message;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(log == "" ? "ここに結果が出ます" : log, MessageType.None);
        }
    }

    // モーションを1コマずつ写真に撮って、1枚の画像に並べる
    public static class SpriteSheetExporter
    {
        const int CaptureLayer = 31;   // 書き出しのあいだだけ使うレイヤー（キャラだけを写すため）

        public static string Export(GameObject rig, AnimationClip clip, int frameCount, int cellSize)
        {
            bool loop = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
            SpriteRenderer[] renderers = rig.GetComponentsInChildren<SpriteRenderer>();
            var oldLayers = new Dictionary<GameObject, int>();
            GameObject cameraObject = null;
            RenderTexture renderTexture = null;
            Texture2D sheet = null;

            AnimationMode.StartAnimationMode();   // 終わったら自動で元のポーズに戻るモード
            try
            {
                // 1. 全コマを通して、キャラがはみ出さない範囲を調べる
                Bounds area = new Bounds();
                bool first = true;
                for (int i = 0; i < frameCount; i++)
                {
                    Sample(rig, clip, FrameTime(clip, i, frameCount, loop));
                    foreach (SpriteRenderer sr in renderers)
                    {
                        if (first) { area = sr.bounds; first = false; }
                        else area.Encapsulate(sr.bounds);
                    }
                }

                // 2. キャラだけを写す、書き出し用のカメラを作る
                foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
                {
                    oldLayers[t.gameObject] = t.gameObject.layer;
                    t.gameObject.layer = CaptureLayer;
                }
                cameraObject = new GameObject("PaperRigCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
                Camera cam = cameraObject.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(area.extents.x, area.extents.y) * 1.1f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // 背景は透明
                cam.cullingMask = 1 << CaptureLayer;
                cam.transform.position = new Vector3(area.center.x, area.center.y, area.center.z - 10f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                renderTexture = new RenderTexture(cellSize, cellSize, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = renderTexture;
                cam.aspect = 1f;

                // 3. 1コマずつ写して、1枚の画像に並べる（左上から右へ）
                int cols = Mathf.CeilToInt(Mathf.Sqrt(frameCount));
                int rows = Mathf.CeilToInt(frameCount / (float)cols);
                sheet = new Texture2D(cols * cellSize, rows * cellSize, TextureFormat.RGBA32, false);
                sheet.SetPixels32(new Color32[sheet.width * sheet.height]);   // 全部透明にしておく

                for (int i = 0; i < frameCount; i++)
                {
                    Sample(rig, clip, FrameTime(clip, i, frameCount, loop));
                    Render(cam, renderTexture);

                    int col = i % cols;
                    int row = i / cols;
                    RenderTexture.active = renderTexture;
                    sheet.ReadPixels(new Rect(0, 0, cellSize, cellSize), col * cellSize, (rows - 1 - row) * cellSize);
                    RenderTexture.active = null;
                }
                sheet.Apply();

                // 4. PNGとして保存する
                string path = AssetDatabase.GenerateUniqueAssetPath($"{MotionMaker.EnsureOutputFolder()}/{clip.name}_{frameCount}f.png");
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                return path;
            }
            finally
            {
                // 後片付け：ポーズ・レイヤーを元に戻し、カメラなどを消す
                AnimationMode.StopAnimationMode();
                foreach (var pair in oldLayers)
                {
                    if (pair.Key != null) pair.Key.layer = pair.Value;
                }
                RenderTexture.active = null;
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                if (renderTexture != null) { renderTexture.Release(); Object.DestroyImmediate(renderTexture); }
                if (sheet != null) Object.DestroyImmediate(sheet);
            }
        }

        // i 番目のコマが、モーションの何秒目か
        static float FrameTime(AnimationClip clip, int i, int frameCount, bool loop)
        {
            // くり返す動きは最後のコマが最初と同じにならないように、1周を frameCount 等分する
            return loop
                ? clip.length * i / frameCount
                : clip.length * i / Mathf.Max(1, frameCount - 1);
        }

        // モーションのある時間のポーズにする
        static void Sample(GameObject rig, AnimationClip clip, float time)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(rig, clip, time);
            AnimationMode.EndSampling();
        }

        // カメラで1枚撮る（URP では専用の方法で撮る）
        static void Render(Camera cam, RenderTexture target)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest();
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                request.destination = target;
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                cam.Render();
            }
        }
    }
}