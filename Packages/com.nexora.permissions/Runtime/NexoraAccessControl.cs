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

        [Header("Global control policy")]
        [UdonSynced] public bool controlsLocked;
        [UdonSynced] public int policyRevision;
        [UdonSynced] public int lastPolicyActorId = -1;
        [UdonSynced] public byte unlockedMinimumRole = NexoraRole.Guest;
        [UdonSynced] public byte lockedMinimumRole = NexoraRole.DJ;
        [UdonSynced] public byte administrationMinimumRole = NexoraRole.Moderator;

        [Header("Action-specific minimum roles")]
        [UdonSynced] public byte transportMinimumRole = NexoraRole.Guest;
        [UdonSynced] public byte playlistMinimumRole = NexoraRole.Guest;
        [UdonSynced] public byte requestMinimumRole = NexoraRole.Guest;
        [UdonSynced] public byte streamingMinimumRole = NexoraRole.Guest;
        [UdonSynced] public byte automationMinimumRole = NexoraRole.DJ;

        [Header("Local audit ring")]
        public int auditCapacity = 48;
        [HideInInspector] public string[] auditActions;
        [HideInInspector] public byte[] auditRoles;
        [HideInInspector] public bool[] auditAllowed;
        [HideInInspector] public int[] auditPlayerIds;
        [HideInInspector] public float[] auditLocalTimes;
        [HideInInspector] public int auditWriteIndex;
        [HideInInspector] public int auditCount;

        [Header("Local counters")]
        [HideInInspector] public int allowedControlCount;
        [HideInInspector] public int deniedControlCount;
        [HideInInspector] public int allowedAdministrationCount;
        [HideInInspector] public int deniedAdministrationCount;
        [HideInInspector] public byte lastDecisionRole;
        [HideInInspector] public bool lastDecisionAllowed;
        [HideInInspector] public string lastDecisionAction;
        [HideInInspector] public int lastDecisionPlayerId = -1;
        [HideInInspector] public float lastDecisionLocalTime;

        private void Start()
        {
            if (auditCapacity < 8) auditCapacity = 8;
            auditActions = new string[auditCapacity];
            auditRoles = new byte[auditCapacity];
            auditAllowed = new bool[auditCapacity];
            auditPlayerIds = new int[auditCapacity];
            auditLocalTimes = new float[auditCapacity];
        }

        public byte LocalRole() { return RoleForPlayer(Networking.LocalPlayer); }

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

        public bool IsLocalAuthorized() { return AuthorizeControl("control"); }
        public bool AuthorizeControl(string action) { return AuthorizeRole(action, NexoraRole.Guest, false); }
        public bool AuthorizeTransport(string action) { return AuthorizeRole("transport:" + SafeAction(action), transportMinimumRole, false); }
        public bool AuthorizePlaylist(string action) { return AuthorizeRole("playlist:" + SafeAction(action), playlistMinimumRole, false); }
        public bool AuthorizeRequest(string action) { return AuthorizeRole("request:" + SafeAction(action), requestMinimumRole, false); }
        public bool AuthorizeStreaming(string action) { return AuthorizeRole("streaming:" + SafeAction(action), streamingMinimumRole, false); }
        public bool AuthorizeAutomation(string action) { return AuthorizeRole("automation:" + SafeAction(action), automationMinimumRole, false); }
        public bool IsLocalAdministrator() { return AuthorizeAdministration("administration"); }
        public bool AuthorizeAdministration(string action) { return AuthorizeRole("admin:" + SafeAction(action), administrationMinimumRole, true); }

        public bool CanControlWhileLocked() { return LocalRole() >= lockedMinimumRole; }
        public bool CanRoleControl(byte role) { return role >= EffectiveMinimum(NexoraRole.Guest); }
        public bool CanRoleAdminister(byte role) { return role >= administrationMinimumRole; }

        public void LockControls() { SetLocked(true); }
        public void UnlockControls() { SetLocked(false); }
        public void ToggleLocked() { SetLocked(!controlsLocked); }

        public void SetLocked(bool value)
        {
            if (!AuthorizeAdministration(value ? "lock-controls" : "unlock-controls")) return;
            if (controlsLocked == value) return;
            TakeOwnership();
            controlsLocked = value;
            CommitPolicyChange();
        }

        public void SetUnlockedMinimumRole(byte role) { if (!AuthorizeAdministration("set-unlocked-minimum")) return; TakeOwnership(); unlockedMinimumRole = ClampRole(role); CommitPolicyChange(); }
        public void SetLockedMinimumRole(byte role) { if (!AuthorizeAdministration("set-locked-minimum")) return; TakeOwnership(); lockedMinimumRole = ClampRole(role); CommitPolicyChange(); }
        public void SetAdministrationMinimumRole(byte role) { if (!AuthorizeAdministration("set-admin-minimum")) return; TakeOwnership(); administrationMinimumRole = ClampRole(role); CommitPolicyChange(); }
        public void SetTransportMinimumRole(byte role) { if (!AuthorizeAdministration("set-transport-minimum")) return; TakeOwnership(); transportMinimumRole = ClampRole(role); CommitPolicyChange(); }
        public void SetPlaylistMinimumRole(byte role) { if (!AuthorizeAdministration("set-playlist-minimum")) return; TakeOwnership(); playlistMinimumRole = ClampRole(role); CommitPolicyChange(); }
        public void SetRequestMinimumRole(byte role) { if (!AuthorizeAdministration("set-request-minimum")) return; TakeOwnership(); requestMinimumRole = ClampRole(role); CommitPolicyChange(); }
        public void SetStreamingMinimumRole(byte role) { if (!AuthorizeAdministration("set-streaming-minimum")) return; TakeOwnership(); streamingMinimumRole = ClampRole(role); CommitPolicyChange(); }
        public void SetAutomationMinimumRole(byte role) { if (!AuthorizeAdministration("set-automation-minimum")) return; TakeOwnership(); automationMinimumRole = ClampRole(role); CommitPolicyChange(); }

        public override void OnDeserialization()
        {
            NormalizePolicy();
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
            auditWriteIndex = 0;
            auditCount = 0;
            if (auditActions == null) return;
            int i = 0;
            while (i < auditActions.Length)
            {
                auditActions[i] = "";
                auditRoles[i] = NexoraRole.Guest;
                auditAllowed[i] = false;
                auditPlayerIds[i] = -1;
                auditLocalTimes[i] = 0f;
                i++;
            }
        }

        private bool AuthorizeRole(string action, byte actionMinimum, bool administration)
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            byte role = RoleForPlayer(player);
            bool validPlayer = player != null && player.IsValid();
            bool allowed = validPlayer && role >= (administration ? administrationMinimumRole : EffectiveMinimum(actionMinimum));
            if (administration)
            {
                if (allowed) allowedAdministrationCount++; else deniedAdministrationCount++;
            }
            else
            {
                if (allowed) allowedControlCount++; else deniedControlCount++;
            }
            RecordDecision(player, role, allowed, action);
            return allowed;
        }

        private byte EffectiveMinimum(byte actionMinimum)
        {
            byte globalMinimum = controlsLocked ? lockedMinimumRole : unlockedMinimumRole;
            return actionMinimum > globalMinimum ? actionMinimum : globalMinimum;
        }

        private void CommitPolicyChange()
        {
            NormalizePolicy();
            policyRevision++;
            VRCPlayerApi local = Networking.LocalPlayer;
            lastPolicyActorId = local == null ? -1 : local.playerId;
            RequestSerialization();
            NotifyPolicyChanged();
        }

        private void NormalizePolicy()
        {
            unlockedMinimumRole = ClampRole(unlockedMinimumRole);
            lockedMinimumRole = ClampRole(lockedMinimumRole);
            administrationMinimumRole = ClampRole(administrationMinimumRole);
            transportMinimumRole = ClampRole(transportMinimumRole);
            playlistMinimumRole = ClampRole(playlistMinimumRole);
            requestMinimumRole = ClampRole(requestMinimumRole);
            streamingMinimumRole = ClampRole(streamingMinimumRole);
            automationMinimumRole = ClampRole(automationMinimumRole);
        }

        private void NotifyPolicyChanged() { if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.LockChanged); }

        private void RecordDecision(VRCPlayerApi player, byte role, bool allowed, string action)
        {
            lastDecisionRole = role;
            lastDecisionAllowed = allowed;
            lastDecisionAction = string.IsNullOrEmpty(action) ? "control" : action;
            lastDecisionPlayerId = player == null ? -1 : player.playerId;
            lastDecisionLocalTime = Time.realtimeSinceStartup;
            if (auditActions == null || auditActions.Length == 0) return;
            auditActions[auditWriteIndex] = lastDecisionAction;
            auditRoles[auditWriteIndex] = role;
            auditAllowed[auditWriteIndex] = allowed;
            auditPlayerIds[auditWriteIndex] = lastDecisionPlayerId;
            auditLocalTimes[auditWriteIndex] = lastDecisionLocalTime;
            auditWriteIndex++;
            if (auditWriteIndex >= auditActions.Length) auditWriteIndex = 0;
            if (auditCount < auditActions.Length) auditCount++;
        }

        private string SafeAction(string action) { return string.IsNullOrEmpty(action) ? "command" : action; }
        private byte ClampRole(byte role) { return role > NexoraRole.Master ? NexoraRole.Master : role; }

        private void TakeOwnership()
        {
            if (!Networking.IsOwner(gameObject) && Networking.LocalPlayer != null) Networking.SetOwner(Networking.LocalPlayer, gameObject);
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
