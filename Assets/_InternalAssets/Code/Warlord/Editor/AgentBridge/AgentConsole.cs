using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Warlord.EditorTools.Agent
{
    internal struct AgentLogItem
    {
        public string Message;
        public string File;
        public int Line;
        public string Level;
    }

    /// <summary>Доступ к окну Console редактора через internal-API UnityEditor.LogEntries.</summary>
    internal static class AgentConsole
    {
        // Флаги UnityEditor.ConsoleWindow.Mode, относящиеся к ошибкам и предупреждениям.
        const int ErrorMask = (1 << 0) | (1 << 1) | (1 << 4) | (1 << 6) | (1 << 8) | (1 << 11)
                              | (1 << 13) | (1 << 17) | (1 << 20);
        const int WarningMask = (1 << 7) | (1 << 9) | (1 << 12);

        static bool _init;
        static Type _entries;
        static MethodInfo _start, _end, _getEntry, _clear;
        static object _entryInstance;
        static FieldInfo _fMessage, _fFile, _fLine, _fMode;

        static void Init()
        {
            if (_init) return;
            _init = true;
            try
            {
                var asm = typeof(EditorWindow).Assembly;
                _entries = asm.GetType("UnityEditor.LogEntries");
                var entryType = asm.GetType("UnityEditor.LogEntry");
                if (_entries == null || entryType == null) return;

                const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                _start = _entries.GetMethod("StartGettingEntries", Flags);
                _end = _entries.GetMethod("EndGettingEntries", Flags);
                _getEntry = _entries.GetMethod("GetEntryInternal", Flags);
                _clear = _entries.GetMethod("Clear", Flags);
                _entryInstance = Activator.CreateInstance(entryType);

                const BindingFlags FieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _fMessage = entryType.GetField("message", FieldFlags);
                _fFile = entryType.GetField("file", FieldFlags);
                _fLine = entryType.GetField("line", FieldFlags);
                _fMode = entryType.GetField("mode", FieldFlags);
            }
            catch
            {
                _entries = null;
            }
        }

        public static bool Available
        {
            get
            {
                Init();
                return _entries != null && _start != null && _getEntry != null && _fMessage != null;
            }
        }

        static string LevelOf(int mode)
        {
            if ((mode & ErrorMask) != 0) return "error";
            if ((mode & WarningMask) != 0) return "warning";
            return "log";
        }

        /// <summary>Возвращает последние записи консоли, новые в конце. level: all|error|warning.</summary>
        public static List<AgentLogItem> Read(int max, string level)
        {
            var result = new List<AgentLogItem>();
            if (!Available) return result;

            object countObj = _start.Invoke(null, null);
            int count = countObj is int i ? i : 0;
            try
            {
                var buffer = new List<AgentLogItem>(Math.Min(count, 4096));
                for (int row = 0; row < count; row++)
                {
                    var got = _getEntry.Invoke(null, new[] { (object)row, _entryInstance });
                    if (got is bool ok && !ok) continue;

                    int mode = _fMode != null ? (int)_fMode.GetValue(_entryInstance) : 0;
                    string lvl = LevelOf(mode);
                    if (level == "error" && lvl != "error") continue;
                    if (level == "warning" && lvl == "log") continue;

                    buffer.Add(new AgentLogItem
                    {
                        Message = (string)_fMessage.GetValue(_entryInstance),
                        File = _fFile != null ? (string)_fFile.GetValue(_entryInstance) : null,
                        Line = _fLine != null ? (int)_fLine.GetValue(_entryInstance) : 0,
                        Level = lvl,
                    });
                }

                int skip = Math.Max(0, buffer.Count - max);
                for (int k = skip; k < buffer.Count; k++) result.Add(buffer[k]);
            }
            finally
            {
                _end?.Invoke(null, null);
            }
            return result;
        }

        public static void Counts(out int errors, out int warnings, out int logs)
        {
            errors = warnings = logs = 0;
            if (!Available) return;

            object countObj = _start.Invoke(null, null);
            int count = countObj is int i ? i : 0;
            try
            {
                for (int row = 0; row < count; row++)
                {
                    var got = _getEntry.Invoke(null, new[] { (object)row, _entryInstance });
                    if (got is bool ok && !ok) continue;
                    int mode = _fMode != null ? (int)_fMode.GetValue(_entryInstance) : 0;
                    switch (LevelOf(mode))
                    {
                        case "error": errors++; break;
                        case "warning": warnings++; break;
                        default: logs++; break;
                    }
                }
            }
            finally
            {
                _end?.Invoke(null, null);
            }
        }

        public static bool Clear()
        {
            Init();
            if (_clear == null) return false;
            _clear.Invoke(null, null);
            return true;
        }
    }
}
