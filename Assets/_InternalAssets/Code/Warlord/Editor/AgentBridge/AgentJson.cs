using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Warlord.EditorTools.Agent
{
    /// <summary>Минимальный потоковый JSON-писатель: без аллокаций на боксинге и без зависимостей.</summary>
    internal sealed class AgentJson
    {
        readonly StringBuilder _sb = new StringBuilder(4096);
        readonly List<bool> _first = new List<bool>();

        void Pre()
        {
            if (_first.Count == 0) return;
            int i = _first.Count - 1;
            if (!_first[i]) _sb.Append(',');
            _first[i] = false;
        }

        void Head(string key)
        {
            Pre();
            if (key == null) return;
            Escape(key);
            _sb.Append(':');
        }

        public AgentJson BeginObj(string key = null) { Head(key); _sb.Append('{'); _first.Add(true); return this; }
        public AgentJson EndObj() { _sb.Append('}'); _first.RemoveAt(_first.Count - 1); return this; }
        public AgentJson BeginArr(string key = null) { Head(key); _sb.Append('['); _first.Add(true); return this; }
        public AgentJson EndArr() { _sb.Append(']'); _first.RemoveAt(_first.Count - 1); return this; }

        public AgentJson Str(string key, string value)
        {
            Head(key);
            if (value == null) _sb.Append("null"); else Escape(value);
            return this;
        }

        public AgentJson Num(string key, double value)
        {
            Head(key);
            _sb.Append(value.ToString("0.####", CultureInfo.InvariantCulture));
            return this;
        }

        public AgentJson Bool(string key, bool value)
        {
            Head(key);
            _sb.Append(value ? "true" : "false");
            return this;
        }

        void Escape(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < ' ') _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }

        public override string ToString() => _sb.ToString();
    }
}
