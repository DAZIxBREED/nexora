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

        [Header("Configured roles")]
        public string[] ownerNames;
        public string[] moderatorNames;
        public string[] djNames;
        public string[] trustedNames;

        [Header("Control policy")]
        [UdonSynced] public bool controlsLocked;
        [UdonSynced] public int policyRevision;
        [UdonSynced] public int lastPolicyActorId = -1;
        public byte unlockedMinimumRole = NexoraRole.Guest;
        public byte lockedMinimumRole = NexoraRole.DJ;
        public byte administrationMinimumRole = NexoraRole.Moderator;

        [Header("Local audit")]
        [HideInInspector] public int deniedControlCount;
        [HideInInspector] public int deniedAdministrationCount;
        [HideInInspector] public byte lastDeniedRole;

        public byte LocalRole()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (player == null || !player.IsValid()) return NexoraRole.Guest;
            if (player.isInstanceOwner) return NexoraRole.Owner;
            if (Networking.IsMaster) return NexoraRole.Master;
            return RoleFor(player.displayName);
        }

        public bool IsLocalAuthorized()
        {
            byte role = LocalRole();
            bool allowed = controlsLocked ? role >= lockedMinimumRole : role >= unlockedMinimumRole;
            if (!allowed)
            {
                deniedControlCount++;
                lastDeniedRole = role;
            }
            return allowed;
        }

        public bool IsLocalAdministrator()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (player == null || !player.IsValid())
            {
                deniedAdministrationCount++;
                lastDeniedRole = NexoraRole.Guest;
                return false;
            }
            if (player.isInstanceOwner || Networking.IsMaster) return true;

            byte role = LocalRole();
            bool allowed = role >= administrationMinimumRole;
            if (!allowed)
            {
                deniedAdministrationCount++;
                lastDeniedRole = role;
            }
            return allowed;
        }

        public bool CanControlWhileLocked()
        {
            return LocalRole() >= lockedMinimumRole;
        }

        public void LockControls() { SetLocked(true); }
        public void UnlockControls() { SetLocked(false); }
        public void ToggleLocked() { SetLocked(!controlsLocked); }

        public void SetLocked(bool value)
        {
            if (!IsLocalAdministrator()) return;
            TakeOwnership();
            controlsLocked = value;
            policyRevision++;
            VRCPlayerApi local = Networking.LocalPlayer;
            lastPolicyActorId = local == null ? -1 : local.playerId;
            RequestSerialization();
            NotifyPolicyChanged();
        }

        public override void OnDeserialization()
        {
            NotifyPolicyChanged();
        }

        private void NotifyPolicyChanged()
        {
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.LockChanged);
        }

        private byte RoleFor(string displayName)
        {
            if (Contains(ownerNames, displayName)) return NexoraRole.Owner;
            if (Contains(moderatorNames, displayName)) return NexoraRole.Moderator;
            if (Contains(djNames, displayName)) return NexoraRole.DJ;
            if (Contains(trustedNames, displayName)) return NexoraRole.Trusted;
            return NexoraRole.Guest;
        }

        private void TakeOwnership()
        {
            if (!Networking.IsOwner(gameObject) && Networking.LocalPlayer != null)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
        }

        private bool Contains(string[] names, string value)
        {
            if (names == null || string.IsNullOrEmpty(value)) return false;
            int i = 0;
            while (i < names.Length)
            {
                if (!string.IsNullOrEmpty(names[i]) && names[i] == value) return true;
                i++;
            }
            return false;
        }
    }
}
