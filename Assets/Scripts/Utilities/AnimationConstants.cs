
using UnityEngine;

namespace RTS.Utilities
{
    public static class AnimationConstants
    {
        public static int SPEED = Animator.StringToHash("Speed");
        public static int IS_GATHERING = Animator.StringToHash("IsGathering");
        public static int ATTACK = Animator.StringToHash("Attack");
        public const string IS_HEALING = "IsHealing";
    }
}