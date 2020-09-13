// DeleteRegistryValueAction.cs, 14.11.2019
// Copyright (C) Dominic Beger 24.03.2020

using System.Collections.Generic;
using System.Threading.Tasks;

namespace nUpdate.Actions
{
    public class DeleteRegistryValueAction : IUpdateAction
    {
        public string RegistryKey { get; set; }
        public IEnumerable<string> ValueNames { get; set; }
        public string Description => "Deletes values in the registry.";

        public Task Execute()
        {
            return Task.Run(() =>
            {
                foreach (var name in ValueNames)
                {
                    RegistryManager.DeleteValue(RegistryKey, name);
                }
            });
        }

        public bool ExecuteBeforeReplacingFiles { get; set; }
        public string Name => "DeleteRegistryValue";
    }
}