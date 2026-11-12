namespace PaperRig
{
    // レイヤー名からパーツの種類を当てる
    public static class PartClassifier
    {
        // 上から順に調べる。くわしい名前（前腕など）を先に書くのがコツ
        static readonly (PartType part, string[] words)[] rules =
        {
            (PartType.LowerArm, new[] { "前腕", "lowerarm", "lower_arm", "forearm" }),
            (PartType.UpperArm, new[] { "上腕", "upperarm", "upper_arm", "腕", "うで", "arm" }),
            (PartType.Hand,     new[] { "手", "hand" }),
            (PartType.LowerLeg, new[] { "すね", "脛", "lowerleg", "lower_leg", "shin" }),
            (PartType.Foot,     new[] { "足先", "靴", "くつ", "foot", "shoe" }),
            (PartType.UpperLeg, new[] { "太もも", "もも", "腿", "upperleg", "upper_leg", "thigh", "脚", "足", "あし", "leg" }),
            (PartType.Head,     new[] { "頭", "あたま", "顔", "かお", "髪", "かみ", "目", "瞳", "口", "眉", "鼻", "耳", "頬", "ほお", "帽子",
                                        "head", "face", "hair", "eye", "brow", "mouth", "nose", "cheek", "hat" }),
            (PartType.Body,     new[] { "体", "からだ", "胴", "胸", "腰", "body", "torso", "chest" }),
        };

        public static PartType Classify(string layerName)
        {
            string name = layerName.ToLowerInvariant();
            foreach (var rule in rules)
            {
                foreach (string word in rule.words)
                {
                    if (name.Contains(word)) return rule.part;
                }
            }
            return PartType.Unknown;
        }
    }
}