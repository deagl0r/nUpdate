// ConnectionManager.cs, 10.06.2019
// Copyright (C) Dominic Beger 17.06.2019

using nUpdate.Win32;

namespace nUpdate
{
    public class ConnectionManager
    {
        public static bool IsConnectionAvailable()
        {
            int desc;
            return NativeMethods.InternetGetConnectedState(out desc, 0);
        }
    }
}