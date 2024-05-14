/* Copyright (c) 1996-2022 The OPC Foundation. All rights reserved.
   The source code in this file is covered under a dual-license scenario:
     - RCL: for OPC Foundation Corporate Members in good-standing
     - GPL V2: everybody else
   RCL license terms accompanied with this source code. See http://opcfoundation.org/License/RCL/1.00/
   GNU General Public License as published by the Free Software Foundation;
   version 2 of the License are accompanied with this source code. See http://opcfoundation.org/License/GPLv2
   This source code is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
*/

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Opc.Ua
{
    /// <summary>
    /// A common interface for custom AsyncResult objects 
    /// </summary>
    public interface IChannelAsyncOperation : IAsyncResult, IDisposable
    {
        /// <summary>
        /// Called when an async operation is complete but faulted.
        /// </summary>
        bool Fault(ServiceResult error);

        /// <summary>
        /// Called when an async operation is complete but faulted.
        /// </summary>
        /// <param name="doNotBlock">Issue the callback on a new task.</param>
        /// <param name="error">The error which caused the fault.</param>
        /// <returns></returns>
        bool Fault(bool doNotBlock, ServiceResult error);

        /// <summary>
        /// Synchronously wait for the operation to complete.
        /// </summary>
        void End(int timeout, bool throwOnError = true);

        /// <summary>
        /// Asynchronously wait for the operation to complete.
        /// </summary>
        Task EndAsync(int timeout, bool throwOnError = true, CancellationToken ct = default);

        /// <summary>
        /// Return the error result of the operation.
        /// </summary>
        ServiceResult Error { get; }
    }
}
