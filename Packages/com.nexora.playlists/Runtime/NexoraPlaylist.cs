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

        [Header("Catalog")]
        public VRCUrl[] urls;
        public string[] titles;
        public int[] playlistStarts;
        public int[] playlistCounts;
        public string[] playlistNames;

        [Header("Capacities")]
        public int queueCapacity = 32;
        public int requestCapacity = 24;
        public int historyCapacity = 24;
        public int quarantineCapacity = 24;

        [Header("Synchronized playlist state")]
        [UdonSynced] public int activePlaylistIndex;
        [UdonSynced] public int currentIndex = -1;
        [UdonSynced] public bool repeatPlaylist;
        [UdonSynced] public bool repeatCurrent;
        [UdonSynced] public bool shuffleEnabled;
        [UdonSynced] public int shuffleSeed = 1;
        [UdonSynced] public bool skipFailedItems = true;
        [UdonSynced] public int playlistRevision;

        [Header("Synchronized queue")]
        [UdonSynced] public int[] queueEntries = new int[0];
        [UdonSynced] public int queueCount;

        [Header("Synchronized requests")]
        [UdonSynced] public int[] requestEntries = new int[0];
        [UdonSynced] public int[] requestPlayerIds = new int[0];
        [UdonSynced] public int requestCount;

        [Header("Synchronized history")]
        [UdonSynced] public int[] historyEntries = new int[0];
        [UdonSynced] public int historyCount;

        [Header("Synchronized quarantine")]
        [UdonSynced] public int[] quarantinedEntries = new int[0];
        [UdonSynced] public byte[] quarantineFailures = new byte[0];
        [UdonSynced] public int quarantineCount;

        [Header("Failure policy")]
        public int failuresBeforeQuarantine = 2;
        public int maximumConsecutiveFailures = 5;
        public bool stopWhenFailureBudgetExhausted = true;

        [UdonSynced] public int consecutiveFailureCount;
        [UdonSynced] public int totalFailureCount;
        [UdonSynced] public int automaticSkipCount;

        [Header("Local telemetry")]
        [HideInInspector] public int queueMutationCount;
        [HideInInspector] public int requestMutationCount;
        [HideInInspector] public int historyMutationCount;
        [HideInInspector] public int lateJoinReconstructionCount;
        [HideInInspector] public int invalidStateRepairCount;

        private int lastAppliedRevision = -1;
        private int shuffleState = 1;

        private void Start()
        {
            EnsureStorage();
            RepairState();
            ApplyReconstructedState();
        }

        public int CatalogCount()
        {
            return urls == null ? 0 : urls.Length;
        }

        public int PlaylistCount()
        {
            if (playlistStarts == null || playlistCounts == null) return 0;
            return Mathf.Min(playlistStarts.Length, playlistCounts.Length);
        }

        public int ActivePlaylistStart()
        {
            if (!IsValidPlaylist(activePlaylistIndex)) return 0;
            return Mathf.Clamp(playlistStarts[activePlaylistIndex], 0, CatalogCount());
        }

        public int ActivePlaylistCount()
        {
            if (!IsValidPlaylist(activePlaylistIndex)) return CatalogCount();
            int start = ActivePlaylistStart();
            return Mathf.Clamp(playlistCounts[activePlaylistIndex], 0, CatalogCount() - start);
        }

        public string ActivePlaylistName()
        {
            if (playlistNames == null || activePlaylistIndex < 0 || activePlaylistIndex >= playlistNames.Length) return "";
            return playlistNames[activePlaylistIndex];
        }

        public bool HasCurrent() { return IsValidCatalogIndex(currentIndex); }
        public string CurrentTitle() { return TitleAt(currentIndex); }

        public string TitleAt(int index)
        {
            if (!IsValidCatalogIndex(index) || titles == null || index >= titles.Length) return "";
            return titles[index];
        }

        public void SelectPlaylist(int playlistIndex)
        {
            if (!CanMutate("select-playlist") || !IsValidPlaylist(playlistIndex)) return;
            TakeOwnership();
            activePlaylistIndex = playlistIndex;
            ClearQueueInternal();
            ClearHistoryInternal();
            consecutiveFailureCount = 0;
            int first = FirstPlayableInActivePlaylist();
            if (first >= 0) SelectInternal(first, false);
            else CommitMutation();
        }

        public void Select(int index)
        {
            if (!CanMutate("select") || !IsPlayable(index)) return;
            TakeOwnership();
            SelectInternal(index, false);
        }

        public void Enqueue(int index)
        {
            if (!CanMutate("queue-add") || !IsPlayable(index)) return;
            EnsureStorage();
            if (queueCount >= queueEntries.Length) return;
            TakeOwnership();
            queueEntries[queueCount++] = index;
            queueMutationCount++;
            CommitMutation();
        }

        public void QueueNext(int index)
        {
            if (!CanMutate("queue-next") || !IsPlayable(index)) return;
            EnsureStorage();
            TakeOwnership();
            if (queueCount >= queueEntries.Length) return;
            int i = queueCount;
            while (i > 0)
            {
                queueEntries[i] = queueEntries[i - 1];
                i--;
            }
            queueEntries[0] = index;
            queueCount++;
            queueMutationCount++;
            CommitMutation();
        }

        public void RemoveQueueAt(int queueIndex)
        {
            if (!CanMutate("queue-remove") || queueIndex < 0 || queueIndex >= queueCount) return;
            TakeOwnership();
            RemoveQueueAtInternal(queueIndex);
            queueMutationCount++;
            CommitMutation();
        }

        public void MoveQueueItem(int fromIndex, int toIndex)
        {
            if (!CanMutate("queue-reorder")) return;
            if (fromIndex < 0 || fromIndex >= queueCount || toIndex < 0 || toIndex >= queueCount || fromIndex == toIndex) return;
            TakeOwnership();
            int value = queueEntries[fromIndex];
            if (fromIndex < toIndex)
            {
                int i = fromIndex;
                while (i < toIndex) { queueEntries[i] = queueEntries[i + 1]; i++; }
            }
            else
            {
                int i = fromIndex;
                while (i > toIndex) { queueEntries[i] = queueEntries[i - 1]; i--; }
            }
            queueEntries[toIndex] = value;
            queueMutationCount++;
            CommitMutation();
        }

        public void ClearQueue()
        {
            if (!CanMutate("queue-clear")) return;
            TakeOwnership();
            ClearQueueInternal();
            queueMutationCount++;
            CommitMutation();
        }

        public void SubmitRequest(int index)
        {
            if (!CanRequest() || !IsPlayable(index)) return;
            EnsureStorage();
            if (requestCount >= requestEntries.Length || RequestExists(index)) return;
            TakeOwnership();
            VRCPlayerApi local = Networking.LocalPlayer;
            requestEntries[requestCount] = index;
            requestPlayerIds[requestCount] = local == null ? -1 : local.playerId;
            requestCount++;
            requestMutationCount++;
            CommitMutation();
        }

        public void ApproveRequestAt(int requestIndex)
        {
            if (!CanMutate("request-approve") || requestIndex < 0 || requestIndex >= requestCount) return;
            TakeOwnership();
            int entry = requestEntries[requestIndex];
            RemoveRequestAtInternal(requestIndex);
            if (IsPlayable(entry) && queueCount < queueEntries.Length) queueEntries[queueCount++] = entry;
            requestMutationCount++;
            queueMutationCount++;
            CommitMutation();
        }

        public void RejectRequestAt(int requestIndex)
        {
            if (!CanMutate("request-reject") || requestIndex < 0 || requestIndex >= requestCount) return;
            TakeOwnership();
            RemoveRequestAtInternal(requestIndex);
            requestMutationCount++;
            CommitMutation();
        }

        public void Next()
        {
            if (!CanMutate("next")) return;
            TakeOwnership();
            int target = -1;

            if (repeatCurrent && HasCurrent()) target = currentIndex;
            else if (queueCount > 0)
            {
                target = queueEntries[0];
                RemoveQueueAtInternal(0);
                queueMutationCount++;
            }
            else if (shuffleEnabled) target = NextShuffleIndex();
            else target = NextSequentialIndex(currentIndex);

            if (target >= 0) SelectInternal(target, false);
            else if (state != null) state.Stop();
        }

        public void Previous()
        {
            if (!CanMutate("previous") || historyCount <= 0) return;
            TakeOwnership();
            int target = historyEntries[historyCount - 1];
            historyCount--;
            historyEntries[historyCount] = -1;
            historyMutationCount++;
            if (IsPlayable(target)) SelectInternal(target, true);
            else CommitMutation();
        }

        public void SetRepeatPlaylist(bool value)
        {
            if (!CanMutate("repeat-playlist")) return;
            TakeOwnership();
            repeatPlaylist = value;
            CommitMutation();
        }

        public void SetRepeatCurrent(bool value)
        {
            if (!CanMutate("repeat-current")) return;
            TakeOwnership();
            repeatCurrent = value;
            CommitMutation();
        }

        public void SetShuffle(bool value)
        {
            if (!CanMutate("shuffle")) return;
            TakeOwnership();
            shuffleEnabled = value;
            if (value)
            {
                int playerId = Networking.LocalPlayer == null ? 1 : Networking.LocalPlayer.playerId + 1;
                shuffleSeed = Mathf.Abs((playlistRevision + 1) * 1103515245 + playerId * 12345);
                if (shuffleSeed == 0) shuffleSeed = 1;
            }
            CommitMutation();
        }

        public void ReportCurrentSucceeded()
        {
            if (!CanMutate("report-success") || !HasCurrent()) return;
            TakeOwnership();
            consecutiveFailureCount = 0;
            RemoveQuarantine(currentIndex);
            CommitMutation();
        }

        public void ReportCurrentFailed()
        {
            if (!CanMutate("report-failure") || !HasCurrent()) return;
            TakeOwnership();
            consecutiveFailureCount++;
            totalFailureCount++;
            RegisterFailure(currentIndex);

            if (maximumConsecutiveFailures > 0 && consecutiveFailureCount >= maximumConsecutiveFailures)
            {
                CommitMutation();
                if (stopWhenFailureBudgetExhausted && state != null) state.Stop();
                return;
            }

            CommitMutation();
            if (skipFailedItems)
            {
                automaticSkipCount++;
                Next();
            }
        }

        public void ClearQuarantine()
        {
            if (!CanMutate("quarantine-clear")) return;
            TakeOwnership();
            int i = 0;
            while (i < quarantineCount)
            {
                quarantinedEntries[i] = -1;
                quarantineFailures[i] = 0;
                i++;
            }
            quarantineCount = 0;
            consecutiveFailureCount = 0;
            CommitMutation();
        }

        public override void OnDeserialization()
        {
            EnsureStorage();
            RepairState();
            ApplyReconstructedState();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (Networking.IsOwner(gameObject)) RequestSerialization();
        }

        private void ApplyReconstructedState()
        {
            if (playlistRevision == lastAppliedRevision) return;
            lastAppliedRevision = playlistRevision;
            lateJoinReconstructionCount++;
            shuffleState = shuffleSeed == 0 ? 1 : shuffleSeed;
            NotifyChanged();
        }

        private void SelectInternal(int index, bool fromHistory)
        {
            if (!IsPlayable(index)) return;
            if (!fromHistory && HasCurrent() && currentIndex != index) PushHistory(currentIndex);
            currentIndex = index;
            consecutiveFailureCount = 0;
            CommitMutation();
            if (state != null) state.LoadMedia(urls[index]);
        }

        private void PushHistory(int index)
        {
            if (!IsValidCatalogIndex(index) || historyEntries.Length == 0) return;
            if (historyCount >= historyEntries.Length)
            {
                int i = 1;
                while (i < historyEntries.Length) { historyEntries[i - 1] = historyEntries[i]; i++; }
                historyCount = historyEntries.Length - 1;
            }
            historyEntries[historyCount++] = index;
            historyMutationCount++;
        }

        private int NextSequentialIndex(int fromIndex)
        {
            int start = ActivePlaylistStart();
            int count = ActivePlaylistCount();
            if (count <= 0) return -1;
            int end = start + count;
            int candidate = fromIndex < start || fromIndex >= end ? start : fromIndex + 1;
            int attempts = 0;
            while (attempts < count)
            {
                if (candidate >= end)
                {
                    if (!repeatPlaylist) return -1;
                    candidate = start;
                }
                if (IsPlayable(candidate)) return candidate;
                candidate++;
                attempts++;
            }
            return -1;
        }

        private int NextShuffleIndex()
        {
            int start = ActivePlaylistStart();
            int count = ActivePlaylistCount();
            if (count <= 0) return -1;
            int attempts = 0;
            while (attempts < count * 2)
            {
                shuffleState = NextRandom(shuffleState);
                int candidate = start + (Mathf.Abs(shuffleState) % count);
                if (IsPlayable(candidate) && (count == 1 || candidate != currentIndex)) return candidate;
                attempts++;
            }
            return NextSequentialIndex(currentIndex);
        }

        private int NextRandom(int value)
        {
            unchecked
            {
                int next = value * 1103515245 + 12345;
                return next == 0 ? 1 : next;
            }
        }

        private void RegisterFailure(int index)
        {
            EnsureStorage();
            int slot = FindQuarantineSlot(index);
            if (slot < 0)
            {
                if (quarantineCount >= quarantinedEntries.Length) return;
                slot = quarantineCount++;
                quarantinedEntries[slot] = index;
                quarantineFailures[slot] = 0;
            }
            if (quarantineFailures[slot] < byte.MaxValue) quarantineFailures[slot]++;
        }

        private void RemoveQuarantine(int index)
        {
            int slot = FindQuarantineSlot(index);
            if (slot < 0) return;
            int i = slot + 1;
            while (i < quarantineCount)
            {
                quarantinedEntries[i - 1] = quarantinedEntries[i];
                quarantineFailures[i - 1] = quarantineFailures[i];
                i++;
            }
            quarantineCount--;
            if (quarantineCount >= 0 && quarantineCount < quarantinedEntries.Length)
            {
                quarantinedEntries[quarantineCount] = -1;
                quarantineFailures[quarantineCount] = 0;
            }
        }

        private bool IsQuarantined(int index)
        {
            int slot = FindQuarantineSlot(index);
            return slot >= 0 && quarantineFailures[slot] >= Mathf.Max(1, failuresBeforeQuarantine);
        }

        private int FindQuarantineSlot(int index)
        {
            int i = 0;
            while (i < quarantineCount)
            {
                if (quarantinedEntries[i] == index) return i;
                i++;
            }
            return -1;
        }

        private bool RequestExists(int index)
        {
            int i = 0;
            while (i < requestCount)
            {
                if (requestEntries[i] == index) return true;
                i++;
            }
            return false;
        }

        private void RemoveRequestAtInternal(int index)
        {
            int i = index + 1;
            while (i < requestCount)
            {
                requestEntries[i - 1] = requestEntries[i];
                requestPlayerIds[i - 1] = requestPlayerIds[i];
                i++;
            }
            requestCount--;
            requestEntries[requestCount] = -1;
            requestPlayerIds[requestCount] = -1;
        }

        private void RemoveQueueAtInternal(int index)
        {
            int i = index + 1;
            while (i < queueCount)
            {
                queueEntries[i - 1] = queueEntries[i];
                i++;
            }
            queueCount--;
            queueEntries[queueCount] = -1;
        }

        private void ClearQueueInternal()
        {
            int i = 0;
            while (i < queueCount) { queueEntries[i] = -1; i++; }
            queueCount = 0;
        }

        private void ClearHistoryInternal()
        {
            int i = 0;
            while (i < historyCount) { historyEntries[i] = -1; i++; }
            historyCount = 0;
        }

        private int FirstPlayableInActivePlaylist()
        {
            int start = ActivePlaylistStart();
            int count = ActivePlaylistCount();
            int i = 0;
            while (i < count)
            {
                int index = start + i;
                if (IsPlayable(index)) return index;
                i++;
            }
            return -1;
        }

        private bool IsPlayable(int index)
        {
            if (!IsValidCatalogIndex(index) || IsQuarantined(index)) return false;
            if (!IsValidPlaylist(activePlaylistIndex)) return true;
            int start = ActivePlaylistStart();
            return index >= start && index < start + ActivePlaylistCount();
        }

        private bool IsValidCatalogIndex(int index)
        {
            return urls != null && index >= 0 && index < urls.Length && !VRCUrl.IsNullOrEmpty(urls[index]);
        }

        private bool IsValidPlaylist(int index)
        {
            return index >= 0 && index < PlaylistCount();
        }

        private bool CanMutate(string action)
        {
            return access != null && access.AuthorizePlaylist(action);
        }

        private bool CanRequest()
        {
            if (access == null) return false;
            return access.AuthorizePlaylist("request-submit") || access.AuthorizeControl("request-submit");
        }

        private void EnsureStorage()
        {
            queueCapacity = Mathf.Clamp(queueCapacity, 1, 128);
            requestCapacity = Mathf.Clamp(requestCapacity, 1, 128);
            historyCapacity = Mathf.Clamp(historyCapacity, 1, 128);
            quarantineCapacity = Mathf.Clamp(quarantineCapacity, 1, 128);

            if (queueEntries == null || queueEntries.Length != queueCapacity) queueEntries = ResizeIntArray(queueEntries, queueCapacity, queueCount);
            if (requestEntries == null || requestEntries.Length != requestCapacity) requestEntries = ResizeIntArray(requestEntries, requestCapacity, requestCount);
            if (requestPlayerIds == null || requestPlayerIds.Length != requestCapacity) requestPlayerIds = ResizeIntArray(requestPlayerIds, requestCapacity, requestCount);
            if (historyEntries == null || historyEntries.Length != historyCapacity) historyEntries = ResizeIntArray(historyEntries, historyCapacity, historyCount);
            if (quarantinedEntries == null || quarantinedEntries.Length != quarantineCapacity) quarantinedEntries = ResizeIntArray(quarantinedEntries, quarantineCapacity, quarantineCount);
            if (quarantineFailures == null || quarantineFailures.Length != quarantineCapacity) quarantineFailures = ResizeByteArray(quarantineFailures, quarantineCapacity, quarantineCount);

            queueCount = Mathf.Clamp(queueCount, 0, queueEntries.Length);
            requestCount = Mathf.Clamp(requestCount, 0, requestEntries.Length);
            historyCount = Mathf.Clamp(historyCount, 0, historyEntries.Length);
            quarantineCount = Mathf.Clamp(quarantineCount, 0, quarantinedEntries.Length);
        }

        private int[] ResizeIntArray(int[] source, int size, int used)
        {
            int[] result = new int[size];
            int i = 0;
            while (i < size) { result[i] = -1; i++; }
            if (source == null) return result;
            int copy = Mathf.Min(Mathf.Min(source.Length, size), used);
            i = 0;
            while (i < copy) { result[i] = source[i]; i++; }
            return result;
        }

        private byte[] ResizeByteArray(byte[] source, int size, int used)
        {
            byte[] result = new byte[size];
            if (source == null) return result;
            int copy = Mathf.Min(Mathf.Min(source.Length, size), used);
            int i = 0;
            while (i < copy) { result[i] = source[i]; i++; }
            return result;
        }

        private void RepairState()
        {
            EnsureStorage();
            bool repaired = false;
            if (PlaylistCount() > 0 && !IsValidPlaylist(activePlaylistIndex)) { activePlaylistIndex = 0; repaired = true; }
            if (currentIndex >= 0 && !IsValidCatalogIndex(currentIndex)) { currentIndex = -1; repaired = true; }
            if (queueCount > queueEntries.Length) { queueCount = queueEntries.Length; repaired = true; }
            if (requestCount > requestEntries.Length) { requestCount = requestEntries.Length; repaired = true; }
            if (historyCount > historyEntries.Length) { historyCount = historyEntries.Length; repaired = true; }
            if (quarantineCount > quarantinedEntries.Length) { quarantineCount = quarantinedEntries.Length; repaired = true; }
            if (repaired) invalidStateRepairCount++;
        }

        private void CommitMutation()
        {
            playlistRevision++;
            RequestSerialization();
            lastAppliedRevision = playlistRevision;
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.PlaylistChanged);
        }

        private void TakeOwnership()
        {
            if (!Networking.IsOwner(gameObject) && Networking.LocalPlayer != null)
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
    }
}
