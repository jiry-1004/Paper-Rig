using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PaperRig
{
    // メニュー「Tools > PaperRig > 1. リグを作る」で開くウィンドウ
    public class PaperRigWindow : EditorWindow
    {
        bool addIdle = true;
        bool savePrefab = true;
        string log = "";
        Vector2 scroll;

        [MenuItem("Tools/PaperRig/1. リグを作る")]
        static void Open()
        {
            GetWindow<PaperRigWindow>("PaperRig");
        }

        // 選択が変わったら表示を更新する
        void OnSelectionChange()
        {
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("2Dキャラ自動リグ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. PSDから作ったキャラをシーンに置く\n" +
                "2. Hierarchy でそのキャラ（いちばん上の親）を選ぶ\n" +
                "3. 「リグを作る」を押す",
                MessageType.Info);

            GameObject target = Selection.activeGameObject;
            bool inScene = target != null && target.scene.IsValid();
            bool alreadyRigged = inScene && target.GetComponentInChildren<PaperRigBone>(true) != null;
            bool ok = inScene && !alreadyRigged;

            string status = !inScene ? "（シーンのキャラを選んでください）"
                          : alreadyRigged ? target.name + "（すでにリグ済みです）"
                          : target.name;
            EditorGUILayout.LabelField("選択中", status);

            addIdle = EditorGUILayout.Toggle("待機モーションをつける", addIdle);
            savePrefab = EditorGUILayout.Toggle("プレハブとして保存", savePrefab);

            using (new EditorGUI.DisabledScope(!ok))
            {
                if (GUILayout.Button("① パーツ判別を確認"))
                {
                    log = PaperRigBuilder.Describe(PaperRigBuilder.Analyze(target));
                }
                if (GUILayout.Button("② リグを作る", GUILayout.Height(32)))
                {
                    MakeRig(target, null);
                }
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(log == "" ? "ここに結果が出ます" : log, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void MakeRig(GameObject target, List<PartType> aiParts)
        {
            GameObject rig = PaperRigBuilder.Build(target, addIdle, aiParts, out string report);
            log = report;
            if (rig != null && savePrefab)
            {
                log += "\n保存先: " + PaperRigBuilder.SaveAsPrefab(rig);
            }
        }
    }
}