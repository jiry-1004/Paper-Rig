using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PaperRig
{
    // 骨を選んだときのインスペクターとシーン表示。
    // 「関節の位置を直す」をONにすると、絵を動かさずに関節だけドラッグで移動できる
    [CustomEditor(typeof(PaperRigBone))]
    public class PaperRigBoneEditor : UnityEditor.Editor
    {
        static bool editJoint;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            string label = editJoint
                ? "関節の位置を直す：ON（シーンのオレンジの丸をドラッグ）"
                : "関節の位置を直す：OFF";
            bool newValue = GUILayout.Toggle(editJoint, label, "Button", GUILayout.Height(28));
            if (newValue != editJoint)
            {
                editJoint = newValue;
                SceneView.RepaintAll();
            }
        }

        void OnDisable()
        {
            // 骨の選択を外したら、いつもの移動ツールを戻す
            Tools.hidden = false;
        }

        void OnSceneGUI()
        {
            // 関節を直している間は、いつもの移動ツールを隠す（間違えて絵ごと動かさないように）
            Tools.hidden = editJoint;
            if (!editJoint) return;

            var bone = (PaperRigBone)target;
            Transform t = bone.transform;

            Handles.color = new Color(1f, 0.55f, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.FreeMoveHandle(t.position, bone.gizmoSize * 1.5f, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                // 子ども（絵や次の骨）の位置を覚えておく
                var children = new List<Transform>();
                foreach (Transform child in t) children.Add(child);

                Undo.RecordObject(t, "関節を移動");
                foreach (Transform child in children) Undo.RecordObject(child, "関節を移動");

                var positions = new List<Vector3>();
                foreach (Transform child in children) positions.Add(child.position);

                // 関節だけ動かして、子どもは元の位置に戻す
                t.position = new Vector3(newPos.x, newPos.y, t.position.z);
                for (int i = 0; i < children.Count; i++) children[i].position = positions[i];
            }
        }
    }
}