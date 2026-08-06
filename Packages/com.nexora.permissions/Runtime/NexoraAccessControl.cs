using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Nexora.Api;
using Nexora.Core;

namespace Nexora.Permissions
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NexoraAccessControl : UdonSharpBehaviour
    {
        public NexoraModuleHost moduleHost;
        public string[] ownerNames;
        public string[] moderatorNames;
        public string[] djNames;
        [UdonSynced] public bool controlsLocked;
        public byte unlockedMinimumRole = NexoraRole.Guest;
        public byte lockedMinimumRole = NexoraRole.DJ;

        public bool IsLocalAuthorized()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (player == null || !player.IsValid()) return false;
            if (player.isInstanceOwner || Networking.IsMaster) return true;
            byte role = RoleFor(player.displayName);
            return controlsLocked ? role >= lockedMinimumRole : role >= unlockedMinimumRole;
        }

        public void SetLocked(bool value)
        {
            if (!IsLocalAuthorized()) return;
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
            controlsLocked = value;
            RequestSerialization();
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.LockChanged);
        }

        public override void OnDeserialization()
        {
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.LockChanged);
        }

        private byte RoleFor(string displayName)
        {
            if (Contains(ownerNames, displayName)) return NexoraRole.Owner;
            if (Contains(moderatorNames, displayName)) return NexoraRole.Moderator;
            if (Contains(djNames, displayName)) return NexoraRole.DJ;
            return NexoraRole.Guest;
        }

        private bool Contains(string[] names, string value)
        {
            if (names == null) return false;
            int i = 0;
            while (i < names.Length)
            {
                if (names[i] == value) return true;
                i++;
            }
            return false;
        }
    }
}
