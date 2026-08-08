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
        [UdonSynced] public byte unlockedMinimumRole = NexoraRole.Guest;
        [UdonSynced] public byte lockedMinimumRole = NexoraRole.DJ;
        [UdonSynced] public byte administrationMinimumRole = NexoraRole.Moderator;

        [Header("Local audit")]
        [HideInInspector] public int allowedControlCount;
        [HideInInspector] public int deniedControlCount;
        [HideInInspector] public int allowedAdministrationCount;
        [HideInInspector] public int deniedAdministrationCount;
        [HideInInspector] public byte lastDecisionRole;
        [HideInInspector] public bool lastDecisionAllowed;
        [HideInInspector] public string lastDecisionAction;
        [HideInInspector] public int lastDecisionPlayerId = -1;
        [HideInInspector] public float lastDecisionLocalTime;

        public byte LocalRole()
        {
            return RoleForPlayer(Networking.LocalPlayer);
        }

        public byte RoleForPlayer(VRCPlayerApi player)
        {
            if (player == null || !player.IsValid()) return NexoraRole.Guest;
            if (player.isInstanceOwner) return NexoraRole.Owner;
            if (player.isMaster) return NexoraRole.Master;
            return RoleForName(player.displayName);
        }

        public byte RoleForName(string displayName)
        {
            if (Contains(ownerNames, displayName)) return NexoraRole.Owner;
            if (Contains(moderatorNames, displayName)) return NexoraRole.Moderator;
            if (Contains(djNames, displayName)) return NexoraRole.DJ;
            if (Contains(trustedNames, displayName)) return NexoraRole.Trusted;
            return NexoraRole.Guest;
        }

        public bool IsLocalAuthorized()
        {
            return AuthorizeControl("control");
        }

        public bool AuthorizeControl(string action)
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            byte role = RoleForPlayer(player);
            byte minimum = controlsLocked ? lockedMinimumRole : unlockedMinimumRole;
            bool allowed = role >= minimum;

            if (allowed) allowedControlCount++;
            else deniedControlCount++;

            RecordDecision(player, role, allowed, action);
            return allowed;
        }

        public bool IsLocalAdministrator()
        {
            return AuthorizeAdministration("administration");
        }

        public bool AuthorizeAdministration(string action)
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            byte role = RoleForPlayer(player);
            bool allowed = false;

            if (player != null && player.IsValid())
            {
                allowed = player.isInstanceOwner || player.isMaster || role >= administrationMinimumRole;
            }

            if (allowed) allowedAdministrationCount++;
            else deniedAdministrationCount++;

            RecordDecision(player, role, allowed, action);
            return allowed;
        }

        public bool CanControlWhileLocked()
        {
            return LocalRole() >= lockedMinimumRole;
        }

        public bool CanRoleControl(byte role)
        {
            return role >= (controlsLocked ? lockedMinimumRole : unlockedMinimumRole);
        }

        public bool CanRoleAdminister(byte role)
        {
            return role >= administrationMinimumRole;
        }

        public void LockControls() { SetLocked(true); }
        public void UnlockControls() { SetLocked(false); }
        public void ToggleLocked() { SetLocked(!controlsLocked); }

        public void SetLocked(bool value)
        {
            if (!AuthorizeAdministration(value ? "lock controls" : "unlock controls")) return;
            if (controlsLocked == value) return;

            TakeOwnership();
            controlsLocked = value;
            policyRevision++;
            VRCPlayerApi local = Networking.LocalPlayer;
            lastPolicyActorId = local == null ? -1 : local.playerId;
            RequestSerialization();
            NotifyPolicyChanged();
        }

        public void SetUnlockedMinimumRole(byte role)
        {
            if (!AuthorizeAdministration("set unlocked minimum role")) return;
            TakeOwnership();
            unlockedMinimumRole = ClampRole(role);
            policyRevision++;
            RequestSerialization();
            NotifyPolicyChanged();
        }

        public void SetLockedMinimumRole(byte role)
        {
            if (!AuthorizeAdministration("set locked minimum role")) return;
            TakeOwnership();
            lockedMinimumRole = ClampRole(role);
            policyRevision++;
            RequestSerialization();
            NotifyPolicyChanged();
        }

        public void SetAdministrationMinimumRole(byte role)
        {
            if (!AuthorizeAdministration("set administration minimum role")) return;
            TakeOwnership();
            administrationMinimumRole = ClampRole(role);
            policyRevision++;
            RequestSerialization();
            NotifyPolicyChanged();
        }

        public override void OnDeserialization()
        {
            NotifyPolicyChanged();
        }

        public void ResetLocalAudit()
        {
            allowedControlCount = 0;
            deniedControlCount = 0;
            allowedAdministrationCount = 0;
            deniedAdministrationCount = 0;
            lastDecisionRole = NexoraRole.Guest;
            lastDecisionAllowed = false;
            lastDecisionAction = "";
            lastDecisionPlayerId = -1;
            lastDecisionLocalTime = 0f;
        }

        private void NotifyPolicyChanged()
        {
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.LockChanged);
        }

        private void RecordDecision(VRCPlayerApi player, byte role, bool allowed, string action)
        {
            lastDecisionRole = role;
            lastDecisionAllowed = allowed;
            lastDecisionAction = string.IsNullOrEmpty(action) ? "control" : action;
            lastDecisionPlayerId = player == null ? -1 : player.playerId;
            lastDecisionLocalTime = Time.realtimeSinceStartup;
        }

        private byte ClampRole(byte role)
        {
            if (role > NexoraRole.Master) return NexoraRole.Master;
            return role;
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
