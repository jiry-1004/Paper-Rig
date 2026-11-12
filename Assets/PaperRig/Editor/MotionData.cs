using System;

namespace PaperRig
{
    // 1つのモーション（動き）のデータ。用意したモーションも、AIが作るモーションもこの形
    [Serializable]
    public class MotionData
    {
        public string name = "Motion";   // 英数字の名前（Walk など）
        public float duration = 1f;      // 1回分の長さ（秒）
        public bool loop = true;         // くり返すかどうか
        public MotionKey[] keys = new MotionKey[0];
    }

    // ある瞬間のポーズ
    [Serializable]
    public class MotionKey
    {
        public float time;               // 0〜1（モーションのどのあたりか。0.5 = ちょうど半分）
        public float hipY;               // 腰（キャラ全体）の上下。キャラの高さに対する割合（0.1 = 1割上）
        public BoneAngle[] bones = new BoneAngle[0];
    }

    // 1本の骨の角度
    [Serializable]
    public class BoneAngle
    {
        public string bone;              // 骨の名前（UpperArm_L など）
        public float angle;              // 角度（度）。反時計回りがプラス。0 = 描いたままのポーズ
    }

    // 最初から用意しておくモーション（正面向きのキャラ用）
    // 角度のルール：_L は画面の左側。左の腕や脚を外側へ広げるとマイナス、右の腕や脚はプラス
    public static class MotionPresets
    {
        public static MotionData[] All => new[] { Idle(), Walk(), Jump(), Wave() };

        // 日本語の表示名
        public static string Label(string name)
        {
            switch (name)
            {
                case "Idle": return "待機";
                case "Walk": return "歩く";
                case "Jump": return "ジャンプ";
                case "Wave": return "手をふる";
                default: return name;
            }
        }

        // データを短く書くための部品
        static BoneAngle B(string bone, float angle) => new BoneAngle { bone = bone, angle = angle };
        static MotionKey K(float time, float hipY, params BoneAngle[] bones) => new MotionKey { time = time, hipY = hipY, bones = bones };

        // 待機：ゆっくり呼吸して、首をかしげる
        public static MotionData Idle() => new MotionData
        {
            name = "Idle",
            duration = 2f,
            loop = true,
            keys = new[]
            {
                K(0f,   0f,      B("Body", 0f),   B("Head", -2f), B("UpperArm_L", -2f), B("UpperArm_R", 2f)),
                K(0.5f, -0.005f, B("Body", 1.5f), B("Head", 3f),  B("UpperArm_L", -5f), B("UpperArm_R", 5f)),
                K(1f,   0f,      B("Body", 0f),   B("Head", -2f), B("UpperArm_L", -2f), B("UpperArm_R", 2f)),
            }
        };

        // 歩く：体を左右にゆらしながら、ぴょこぴょこ歩く（正面向き）
        public static MotionData Walk() => new MotionData
        {
            name = "Walk",
            duration = 0.8f,
            loop = true,
            keys = new[]
            {
                K(0f,    0f,    B("Body", 0f),  B("Head", 0f),  B("UpperLeg_L", 0f),  B("UpperLeg_R", 0f), B("UpperArm_L", 0f),   B("UpperArm_R", 0f)),
                K(0.25f, 0.02f, B("Body", -4f), B("Head", 2f),  B("UpperLeg_L", -8f), B("UpperLeg_R", 2f), B("UpperArm_L", -4f),  B("UpperArm_R", 10f)),
                K(0.5f,  0f,    B("Body", 0f),  B("Head", 0f),  B("UpperLeg_L", 0f),  B("UpperLeg_R", 0f), B("UpperArm_L", 0f),   B("UpperArm_R", 0f)),
                K(0.75f, 0.02f, B("Body", 4f),  B("Head", -2f), B("UpperLeg_L", -2f), B("UpperLeg_R", 8f), B("UpperArm_L", -10f), B("UpperArm_R", 4f)),
                K(1f,    0f,    B("Body", 0f),  B("Head", 0f),  B("UpperLeg_L", 0f),  B("UpperLeg_R", 0f), B("UpperArm_L", 0f),   B("UpperArm_R", 0f)),
            }
        };

        // ジャンプ：しゃがむ → 跳ぶ → 着地（1回だけ）
        public static MotionData Jump() => new MotionData
        {
            name = "Jump",
            duration = 1f,
            loop = false,
            keys = new[]
            {
                K(0f,    0f,     B("UpperLeg_L", 0f),   B("LowerLeg_L", 0f),  B("UpperLeg_R", 0f),  B("LowerLeg_R", 0f),   B("UpperArm_L", 0f),    B("UpperArm_R", 0f)),
                K(0.15f, -0.04f, B("UpperLeg_L", -10f), B("LowerLeg_L", 15f), B("UpperLeg_R", 10f), B("LowerLeg_R", -15f), B("UpperArm_L", 10f),   B("UpperArm_R", -10f)),
                K(0.35f, 0.22f,  B("UpperLeg_L", 0f),   B("LowerLeg_L", 0f),  B("UpperLeg_R", 0f),  B("LowerLeg_R", 0f),   B("UpperArm_L", -120f), B("UpperArm_R", 120f)),
                K(0.55f, 0.28f,  B("UpperLeg_L", -15f), B("LowerLeg_L", 20f), B("UpperLeg_R", 15f), B("LowerLeg_R", -20f), B("UpperArm_L", -110f), B("UpperArm_R", 110f)),
                K(0.8f,  -0.03f, B("UpperLeg_L", -10f), B("LowerLeg_L", 15f), B("UpperLeg_R", 10f), B("LowerLeg_R", -15f), B("UpperArm_L", -20f),  B("UpperArm_R", 20f)),
                K(1f,    0f,     B("UpperLeg_L", 0f),   B("LowerLeg_L", 0f),  B("UpperLeg_R", 0f),  B("LowerLeg_R", 0f),   B("UpperArm_L", 0f),    B("UpperArm_R", 0f)),
            }
        };

        // 手をふる：右手を上げて、ひじから先をふる
        public static MotionData Wave() => new MotionData
        {
            name = "Wave",
            duration = 1.2f,
            loop = true,
            keys = new[]
            {
                K(0f,    0f, B("Head", 0f), B("UpperArm_R", 130f), B("LowerArm_R", 0f)),
                K(0.25f, 0f, B("Head", 3f), B("UpperArm_R", 130f), B("LowerArm_R", 30f)),
                K(0.5f,  0f, B("Head", 0f), B("UpperArm_R", 130f), B("LowerArm_R", 0f)),
                K(0.75f, 0f, B("Head", 3f), B("UpperArm_R", 130f), B("LowerArm_R", 30f)),
                K(1f,    0f, B("Head", 0f), B("UpperArm_R", 130f), B("LowerArm_R", 0f)),
            }
        };
    }
}