// Extensions.cs, 10.06.2019
// Copyright (C) Dominic Beger 17.06.2019

using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace nUpdate
{
    internal static class Extensions
    {
        public static void Empty(this DirectoryInfo directory)
        {
            foreach (var file in directory.GetFiles())
                file.Delete();
            foreach (var subDirectory in directory.GetDirectories())
                subDirectory.Delete(true);
        }

        public static async Task<HttpWebResponse> GetResponseAsync(this HttpWebRequest request, CancellationToken ct)
        {
            using (ct.Register(request.Abort, useSynchronizationContext: false))
            {
                var response = await request.GetResponseAsync();
                ct.ThrowIfCancellationRequested();
                return (HttpWebResponse)response;
            }
        }
    }
}