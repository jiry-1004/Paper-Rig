using UnityEngine;

namespace PaperRig
{
    // 再生ボタンを押すと、体のゆれ・首のかたむき・腕のゆれで待機モーションをする
    public class PaperRigIdle : MonoBehaviour
    {
        [Header("動かす骨（自動で設定されます）")]
        public Transform body;
        public Transform head;
        public Transform[] arms = new Transform[0];

        [Header("動きの強さ")]
        public float speed = 2f;        // 速さ
        public float bodySway = 1.5f;   // 体のゆれ（度）
        public float headTilt = 4f;     // 首のかたむき（度）
        public float armSwing = 5f;     // 腕のゆれ（度）

        Quaternion bodyStart;
        Quaternion headStart;
        Quaternion[] armStart;

        void Start()
        {
            // 最初の角度を覚えておく
            if (body != null) bodyStart = body.localRotation;
            if (head != null) headStart = head.localRotation;

            armStart = new Quaternion[arms.Length];
            for (int i = 0; i < arms.Length; i++)
            {
                if (arms[i] != null) armStart[i] = arms[i].localRotation;
            }
        }

        void Update()
        {
            float t = Time.time * speed;

            if (body != null)
            {
                body.localRotation = bodyStart * Quaternion.Euler(0f, 0f, bodySway * Mathf.Sin(t * 0.5f));
            }
            if (head != null)
            {
                head.localRotation = headStart * Quaternion.Euler(0f, 0f, headTilt * Mathf.Sin(t * 0.5f + 1f));
            }
            for (int i = 0; i < arms.Length; i++)
            {
                if (arms[i] == null) continue;
                // 左右の腕を逆向きにゆらす
                float direction = (i % 2 == 0) ? 1f : -1f;
                arms[i].localRotation = armStart[i] * Quaternion.Euler(0f, 0f, armSwing * Mathf.Sin(t) * direction);
            }
        }
    }
}