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
        [UdonSynced] public bool repeatPlaylist;
        [UdonSynced] public bool repeatCurrent;
        [UdonSynced] public bool skipFailedItems = true;
        [UdonSynced] public int playlistRevision;

        public int Count() { return urls == null ? 0 : urls.Length; }
        public bool HasCurrent() { return currentIndex >= 0 && currentIndex < Count(); }
        public bool HasQueued() { return queuedIndex >= 0 && queuedIndex < Count(); }

        public string CurrentTitle()
        {
            if (!HasCurrent() || titles == null || currentIndex >= titles.Length) return "";
            return titles[currentIndex];
        }

        public void Select(int index)
        {
            if (!CanMutate() || urls == null || index < 0 || index >= urls.Length) return;
            TakeOwnership();
            if (currentIndex != index) previousIndex = currentIndex;
            currentIndex = index;
            failedIndex = -1;
            if (queuedIndex == index) queuedIndex = -1;
            playlistRevision++;
            RequestSerialization();
            LoadCurrent();
            NotifyChanged();
        }

        public void QueueNext(int index)
        {
            if (!CanMutate() || urls == null || index < 0 || index >= urls.Length) return;
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

        public void ReportCurrentFailed()
        {
            if (!CanMutate() || !HasCurrent()) return;
            TakeOwnership();
            failedIndex = currentIndex;
            playlistRevision++;
            RequestSerialization();
            NotifyChanged();

            if (skipFailedItems)
            {
                AdvanceAfterFailure();
            }
        }

        public void Next()
        {
            int count = Count();
            if (!CanMutate() || count == 0) return;
            if (repeatCurrent && HasCurrent())
            {
                Select(currentIndex);
                return;
            }

            if (HasQueued())
            {
                int queued = queuedIndex;
                queuedIndex = -1;
                Select(queued);
                return;
            }

            int next = currentIndex < 0 ? 0 : currentIndex + 1;
            if (next >= count)
            {
                if (!repeatPlaylist) return;
                next = 0;
            }
            Select(next);
        }

        public void Previous()
        {
            if (!CanMutate()) return;
            if (previousIndex >= 0 && previousIndex < Count())
            {
                Select(previousIndex);
                return;
            }

            int count = Count();
            if (count == 0) return;
            int previous = currentIndex < 0 ? 0 : currentIndex - 1;
            if (previous < 0)
            {
                if (!repeatPlaylist) return;
                previous = count - 1;
            }
            Select(previous);
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

        public override void OnDeserialization()
        {
            NotifyChanged();
        }

        private void AdvanceAfterFailure()
        {
            int count = Count();
            if (count == 0) return;

            if (HasQueued() && queuedIndex != failedIndex)
            {
                int queued = queuedIndex;
                queuedIndex = -1;
                Select(queued);
                return;
            }

            int next = failedIndex + 1;
            if (next >= count)
            {
                if (!repeatPlaylist) return;
                next = 0;
            }

            if (next != failedIndex)
            {
                Select(next);
            }
        }

        private bool CanMutate() { return access != null && access.IsLocalAuthorized(); }

        private void LoadCurrent()
        {
            if (state == null || !HasCurrent()) return;
            state.mediaUrl = urls[currentIndex];
            state.Commit(Nexora.Api.NexoraPlaybackState.Loading, 0d);
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
