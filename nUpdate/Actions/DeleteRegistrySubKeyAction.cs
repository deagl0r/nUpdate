// DeleteRegistrySubkeyAction.cs, 14.11.2019
// Copyright (C) Dominic Beger 24.03.2020

using System.Collections.Generic;
using System.Threading.Tasks;

namespace nUpdate.Actions
{
    public class DeleteRegistrySubKeyAction : IUpdateAction
    {
        public string RegistryKey { get; set; }
        public IEnumerable<string> SubKeysToDelete { get; set; }
        public string Description => "Deletes a registry subkey.";

        public Task Execute()
        {
            return Task.Run(() =>
            {
                foreach (var subKey in SubKeysToDelete) RegistryManager.DeleteSubKey(RegistryKey, subKey);
            });
        }

        public bool ExecuteBeforeReplacingFiles { get; set; }
        public string Name => "DeleteRegistrySubKey";
    }
}