/**
* Copyright 2021 The Nakama Authors
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System.Collections.Generic;
using Nakama;

namespace NakamaSync
{
    internal class IncomingVarIngress : ISyncService
    {
        public ILogger Logger
        {
            get;
            set;
        }

        public SyncErrorHandler ErrorHandler
        {
            get;
            set;
        }

        private readonly IncomingVarGuestIngress _guestIngress;
        private readonly IncomingVarHostIngress _sharedHostIngress;
        private readonly VarRegistry _registry;
        private readonly LockVersionGuard _lockVersionGuard;

        public IncomingVarIngress(
            IncomingVarGuestIngress guestIngress, IncomingVarHostIngress sharedHostIngress, VarRegistry registry, LockVersionGuard lockVersionGuard)
        {
            _guestIngress = guestIngress;
            _sharedHostIngress = sharedHostIngress;
            _registry = registry;
            _lockVersionGuard = lockVersionGuard;
        }

        public void Subscribe(SyncSocket socket, HostTracker hostTracker)
        {
            socket.OnSyncEnvelope += (source, envelope) =>
            {
                ReceiveSyncEnvelope(source, envelope, hostTracker.IsSelfHost());
            };
        }

        public void ReceiveSyncEnvelope(IUserPresence source, Envelope envelope, bool isHost)
        {
            Logger?.DebugFormat($"Shared role ingress received sync envelope.");

            var bools = IncomingVarIngressContext.FromBoolValues(envelope, _registry);
            ReceiveSyncEnvelope(source, bools, isHost);

            var floats = IncomingVarIngressContext.FromFloatValues(envelope, _registry);
            ReceiveSyncEnvelope(source, floats, isHost);

            var ints = IncomingVarIngressContext.FromIntValues(envelope, _registry);
            ReceiveSyncEnvelope(source, ints, isHost);

            var strings = IncomingVarIngressContext.FromStringValues(envelope, _registry);
            ReceiveSyncEnvelope(source, strings, isHost);

            var objects = IncomingVarIngressContext.FromObjectValues(envelope, _registry);
            ReceiveSyncEnvelope(source, objects, isHost);

            Logger?.DebugFormat($"Shared role ingress done processing sync envelope.");
        }

        private void ReceiveSyncEnvelope<T>(IUserPresence source, List<IncomingVarIngressContext<T>> contexts, bool isHost)
        {
            Logger?.DebugFormat($"Shared role ingress processing num contexts: {contexts.Count}");

            foreach (IncomingVarIngressContext<T> context in contexts)
            {
                Logger?.DebugFormat($"Shared role ingress processing context: {context}");

                if (!_lockVersionGuard.IsValidLockVersion(context.Value.Key, context.Value.LockVersion))
                {
                    Logger?.DebugFormat($"Shared role ingress received invalid lock version: {context.Value.LockVersion}");
                    continue;
                }

                if (isHost)
                {
                    Logger?.InfoFormat($"Setting shared value for self as host: {context.Value}");
                    _sharedHostIngress.ProcessValue(source, context);
                }
                else
                {
                    Logger?.InfoFormat($"Setting shared value for self as guest: {context.Value}");
                    _guestIngress.ProcessValue(context.Var, source, context.Value);
                }
            }
        }
    }
}
