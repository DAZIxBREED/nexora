using UdonSharp;
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
        public VRCUrl[] urls;
        public string[] titles;
        [UdonSynced] public int currentIndex = -1;
        [UdonSynced] public bool repeatPlaylist;

        public void Select(int index)
        {
            if (access == null || !access.IsLocalAuthorized() || urls == null || index < 0 || index >= urls.Length) return;
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
            currentIndex = index;
            RequestSerialization();
            if (state != null)
            {
                state.mediaUrl = urls[index];
                state.Commit(Nexora.Api.NexoraPlaybackState.Loading, 0d);
            }
            if (moduleHost != null) moduleHost.Broadcast(NexoraEvent.PlaylistChanged);
        }

        public void Next()
        {
            if (urls == null || urls.Length == 0) return;
            int next = currentIndex + 1;
            if (next >= urls.Length)
            {
                if (!repeatPlaylist) return;
                next = 0;
            }
            Select(next);
        }
    }
}
