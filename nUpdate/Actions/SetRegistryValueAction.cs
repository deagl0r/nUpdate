// SetRegistryValueAction.cs, 14.11.2019
// Copyright (C) Dominic Beger 24.03.2020

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace nUpdate.Actions
{
    public class SetRegistryValueAction : IUpdateAction
    {
        public string RegistryKey { get; set; }
        public List<Tuple<string, object, RegistryValueKind>> ValueTuples;
        public string Description => "Sets values in the registry.";

        public Task Execute()

        {
            return Task.Run(() =>
            {
                foreach (var (item1, item2, item3) in ValueTuples)
                {
                    RegistryManager.SetValue(RegistryKey, item1, item2, item3);
                }
            });
        }

        public bool ExecuteBeforeReplacingFiles { get; set; }
        public string Name => "SetRegistryValue";
    }
}