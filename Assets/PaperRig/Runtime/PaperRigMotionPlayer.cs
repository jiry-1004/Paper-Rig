using System.Collections.Generic;
using UnityEngine;

namespace PaperRig
{
    // 再生中、画面左上のボタンでモーションを切り替える（動きの確認用）
    [RequireComponent(typeof(Animator))]
    public class PaperRigMotionPlayer : MonoBehaviour
    {
        public List<string> motions = new List<string>();   // 自動で登録されます
        public bool showButtons = true;

        Animator animator;

        void Awake()
        {
            animator = GetComponent<Animator>();
        }

        void OnGUI()
        {
            if (!showButtons) return;

            GUILayout.BeginArea(new Rect(10, 10, 180, 40 + motions.Count * 40));
            foreach (string motion in motions)
            {
                if (GUILayout.Button(motion, GUILayout.Height(32)))
                {
                    animator.Play(motion, 0, 0f);   // 最初から再生する
                }
            }
            GUILayout.EndArea();
        }
    }
}