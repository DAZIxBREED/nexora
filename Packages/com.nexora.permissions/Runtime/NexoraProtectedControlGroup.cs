using UdonSharp;
using UnityEngine;
using Nexora.Api;

namespace Nexora.Permissions
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraProtectedControlGroup : UdonSharpBehaviour
    {
        public const byte ScopeControl = 0;
        public const byte ScopeTransport = 1;
        public const byte ScopePlaylist = 2;
        public const byte ScopeStreaming = 3;
        public const byte ScopeAutomation = 4;
        public const byte ScopeAdministration = 5;

        public NexoraAccessControl accessControl;

        [Header("Permission scope")]
        public byte scope = ScopeTransport;

        [Header("Protected interaction")]
        public CanvasGroup[] canvasGroups;
        public Collider[] protectedColliders;

        [Header("Optional presentation")]
        public GameObject[] authorizedOnlyObjects;
        public GameObject[] unauthorizedOnlyObjects;
        public GameObject[] lockedOnlyObjects;

        [Header("Refresh")]
        public float refreshIntervalSeconds = 0.5f;

        [HideInInspector] public bool localAuthorized;
        [HideInInspector] public byte localRole;
        [HideInInspector] public int appliedPolicyRevision = -1;

        private bool refreshScheduled;

        private void Start()
        {
            RefreshNow();
            ScheduleRefresh();
        }

        public void RefreshTick()
        {
            refreshScheduled = false;
            RefreshNow();
            ScheduleRefresh();
        }

        public void RefreshNow()
        {
            if (accessControl == null)
            {
                localAuthorized = false;
                ApplyInteraction(false);
                return;
            }

            byte role = accessControl.LocalRole();
            bool policyChanged = appliedPolicyRevision != accessControl.policyRevision;
            bool roleChanged = role != localRole;
            if (!policyChanged && !roleChanged) return;

            localRole = role;
            appliedPolicyRevision = accessControl.policyRevision;
            localAuthorized = RoleAllowed(role);
            ApplyInteraction(localAuthorized);
        }

        private bool RoleAllowed(byte role)
        {
            byte minimum;
            if (scope == ScopeAdministration)
            {
                minimum = accessControl.administrationMinimumRole;
            }
            else
            {
                byte actionMinimum = NexoraRole.Guest;
                if (scope == ScopeTransport) actionMinimum = accessControl.transportMinimumRole;
                else if (scope == ScopePlaylist) actionMinimum = accessControl.playlistMinimumRole;
                else if (scope == ScopeStreaming) actionMinimum = accessControl.streamingMinimumRole;
                else if (scope == ScopeAutomation) actionMinimum = accessControl.automationMinimumRole;

                byte globalMinimum = accessControl.controlsLocked ? accessControl.lockedMinimumRole : accessControl.unlockedMinimumRole;
                minimum = actionMinimum > globalMinimum ? actionMinimum : globalMinimum;
            }

            return role >= minimum;
        }

        private void ApplyInteraction(bool allowed)
        {
            if (canvasGroups != null)
            {
                int i = 0;
                while (i < canvasGroups.Length)
                {
                    CanvasGroup group = canvasGroups[i];
                    if (group != null)
                    {
                        group.interactable = allowed;
                        group.blocksRaycasts = allowed;
                    }
                    i++;
                }
            }

            if (protectedColliders != null)
            {
                int i = 0;
                while (i < protectedColliders.Length)
                {
                    if (protectedColliders[i] != null) protectedColliders[i].enabled = allowed;
                    i++;
                }
            }

            SetObjects(authorizedOnlyObjects, allowed);
            SetObjects(unauthorizedOnlyObjects, !allowed);
            SetObjects(lockedOnlyObjects, accessControl != null && accessControl.controlsLocked);
        }

        private void SetObjects(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            int i = 0;
            while (i < objects.Length)
            {
                if (objects[i] != null) objects[i].SetActive(active);
                i++;
            }
        }

        private void ScheduleRefresh()
        {
            if (refreshScheduled) return;
            refreshScheduled = true;
            SendCustomEventDelayedSeconds(nameof(RefreshTick), Mathf.Max(0.1f, refreshIntervalSeconds));
        }
    }
}
