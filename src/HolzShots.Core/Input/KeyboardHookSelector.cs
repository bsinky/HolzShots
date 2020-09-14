using System;
using System.ComponentModel;
using System.Diagnostics;

namespace HolzShots.Input
{
    public static class KeyboardHookSelector
    {
        public static KeyboardHook CreateHookForCurrentPlatform(ISynchronizeInvoke invoke)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                case PlatformID.WinCE:
                    return new WindowsKeyboardHook(invoke);
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                case PlatformID.Xbox:
                default:
                    Debug.Fail();
                    throw new NotSupportedException();
            }
        }
    }
}
