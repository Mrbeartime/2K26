using UnityEngine;

namespace ProjectD.Menus
{
    /// <summary>Prevents one Escape press from closing multiple stacked screens.</summary>
    public static class MenuInput
    {
        private static int handledFrame = -1;
        public static bool BackHandledThisFrame => handledFrame == Time.frameCount;
        public static bool ConsumeBack()
        {
            if (BackHandledThisFrame) return false;
            handledFrame = Time.frameCount;
            return true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => handledFrame = -1;
    }
}
