using UnityEngine;

namespace PaperRig
{
    // パーツの種類
    public enum PartType
    {
        Unknown,   // わからないもの（服や小物など）→ 位置から仲間を探す
        Head,      // 頭・顔・髪など
        Body,      // 体・胴・服
        UpperArm,  // 上腕（肩からひじ）
        LowerArm,  // 前腕（ひじから手首）
        Hand,      // 手
        UpperLeg,  // 太もも
        LowerLeg,  // すね
        Foot       // 足先・靴
    }

    // 画面で見てどちら側にあるか
    public enum Side
    {
        Center,
        Left,   // 画面の左側
        Right   // 画面の右側
    }

    // 自動で作った「骨」につける目印。
    // シーン画面に、関節の丸と、骨どうしをつなぐ線を表示する。
    public class PaperRigBone : MonoBehaviour
    {
        public PartType part;
        public Side side;
        public float gizmoSize = 0.1f;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0f);
            Gizmos.DrawWireSphere(transform.position, gizmoSize);

            foreach (Transform child in transform)
            {
                if (child.GetComponent<PaperRigBone>() != null)
                {
                    Gizmos.DrawLine(transform.position, child.position);
                }
            }
        }
    }
}