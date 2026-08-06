using UdonSharp;
using UnityEngine;

namespace Nexora.Diagnostics
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraDiagnostics : UdonSharpBehaviour
    {
        public int capacity = 64;
        [HideInInspector] public string[] messages;
        [HideInInspector] public int writeIndex;

        private void Start()
        {
            if (capacity < 8) capacity = 8;
            messages = new string[capacity];
            Log("Diagnostics initialized");
        }

        public void Log(string message)
        {
            if (messages == null) return;
            messages[writeIndex] = message;
            writeIndex++;
            if (writeIndex >= capacity) writeIndex = 0;
            Debug.Log("[Nexora] " + message);
        }
    }
}
