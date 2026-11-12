using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PaperRig
{
    public class AiMotionWindow : EditorWindow
    {
        string request = "両手を上げて、ぴょんぴょん跳ねて喜ぶ";
        float seconds = 1.5f;
        bool loop = true;
        string apiKey = "";
        bool busy;
        string log = "";
        Vector2 scroll;

        [MenuItem("Tools/PaperRig/3. AIで動きを作る")]
        static void Open()
        {
            GetWindow<AiMotionWindow>("AIモーション");
        }

        void OnEnable()
        {
            apiKey = ClaudeClient.ApiKey;
        }

        void OnSelectionChange()
        {
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("AIで動きを作る", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("作りたい動きを文章で書くと、AIが関節の角度を考えてモーションを作ります。", MessageType.Info);

            GameObject rig = MotionMaker.SelectedRig(out string status);
            EditorGUILayout.LabelField("選択中", status);

            string newKey = EditorGUILayout.PasswordField("APIキー", apiKey);
            if (newKey != apiKey)
            {
                apiKey = newKey;
                ClaudeClient.ApiKey = apiKey;
            }

            EditorGUILayout.LabelField("どんな動き？");
            request = EditorGUILayout.TextArea(request, GUILayout.Height(60));
            seconds = EditorGUILayout.Slider("長さ（秒）", seconds, 0.5f, 5f);
            loop = EditorGUILayout.Toggle("くり返す", loop);

            bool ready = rig != null && !busy && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrWhiteSpace(request);
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button(busy ? "AIが考え中..." : "AIでモーションを作る", GUILayout.Height(32)))
                {
                    Run(rig);
                }
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(log == "" ? "ここに結果が出ます" : log, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        async void Run(GameObject rig)
        {
            busy = true;
            log = "AIに問い合わせ中...";
            Repaint();
            try
            {
                string text = await ClaudeClient.AskAsync(BuildPrompt(rig));
                MotionData data = JsonUtility.FromJson<MotionData>(ClaudeClient.ExtractJson(text));
                Clean(data, rig);

                var warnings = new List<string>();
                log = MotionMaker.AddMotions(rig, new[] { data }, warnings);
                log += $"\nキーの数: {data.keys.Length}";
                if (warnings.Count > 0) log += "\n\n注意:\n" + string.Join("\n", warnings);
            }
            catch (System.Exception e)
            {
                log = "エラー: " + e.Message;
            }
            finally
            {
                busy = false;
                Repaint();
            }
        }

        string BuildPrompt(GameObject rig)
        {
            var sb = new StringBuilder();
            sb.AppendLine("2Dゲームのキャラクターのアニメーションを作ってください。");
            sb.AppendLine("キャラは絵のパーツを骨で回して動かす「切り絵アニメ」です。正面を向いています。");
            sb.AppendLine();
            sb.AppendLine($"作りたい動き: {request}");
            sb.AppendLine($"長さ: {seconds:F1}秒 / くり返し: {(loop ? "する" : "しない")}");
            sb.AppendLine();
            sb.AppendLine("使える骨（カッコ内は親の骨。子の骨は親が回るとついてくる）:");
            foreach (PaperRigBone bone in rig.GetComponentsInChildren<PaperRigBone>(true))
            {
                PaperRigBone parent = bone.transform.parent != null ? bone.transform.parent.GetComponent<PaperRigBone>() : null;
                sb.AppendLine($"- {bone.name}（親: {(parent != null ? parent.name : "なし")}）");
            }
            sb.AppendLine();
            sb.AppendLine("ルール:");
            sb.AppendLine("- 骨の名前の _L は画面の左側、_R は画面の右側");
            sb.AppendLine("- angle は度。反時計回りがプラス。0 は描いたままのポーズ（腕や脚は下におろした状態）");
            sb.AppendLine("- 左の腕や脚を外側（画面の左）へ上げるとマイナス、右の腕や脚を外側（画面の右）へ上げるとプラス");
            sb.AppendLine("- 子の骨の角度は、親の骨からの相対的な角度");
            sb.AppendLine("- hipY はキャラ全体の上下。キャラの高さに対する割合で、0.1 で1割上、-0.05 で少し沈む");
            sb.AppendLine("- time は 0〜1。0 と 1 のキーを必ず入れる。くり返す動きは 0 と 1 を同じポーズにする");
            sb.AppendLine("- 角度は -150〜150 の範囲で、自然でかわいい動きにする。キーは5〜12個くらい");
            sb.AppendLine("- Hip は回さない（上下は hipY で表す）");
            sb.AppendLine("- name は、作りたい動きの内容を表す短い英単語にする（例: Run, Bow, Dance, Sad）。下の例の名前はまねしない");
            sb.AppendLine();
            sb.AppendLine("返事は次の形のJSONだけにしてください。");
            sb.AppendLine("{\"name\":\"MotionName\",\"duration\":1.5,\"loop\":true,\"keys\":[{\"time\":0,\"hipY\":0,\"bones\":[{\"bone\":\"UpperArm_L\",\"angle\":0}]}]}");
            return sb.ToString();
        }

        void Clean(MotionData data, GameObject rig)
        {
            if (data == null || data.keys == null || data.keys.Length == 0)
            {
                throw new System.Exception("AIの返事にポーズ（keys）が入っていませんでした。もう一度試してください。");
            }

            string safe = new string((data.name ?? "").Where(c => c < 128 && char.IsLetterOrDigit(c)).ToArray());
            if (safe.Length == 0 || safe == "MotionName") safe = "Motion";
            string name = "AI_" + safe;

            var player = rig.GetComponent<PaperRigMotionPlayer>();
            string baseName = name;
            int number = 2;
            while (player != null && player.motions.Contains(name))
            {
                name = baseName + number;
                number++;
            }
            data.name = name;

            data.duration = seconds;
            data.loop = loop;

            data.keys = data.keys.Where(k => k != null).OrderBy(k => k.time).ToArray();
            foreach (MotionKey key in data.keys)
            {
                key.time = Mathf.Clamp01(key.time);
                key.hipY = Mathf.Clamp(key.hipY, -0.3f, 0.5f);
                if (key.bones == null) key.bones = new BoneAngle[0];
                foreach (BoneAngle b in key.bones)
                {
                    if (b != null) b.angle = Mathf.Clamp(b.angle, -170f, 170f);
                }
            }
        }
    }
}