using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PaperRig
{
    // キャラの絵を調べて骨を作り、動かせる状態にする本体
    public static class PaperRigBuilder
    {
        // 1枚のレイヤー（絵のパーツ）の情報
        public class Layer
        {
            public SpriteRenderer renderer;
            public PartType part;
            public Side side;
            public Bounds bounds;
            public bool guessed;   // 名前ではなく位置から推定したか
        }

        // ================================================================
        // ① 絵を調べる：パーツの種類と左右を決める
        //    aiParts を渡すと、レイヤー名の代わりにその判定を使う（将来AIでパーツを判別するとき用）
        // ================================================================
        public static List<Layer> Analyze(GameObject character, List<PartType> aiParts = null)
        {
            var layers = new List<Layer>();
            foreach (SpriteRenderer sr in character.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr.sprite == null) continue;
                if (sr.gameObject == character) continue;   // 親そのものは対象外
                layers.Add(new Layer
                {
                    renderer = sr,
                    part = PartClassifier.Classify(sr.gameObject.name),
                    bounds = sr.bounds
                });
            }

            if (aiParts != null && aiParts.Count == layers.Count)
            {
                for (int i = 0; i < layers.Count; i++) layers[i].part = aiParts[i];
            }

            // わからないパーツは、そのパーツの中心を含む「一番小さいパーツ」の仲間にする
            // （例：頭の上のリボン → 頭）。どれにも入らなければ体の仲間にする
            List<Layer> known = layers.Where(l => l.part != PartType.Unknown).ToList();
            foreach (Layer layer in layers)
            {
                if (layer.part != PartType.Unknown) continue;
                Layer best = null;
                foreach (Layer other in known)
                {
                    if (!Contains2D(other.bounds, layer.bounds.center)) continue;
                    if (best == null || Area(other.bounds) < Area(best.bounds)) best = other;
                }
                layer.part = best != null ? best.part : PartType.Body;
                layer.guessed = true;
            }

            // 左右を決める（体の中心より左か右か）
            Bounds all = UnionOf(layers) ?? new Bounds();
            Bounds bodyBounds = UnionOf(layers, PartType.Body) ?? all;
            foreach (Layer layer in layers)
            {
                if (IsLimb(layer.part))
                {
                    layer.side = layer.bounds.center.x < bodyBounds.center.x ? Side.Left : Side.Right;
                }
                else
                {
                    layer.side = Side.Center;
                }
            }
            return layers;
        }

        // 判別結果を文章にする（ウィンドウに表示する用）
        public static string Describe(List<Layer> layers)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"レイヤー数: {layers.Count}");
            foreach (Layer l in layers)
            {
                string note = l.guessed ? "（位置から推定）" : "";
                sb.AppendLine($"・{l.renderer.gameObject.name} → {PartName(l.part)}{SideName(l.side)}{note}");
            }
            return sb.ToString();
        }

        // ================================================================
        // ② 骨を作る
        // ================================================================
        public static GameObject Build(GameObject source, bool addIdle, List<PartType> aiParts, out string report)
        {
            // パーツの絵がなければ何もしない
            if (Analyze(source).Count == 0)
            {
                report = "パーツの絵が見つかりませんでした。PSDの読み込み設定で「Use as Rig」にチェックが入っているか確認してください。";
                return null;
            }

            Undo.SetCurrentGroupName("PaperRig リグ作成");

            // 元のキャラは残して隠し、コピーに骨を入れる
            GameObject rig = Object.Instantiate(source, source.transform.parent);
            rig.name = source.name + "_Rig";
            Undo.RegisterCreatedObjectUndo(rig, "PaperRig リグ作成");
            Undo.RecordObject(source, "PaperRig リグ作成");
            source.SetActive(false);

            List<Layer> layers = Analyze(rig, aiParts);

            Bounds all = UnionOf(layers).Value;
            bool hasBody = layers.Any(l => l.part == PartType.Body);
            Bounds bodyB = UnionOf(layers, PartType.Body) ?? all;
            float z = rig.transform.position.z;
            float gizmo = all.size.y * 0.012f;

            // --- 腰（いちばん根元の骨）：太ももの上端。太ももがなければ体の下のほう ---
            Bounds? legsB = UnionOf(layers, PartType.UpperLeg);
            float hipY = legsB?.max.y ?? (bodyB.min.y + bodyB.size.y * 0.1f);
            Vector3 hip = new Vector3(bodyB.center.x, hipY, z);
            Transform hipBone = CreateBone("Hip", hip, rig.transform, PartType.Unknown, Side.Center, gizmo);

            // --- 体：腰を中心に回る ---
            Transform bodyBone = CreateBone("Body", hip, hipBone, PartType.Body, Side.Center, gizmo);
            AttachLayers(layers, PartType.Body, Side.Center, bodyBone);

            // --- 頭：首（体の上端）を中心に回る ---
            Transform headBone = null;
            Bounds? headB = UnionOf(layers, PartType.Head);
            if (headB != null)
            {
                float neckY = hasBody ? bodyB.max.y : headB.Value.min.y;
                Vector3 neck = new Vector3(headB.Value.center.x, neckY, z);
                headBone = CreateBone("Head", neck, bodyBone, PartType.Head, Side.Center, gizmo);
                AttachLayers(layers, PartType.Head, Side.Center, headBone);
            }

            // --- 腕と脚：左右それぞれ、根元から順番に骨をつなげる ---
            var armBones = new List<Transform>();
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float sign = side == Side.Left ? -1f : 1f;

                // 腕は「肩のあたり」から探し始める
                Vector3 shoulder = new Vector3(bodyB.center.x + sign * bodyB.extents.x, bodyB.max.y, z);
                Transform arm = BuildChain(layers, side,
                    new[] { PartType.UpperArm, PartType.LowerArm, PartType.Hand },
                    shoulder, bodyBone, gizmo, z);
                if (arm != null) armBones.Add(arm);

                // 脚は「体の中心の高さ」から探し始める（スカートがあっても迷わないように）
                Vector3 legStart = new Vector3(bodyB.center.x + sign * bodyB.extents.x * 0.5f, bodyB.center.y, z);
                BuildChain(layers, side,
                    new[] { PartType.UpperLeg, PartType.LowerLeg, PartType.Foot },
                    legStart, hipBone, gizmo, z);
            }

            // --- 中身がなくなった空の入れ物（PSDのグループなど）を片付ける ---
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true).Reverse())
            {
                if (t == rig.transform) continue;
                if (t.childCount == 0 && t.GetComponents<Component>().Length == 1)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }

            // --- 待機モーション ---
            if (addIdle)
            {
                var idle = rig.AddComponent<PaperRigIdle>();
                idle.body = bodyBone;
                idle.head = headBone;
                idle.arms = armBones.ToArray();
            }

            Selection.activeGameObject = rig;
            report = "完成: " + rig.name + "\n" + Describe(layers);
            return rig;
        }

        // 完成したキャラを Assets/PaperRig/Output にプレハブとして保存する
        public static string SaveAsPrefab(GameObject rig)
        {
            if (!AssetDatabase.IsValidFolder("Assets/PaperRig")) AssetDatabase.CreateFolder("Assets", "PaperRig");
            if (!AssetDatabase.IsValidFolder("Assets/PaperRig/Output")) AssetDatabase.CreateFolder("Assets/PaperRig", "Output");
            string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/PaperRig/Output/{rig.name}.prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(rig, path, InteractionMode.UserAction);
            return path;
        }

        // ================================================================
        // ここから下は ② で使う小さな部品
        // ================================================================

        // 腕や脚のように、つながったパーツの骨を根元から順番に作る
        static Transform BuildChain(List<Layer> layers, Side side, PartType[] chain,
                                    Vector3 anchor, Transform parent, float gizmo, float z)
        {
            Transform first = null;
            foreach (PartType part in chain)
            {
                Bounds? b = UnionOf(layers.Where(l => l.part == part && l.side == side));
                if (b == null) continue;   // このパーツがない絵もある

                // パーツの両端のうち、ひとつ前の関節に近い方をこのパーツの関節にする
                GetEnds(b.Value, z, out Vector3 end1, out Vector3 end2);
                bool end1IsJoint = (end1 - anchor).sqrMagnitude <= (end2 - anchor).sqrMagnitude;
                Vector3 joint = end1IsJoint ? end1 : end2;
                Vector3 tip = end1IsJoint ? end2 : end1;

                string name = part + (side == Side.Left ? "_L" : "_R");
                Transform bone = CreateBone(name, joint, parent, part, side, gizmo);
                AttachLayers(layers, part, side, bone);

                if (first == null) first = bone;
                parent = bone;   // 次のパーツはこの骨の子どもにする
                anchor = tip;    // 次の関節は、このパーツの先っぽの近くにある
            }
            return first;
        }

        // パーツの「細長い方向」の両端（少し内側）を求める
        static void GetEnds(Bounds b, float z, out Vector3 end1, out Vector3 end2)
        {
            Vector3 center = new Vector3(b.center.x, b.center.y, z);
            if (b.size.y >= b.size.x)
            {
                // 縦長：上と下
                float inset = Mathf.Min(b.size.x * 0.5f, b.size.y * 0.3f);
                float half = b.extents.y - inset;
                end1 = center + Vector3.up * half;
                end2 = center - Vector3.up * half;
            }
            else
            {
                // 横長：右と左
                float inset = Mathf.Min(b.size.y * 0.5f, b.size.x * 0.3f);
                float half = b.extents.x - inset;
                end1 = center + Vector3.right * half;
                end2 = center - Vector3.right * half;
            }
        }

        static Transform CreateBone(string name, Vector3 position, Transform parent,
                                    PartType part, Side side, float gizmo)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var bone = go.AddComponent<PaperRigBone>();
            bone.part = part;
            bone.side = side;
            bone.gizmoSize = gizmo;
            return go.transform;
        }

        // レイヤーを骨の子どもにする（見た目の位置はそのまま）
        static void AttachLayers(List<Layer> layers, PartType part, Side side, Transform bone)
        {
            foreach (Layer l in layers)
            {
                if (l.part == part && l.side == side)
                {
                    l.renderer.transform.SetParent(bone, true);
                }
            }
        }

        static Bounds? UnionOf(IEnumerable<Layer> layers)
        {
            Bounds? result = null;
            foreach (Layer l in layers)
            {
                if (result == null)
                {
                    result = l.bounds;
                }
                else
                {
                    Bounds b = result.Value;
                    b.Encapsulate(l.bounds);
                    result = b;
                }
            }
            return result;
        }

        static Bounds? UnionOf(List<Layer> layers, PartType part)
        {
            return UnionOf(layers.Where(l => l.part == part));
        }

        static bool Contains2D(Bounds b, Vector3 p)
        {
            return p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
        }

        static float Area(Bounds b)
        {
            return b.size.x * b.size.y;
        }

        static bool IsLimb(PartType part)
        {
            return part == PartType.UpperArm || part == PartType.LowerArm || part == PartType.Hand
                || part == PartType.UpperLeg || part == PartType.LowerLeg || part == PartType.Foot;
        }

        static string PartName(PartType part)
        {
            switch (part)
            {
                case PartType.Head: return "頭";
                case PartType.Body: return "体";
                case PartType.UpperArm: return "上腕";
                case PartType.LowerArm: return "前腕";
                case PartType.Hand: return "手";
                case PartType.UpperLeg: return "太もも";
                case PartType.LowerLeg: return "すね";
                case PartType.Foot: return "足先";
                default: return "不明";
            }
        }

        static string SideName(Side side)
        {
            switch (side)
            {
                case Side.Left: return "（画面左）";
                case Side.Right: return "（画面右）";
                default: return "";
            }
        }
    }
}