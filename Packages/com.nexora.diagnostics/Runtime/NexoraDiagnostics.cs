using UdonSharp;
using UnityEngine;

namespace Nexora.Diagnostics
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraDiagnostics : UdonSharpBehaviour
    {
        public int capacity = 64;
        [HideInInspector] public string[] messages;
        [HideInInspector] public byte[] severities;
        [HideInInspector] public float[] localTimes;
        [HideInInspector] public int writeIndex;
        [HideInInspector] public int count;
        [HideInInspector] public int infoCount;
        [HideInInspector] public int warningCount;
        [HideInInspector] public int errorCount;

        public const byte Info = 0;
        public const byte Warning = 1;
        public const byte Error = 2;

        private void Start()
        {
            if (capacity < 8) capacity = 8;
            messages = new string[capacity];
            severities = new byte[capacity];
            localTimes = new float[capacity];
            Log("Diagnostics initialized");
        }

        public void Log(string message)
        {
            Write(Info, message);
        }

        public void Warn(string message)
        {
            Write(Warning, message);
        }

        public void Fail(string message)
        {
            Write(Error, message);
        }

        public string NewestMessage()
        {
            if (count == 0 || messages == null) return "";
            int index = writeIndex - 1;
            if (index < 0) index = capacity - 1;
            return messages[index];
        }

        public void Clear()
        {
            if (messages == null) return;
            int i = 0;
            while (i < messages.Length)
            {
                messages[i] = "";
                severities[i] = Info;
                localTimes[i] = 0f;
                i++;
            }
            writeIndex = 0;
            count = 0;
            infoCount = 0;
            warningCount = 0;
            errorCount = 0;
        }

        private void Write(byte severity, string message)
        {
            if (messages == null || string.IsNullOrEmpty(message)) return;

            messages[writeIndex] = message;
            severities[writeIndex] = severity;
            localTimes[writeIndex] = Time.realtimeSinceStartup;

            if (severity == Error) errorCount++;
            else if (severity == Warning) warningCount++;
            else infoCount++;

            writeIndex++;
            if (writeIndex >= capacity) writeIndex = 0;
            if (count < capacity) count++;

            if (severity == Error) Debug.LogError("[Nexora] " + message);
            else if (severity == Warning) Debug.LogWarning("[Nexora] " + message);
            else Debug.Log("[Nexora] " + message);
        }
    }
}
