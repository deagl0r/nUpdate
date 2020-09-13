// IOperationPanel.cs, 10.06.2019
// Copyright (C) Dominic Beger 17.06.2019

using nUpdate.Actions;

namespace nUpdate.Administration.Core.Operations.Panels
{
    public interface IOperationPanel
    {
        /// <summary>
        ///     Gets a value indicating whether the operation is valid or not.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        ///     Gets the current operation to perform.
        /// </summary>
        IUpdateAction Operation { get; }
    }
}