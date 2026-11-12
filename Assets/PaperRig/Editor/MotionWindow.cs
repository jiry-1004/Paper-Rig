using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PaperRig
{
    // メニュー「Tools > PaperRig > 2. モーションを作る」で開くウィンドウ
    public class MotionWindow : EditorWindow
    {
        bool[] use;
        string log = "";
        Vector2 scroll;

        [MenuItem("Tools/PaperRig/2. モーションを作る")]
        static void Open()
        {
            GetWindow<MotionWindow>("モーション");
        }

        void OnSelectionChange()
        {
            Repaint();
        }

        void OnGUI()
        {
            MotionData[] presets = MotionPresets.All;
            if (use == null || use.Length != presets.Length)
            {
                use = new bool[presets.Length];
                for (int i = 0; i < use.Length; i++) use[i] = true;
            }

            EditorGUILayout.LabelField("モーションを自動で作る", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "リグ済みのキャラ（○○_Rig）を選び、作りたい動きにチェックを入れて押してください。\n" +
                "再生ボタン（▶）を押すと、画面左上のボタンで動きを切り替えられます。",
                MessageType.Info);

            GameObject rig = MotionMaker.SelectedRig(out string status);
            EditorGUILayout.LabelField("選択中", status);

            EditorGUILayout.Space();
            for (int i = 0; i < presets.Length; i++)
            {
                use[i] = EditorGUILayout.ToggleLeft($"{MotionPresets.Label(presets[i].name)}（{presets[i].name}）", use[i]);
            }

            using (new EditorGUI.DisabledScope(rig == null))
            {
                if (GUILayout.Button("モーションを作る", GUILayout.Height(32)))
                {
                    var chosen = new List<MotionData>();
                    for (int i = 0; i < presets.Length; i++)
                    {
                        if (use[i]) chosen.Add(presets[i]);
                    }
                    var warnings = new List<string>();
                    log = MotionMaker.AddMotions(rig, chosen, warnings);
                    if (warnings.Count > 0) log += "\n\n注意:\n" + string.Join("\n", warnings);
                }
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(log == "" ? "ここに結果が出ます" : log, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }
}