using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Nexora.Core;
using Nexora.Sync;
using Nexora.Permissions;

namespace Nexora.Playlists
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NexoraPlaylist : UdonSharpBehaviour
    {
        public NexoraMediaState state;
        public NexoraAccessControl access;
        public NexoraModuleHost moduleHost;

        [Header("Playlist catalog")]
        public VRCUrl[] urls;
        public string[] titles;

        [Header("Synchronized selection")]
        [UdonSynced] public int currentIndex = -1;
        [UdonSynced] public int previousIndex = -1;
        [UdonSynced] public int queuedIndex = -1;
        [UdonSynced] public int failedIndex = -1;
        [UdonSynced] public int consecutiveFailureCount;
        [UdonSynced] public int totalFailureCount;
        [UdonSynced] public bool repeatPlaylist;
        [UdonSynced] public bool repeatCurrent;
        [UdonSynced] public bool skipFailedItems = true;
        [UdonSynced] public int playlistRevision;

        [Header("Failure policy")]
        public int maximumConsecutiveFailures = 4;
        public bool stopWhenFailureBudgetExhausted = true;

        [HideInInspector] public int successfulSelectionCount;
        [HideInInspector] public int automaticSkipCount;

        public int Count() { return urls == null ? 0 : urls.Length; }
        public bool HasCurrent() { return IsValidIndex(currentIndex); }
        public bool HasQueued() { return IsValidIndex(queuedIndex); }

        public string CurrentTitle()
        {
            if (!HasCurrent() || titles == null || currentIndex >= titles.Length) return "";
            return titles[currentIndex];
        }

        public string TitleAt(int index)
        {
            if (!IsValidIndex(index) || titles == null || index >= titles.Length) return "";
            return titles[index];
        }

        public void Select(int index)
        {
            if (!CanMutate() || !IsValidIndex(index)) return;
            SelectInternal(index, false);
        }

        public void QueueNext(int index)
        {
            if (!CanMutate() || !IsValidIndex(index)) return;
            TakeOwnership();
            queuedIndex = index;
            playlistRevision++;
            RequestSerialization();
            NotifyChanged();
        }

        public void ClearQueue()
        {
            if (!CanMutate()) return;
            TakeOwnership();
            queuedIndex = -1;
            playlistRevision++;
            RequestSerialization();
            NotifyChanged();
        }

        public void ReportCurrentSucceeded()
        {
            if (!CanMutate() || !HasCurrent()) return;
            TakeOwnership();
            failedIndex = -1;
            consecutiveFailureCount = 0;
            successfulSelectionCount++;
            playlistRevision++;
            RequestSerialization();
            NotifyChanged();
        }

        public void ReportCurrentFailed()
        {
            if (!CanMutate() || !HasCurrent()) return;

            TakeOwnership();
            failedIndex = currentIndex;
            consecutiveFailureCount++;
            totalFailureCount++;
            playlistRevision++;
            RequestSerialization();
            NotifyChanged();

            if (!skipFailedItems) return;

            if (maximumConsecutiveFailures > 0 && consecutiveFailureCount >= maximumConsecutiveFailures)
            {
                if (stopWhenFailureBudgetExhausted && state != null)
                {
                    state.Stop();
                }
                return;
            }

            AdvanceAfterFailure();
        }

        public void Next()
        {
            if (!CanMutate() || Count() == 0) return;

            if (repeatCurrent && HasCurrent())
            {
                SelectInternal(currentIndex, false);
                return;
            }

            if (HasQueued())
            {
                int queued = queuedIndex;
                queuedIndex = -1;
                SelectInternal(queued, false);
                return;
            }

            int next = NextSequentialIndex(currentIndex);
            if (next >= 0) SelectInternal(next, false);
        }

        public void Previous()
        {
            if (!CanMutate() || Count() == 0) return;

            if (IsValidIndex(previousIndex))
            {
                int target = previousIndex;
                previousIndex = currentIndex;
                SelectInternal(target, true);
                return;
            }

            int previous = currentIndex < 0 ? 0 : currentIndex - 1;
            if (previous < 0)
            {
                if (!repeatPlaylist) return;
                previous = Count() - 1;
            }
            SelectInternal(previous, false);
        }

        public void SetRepeatPlaylist(bool value)
        {
            if (!CanMutate()) return;
            TakeOwnership();
            repeatPlaylist = value;
            playlistRevision++;
            RequestSerialization();
            NotifyChanged();
        }

        public void SetRepeatCurrent(bool value)
        {
            if (!CanMutate()) return;
            TakeOwnership();
            repeatCurrent = value;
            playlistRevision++;
            RequestSerialization();
            NotifyChanged();
        }

        public void SetSkipFailedItems(bool value)
        {
            if (!CanMutate()) return;
            TakeOwnership();
            skipFailedItems = value;
            playlistRevision++;
            RequestSerialization();
            NotifyChanged();
        }

        public void ResetFailureBudget()
        {
            if (!CanMutate()) return;
            TakeOwnership();
            failedIndex = -1;
            consecutiveFailureCount = 0;
            playlistRevision++;
            RequestSerialization();
            NotifyChanged();
        }

        public override void OnDeserialization()
        {
            NotifyChanged();
        }

        private void AdvanceAfterFailure()
        {
            if (Count() == 0) return;

            if (HasQueued() && queuedIndex != failedIndex)
            {
                int queued = queuedIndex;
                queuedIndex = -1;
                automaticSkipCount++;
                SelectInternal(queued, false);
                return;
            }

            int next = NextSequentialIndex(failedIndex);
            if (next >= 0 && next != failedIndex)
            {
                automaticSkipCount++;
                SelectInternal(next, false);
            }
        }

        private int NextSequentialIndex(int fromIndex)
        {
            int count = Count();
            if (count == 0) return -1;

            int next = fromIndex < 0 ? 0 : fromIndex + 1;
            if (next >= count)
            {
                if (!repeatPlaylist) return -1;
                next = 0;
            }
            return next;
        }

        private void SelectInternal(int index, bool preserveHistory)
        {
            if (!IsValidIndex(index)) return;

            TakeOwnership();
            if (!preserveHistory && currentIndex != index) previousIndex = currentIndex;
            currentIndex = index;
            failedIndex = -1;
            if (queuedIndex == index) queuedIndex = -1;
            playlistRevision++;
            RequestSerialization();
            LoadCurrent();
            NotifyChanged();
        }

        private bool CanMutate()
        {
            return access != null && access.AuthorizeControl("playlist");
        }

        private bool IsValidIndex(int index)
        {
            return urls != null && index >= 0 && index < urls.Length;
        }

        private void LoadCurrent()
        {
            if (state == null || !HasCurrent()) return;
            state.LoadMedia(urls[currentIndex]);
        }

        private void NotifyChanged()
        {
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.PlaylistChanged);
        }

        private void TakeOwnership()
        {
            if (!Networking.IsOwner(gameObject) && Networking.LocalPlayer != null)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
        }
    }
}
