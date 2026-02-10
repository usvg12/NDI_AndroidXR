// NDI SDK for Unity is licensed for non-commercial use only.
// By using this script you agree to the NDI SDK non-commercial license terms.

using UnityEngine;

#if NDI_SDK_ENABLED
using NewTek.NDI;
#endif

namespace NDI
{
    public static class NdiSdkLifetime
    {
        private static readonly object SyncRoot = new object();
        private static int activeAcquireCount;

        public static bool TryAcquire()
        {
#if NDI_SDK_ENABLED
            lock (SyncRoot)
            {
                if (activeAcquireCount == 0 && !NDIlib.initialize())
                {
                    Debug.LogError("Failed to initialize NDI SDK global lifetime.");
                    return false;
                }

                activeAcquireCount++;
                return true;
            }
#else
            return false;
#endif
        }

        public static void Release()
        {
#if NDI_SDK_ENABLED
            lock (SyncRoot)
            {
                if (activeAcquireCount <= 0)
                {
                    Debug.LogWarning("NDI SDK lifetime release called without a matching acquire.");
                    activeAcquireCount = 0;
                    return;
                }

                activeAcquireCount--;
                if (activeAcquireCount == 0)
                {
                    NDIlib.destroy();
                }
            }
#endif
        }
    }
}
