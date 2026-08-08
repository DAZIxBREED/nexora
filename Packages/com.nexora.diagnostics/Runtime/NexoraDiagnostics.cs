using UdonSharp;
using UnityEngine;

namespace Nexora.Diagnostics
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class NexoraDiagnostics : UdonSharpBehaviour
    {
        public const byte Info = 0;
        public const byte Warning = 1;
        public const byte Error = 2;

        [Header("History")]
        public int capacity = 128;
        public bool mirrorToUnityConsole = true;

        [HideInInspector] public string[] messages;
        [HideInInspector] public string[] sources;
        [HideInInspector] public byte[] severities;
        [HideInInspector] public float[] localTimes;
        [HideInInspector] public int[] sequenceNumbers;
        [HideInInspector] public int writeIndex;
        [HideInInspector] public int count;
        [HideInInspector] public int nextSequence = 1;

        [Header("Lifetime counters")]
        [HideInInspector] public int infoCount;
        [HideInInspector] public int warningCount;
        [HideInInspector] public int errorCount;
        [HideInInspector] public int droppedCount;
        [HideInInspector] public float lastWriteTime;

        private void Start()
        {
            InitializeBuffers();
            LogFrom("Diagnostics", "Diagnostics initialized");
        }

        public void InitializeBuffers()
        {
            if (capacity < 8) capacity = 8;
            messages = new string[capacity];
            sources = new string[capacity];
            severities = new byte[capacity];
            localTimes = new float[capacity];
            sequenceNumbers = new int[capacity];
            writeIndex = 0;
            count = 0;
        }

        public void Log(string message) { Write(Info, "Nexora", message); }
        public void Warn(string message) { Write(Warning, "Nexora", message); }
        public void Fail(string message) { Write(Error, "Nexora", message); }

        public void LogFrom(string source, string message) { Write(Info, source, message); }
        public void WarnFrom(string source, string message) { Write(Warning, source, message); }
        public void FailFrom(string source, string message) { Write(Error, source, message); }

        public string NewestMessage() { return MessageByAge(0); }
        public string NewestSource() { return SourceByAge(0); }
        public byte NewestSeverity() { return SeverityByAge(0); }

        public string MessageByAge(int age)
        {
            int index = IndexByAge(age);
            return index < 0 ? "" : messages[index];
        }

        public string SourceByAge(int age)
        {
            int index = IndexByAge(age);
            return index < 0 ? "" : sources[index];
        }

        public byte SeverityByAge(int age)
        {
            int index = IndexByAge(age);
            return index < 0 ? Info : severities[index];
        }

        public float TimeByAge(int age)
        {
            int index = IndexByAge(age);
            return index < 0 ? 0f : localTimes[index];
        }

        public int SequenceByAge(int age)
        {
            int index = IndexByAge(age);
            return index < 0 ? 0 : sequenceNumbers[index];
        }

        public int CurrentStoredWarnings()
        {
            return CountSeverity(Warning);
        }

        public int CurrentStoredErrors()
        {
            return CountSeverity(Error);
        }

        public string BuildRecentSummary(int maximumEntries)
        {
            if (messages == null || count == 0) return "";
            int limit = maximumEntries;
            if (limit < 1) limit = 1;
            if (limit > count) limit = count;

            string summary = "";
            int age = limit - 1;
            while (age >= 0)
            {
                int index = IndexByAge(age);
                if (index >= 0)
                {
                    string severity = severities[index] == Error ? "ERROR" : (severities[index] == Warning ? "WARN" : "INFO");
                    summary += "#" + sequenceNumbers[index] + " [" + severity + "] [" + sources[index] + "] " + messages[index];
                    if (age > 0) summary += "\n";
                }
                age--;
            }
            return summary;
        }

        public void Clear()
        {
            if (messages == null)
            {
                InitializeBuffers();
                return;
            }

            int i = 0;
            while (i < messages.Length)
            {
                messages[i] = "";
                sources[i] = "";
                severities[i] = Info;
                localTimes[i] = 0f;
                sequenceNumbers[i] = 0;
                i++;
            }
            writeIndex = 0;
            count = 0;
        }

        public void ResetLifetimeCounters()
        {
            infoCount = 0;
            warningCount = 0;
            errorCount = 0;
            droppedCount = 0;
        }

        private void Write(byte severity, string source, string message)
        {
            if (messages == null || messages.Length != capacity)
            {
                InitializeBuffers();
            }

            if (string.IsNullOrEmpty(message))
            {
                droppedCount++;
                return;
            }

            if (string.IsNullOrEmpty(source)) source = "Nexora";

            messages[writeIndex] = message;
            sources[writeIndex] = source;
            severities[writeIndex] = severity;
            localTimes[writeIndex] = Time.realtimeSinceStartup;
            sequenceNumbers[writeIndex] = nextSequence;
            nextSequence++;
            lastWriteTime = Time.realtimeSinceStartup;

            if (severity == Error) errorCount++;
            else if (severity == Warning) warningCount++;
            else infoCount++;

            writeIndex++;
            if (writeIndex >= capacity) writeIndex = 0;
            if (count < capacity) count++;

            if (!mirrorToUnityConsole) return;
            string formatted = "[Nexora][" + source + "] " + message;
            if (severity == Error) Debug.LogError(formatted);
            else if (severity == Warning) Debug.LogWarning(formatted);
            else Debug.Log(formatted);
        }

        private int IndexByAge(int age)
        {
            if (messages == null || age < 0 || age >= count) return -1;
            int index = writeIndex - 1 - age;
            while (index < 0) index += capacity;
            return index;
        }

        private int CountSeverity(byte severity)
        {
            if (messages == null) return 0;
            int result = 0;
            int age = 0;
            while (age < count)
            {
                int index = IndexByAge(age);
                if (index >= 0 && severities[index] == severity) result++;
                age++;
            }
            return result;
        }
    }
}
