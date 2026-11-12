using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PaperRig
{
    // MotionData から Unity のアニメーション（AnimationClip）を作り、キャラに取り付ける
    public static class MotionMaker
    {
        const float SampleRate = 30f;   // 1秒を何個に区切って動きを記録するか

        // 選択中のキャラ（リグ済み）を返す。骨やパーツを選んでいても、キャラ本体を探す
        public static GameObject SelectedRig(out string status)
        {
            GameObject go = Selection.activeGameObject;
            if (go == null || !go.scene.IsValid())
            {
                status = "（シーンのキャラを選んでください）";
                return null;
            }

            PaperRigBone bone = go.GetComponentInParent<PaperRigBone>();
            if (bone != null)
            {
                // いちばん根元の骨（Hip）の親がキャラ本体
                Transform t = bone.transform;
                while (t.parent != null && t.parent.GetComponent<PaperRigBone>() != null) t = t.parent;
                if (t.parent != null) go = t.parent.gameObject;
            }

            if (go.GetComponentInChildren<PaperRigBone>(true) == null)
            {
                status = go.name + "（先に「1. リグを作る」で骨を入れてください）";
                return null;
            }
            status = go.name;
            return go;
        }

        // 出力先フォルダ（なければ作る）
        public static string EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/PaperRig")) AssetDatabase.CreateFolder("Assets", "PaperRig");
            if (!AssetDatabase.IsValidFolder("Assets/PaperRig/Output")) AssetDatabase.CreateFolder("Assets/PaperRig", "Output");
            return "Assets/PaperRig/Output";
        }

        // モーションを保存して、キャラの Animator に登録する
        public static string AddMotions(GameObject rig, IEnumerable<MotionData> motions, List<string> warnings)
        {
            string folder = EnsureOutputFolder();

            // モーションをまとめる「アニメーターコントローラー」（キャラごとに1つ）
            string controllerPath = $"{folder}/{rig.name}_Motions.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            // Animator と、再生中に動きを切り替えるボタンをキャラに付ける
            Animator animator = rig.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(rig);
            Undo.RecordObject(animator, "モーション追加");
            animator.runtimeAnimatorController = controller;

            var player = rig.GetComponent<PaperRigMotionPlayer>();
            if (player == null) player = Undo.AddComponent<PaperRigMotionPlayer>(rig);
            Undo.RecordObject(player, "モーション追加");

            // Step 4 の待機モーションは Animator とぶつかるので外す
            var idle = rig.GetComponent<PaperRigIdle>();
            if (idle != null) Undo.DestroyObjectImmediate(idle);

            var made = new List<string>();
            foreach (MotionData data in motions)
            {
                AnimationClip clip = CreateClip(rig, data, warnings);
                string clipPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{rig.name}_{data.name}.anim");
                AssetDatabase.CreateAsset(clip, clipPath);

                // 同じ名前の動きがあれば差し替え、なければ追加する
                ChildAnimatorState existing = stateMachine.states.FirstOrDefault(s => s.state.name == data.name);
                if (existing.state != null)
                {
                    existing.state.motion = clip;
                }
                else
                {
                    AnimatorState state = controller.AddMotion(clip);
                    state.name = data.name;
                }

                if (!player.motions.Contains(data.name)) player.motions.Add(data.name);
                made.Add(data.name);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return "作ったモーション: " + string.Join(", ", made) + "\n保存先: " + folder;
        }

        // MotionData 1つから AnimationClip を作る
        public static AnimationClip CreateClip(GameObject rig, MotionData data, List<string> warnings)
        {
            Transform root = rig.transform;
            if (data.keys == null) data.keys = new MotionKey[0];
            float duration = Mathf.Max(0.1f, data.duration);
            var clip = new AnimationClip { name = data.name, frameRate = SampleRate };

            // 骨を名前で探せるようにする
            var bones = new Dictionary<string, Transform>();
            foreach (PaperRigBone b in rig.GetComponentsInChildren<PaperRigBone>(true))
            {
                if (!bones.ContainsKey(b.name)) bones.Add(b.name, b.transform);
            }

            // --- 骨の回転 ---
            var boneNames = data.keys
                .Where(k => k.bones != null)
                .SelectMany(k => k.bones)
                .Where(b => b != null && !string.IsNullOrEmpty(b.bone))
                .Select(b => b.bone)
                .Distinct();

            foreach (string boneName in boneNames)
            {
                if (!bones.TryGetValue(boneName, out Transform t))
                {
                    warnings.Add($"骨「{boneName}」がこのキャラにないので飛ばしました");
                    continue;
                }

                // その骨の角度の変化を、なめらかなカーブにする
                var angle = new AnimationCurve();
                foreach (MotionKey key in data.keys)
                {
                    BoneAngle ba = key.bones?.FirstOrDefault(b => b != null && b.bone == boneName);
                    if (ba != null) angle.AddKey(Mathf.Clamp01(key.time) * duration, ba.angle);
                }
                AddEnds(angle, duration, data.loop, 0f);

                // Unity は回転を「クォータニオン」で保存するので、細かく区切って変換する
                string path = AnimationUtility.CalculateTransformPath(t, root);
                Quaternion rest = t.localRotation;
                var qx = new AnimationCurve();
                var qy = new AnimationCurve();
                var qz = new AnimationCurve();
                var qw = new AnimationCurve();
                int frames = Mathf.Max(2, Mathf.RoundToInt(duration * SampleRate));
                for (int i = 0; i <= frames; i++)
                {
                    float time = duration * i / frames;
                    Quaternion q = rest * Quaternion.Euler(0f, 0f, angle.Evaluate(time));
                    qx.AddKey(time, q.x);
                    qy.AddKey(time, q.y);
                    qz.AddKey(time, q.z);
                    qw.AddKey(time, q.w);
                }
                clip.SetCurve(path, typeof(Transform), "localRotation.x", qx);
                clip.SetCurve(path, typeof(Transform), "localRotation.y", qy);
                clip.SetCurve(path, typeof(Transform), "localRotation.z", qz);
                clip.SetCurve(path, typeof(Transform), "localRotation.w", qw);
            }

            // --- 腰（キャラ全体）の上下 ---
            if (data.keys.Any(k => Mathf.Abs(k.hipY) > 0.0001f) && bones.TryGetValue("Hip", out Transform hip))
            {
                float height = CharacterHeight(rig);
                Vector3 rest = hip.localPosition;
                var y = new AnimationCurve();
                foreach (MotionKey key in data.keys)
                {
                    y.AddKey(Mathf.Clamp01(key.time) * duration, rest.y + key.hipY * height);
                }
                AddEnds(y, duration, data.loop, rest.y);

                string path = AnimationUtility.CalculateTransformPath(hip, root);
                clip.SetCurve(path, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, duration, rest.x));
                clip.SetCurve(path, typeof(Transform), "localPosition.y", y);
                clip.SetCurve(path, typeof(Transform), "localPosition.z", AnimationCurve.Constant(0f, duration, rest.z));
            }

            clip.EnsureQuaternionContinuity();

            // くり返し再生の設定
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = data.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        // カーブの最初（0秒）と最後にキーがなければ足す
        static void AddEnds(AnimationCurve curve, float duration, bool loop, float restValue)
        {
            if (curve.length == 0 || curve.keys[0].time > 0.0001f)
            {
                curve.AddKey(0f, restValue);
            }
            if (curve.keys[curve.length - 1].time < duration - 0.0001f)
            {
                // くり返す動きは、最後を最初と同じポーズにしてつなぎ目をなくす
                curve.AddKey(duration, loop ? curve.keys[0].value : restValue);
            }
        }

        // キャラの高さ（全パーツを囲んだ高さ）
        static float CharacterHeight(GameObject rig)
        {
            SpriteRenderer[] renderers = rig.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length == 0) return 1f;
            Bounds b = renderers[0].bounds;
            foreach (SpriteRenderer sr in renderers) b.Encapsulate(sr.bounds);
            return b.size.y;
        }
    }
}