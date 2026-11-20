using System;
using System.Globalization;
using System.Linq;

namespace Optimizzzer.Core
{
    /// <summary>A typed registry value with a stable string form for the journal.</summary>
    public sealed class RegValue : IEquatable<RegValue>
    {
        private RegValue(RegKind kind, object data) { Kind = kind; Data = data; }

        public RegKind Kind { get; }
        /// <summary>uint, ulong, string, string[] or byte[].</summary>
        public object Data { get; }

        public static RegValue DWord(uint v) => new RegValue(RegKind.DWord, v);
        public static RegValue DWord(int v) => new RegValue(RegKind.DWord, unchecked((uint)v));
        public static RegValue QWord(ulong v) => new RegValue(RegKind.QWord, v);
        public static RegValue Str(string v) => new RegValue(RegKind.String, v ?? "");
        public static RegValue Expand(string v) => new RegValue(RegKind.ExpandString, v ?? "");
        public static RegValue Multi(params string[] v) => new RegValue(RegKind.MultiString, v ?? new string[0]);
        public static RegValue Bin(params byte[] v) => new RegValue(RegKind.Binary, v ?? new byte[0]);

        public static RegValue FromObject(RegKind kind, object data)
        {
            switch (kind)
            {
                case RegKind.DWord: return DWord(unchecked((uint)Convert.ToInt64(data, CultureInfo.InvariantCulture)));
                case RegKind.QWord: return QWord(unchecked((ulong)Convert.ToInt64(data, CultureInfo.InvariantCulture)));
                case RegKind.String: return Str(Convert.ToString(data, CultureInfo.InvariantCulture));
                case RegKind.ExpandString: return Expand(Convert.ToString(data, CultureInfo.InvariantCulture));
                case RegKind.MultiString: return Multi((string[])data);
                default: return Bin((byte[])data);
            }
        }

        public string Serialize()
        {
            switch (Kind)
            {
                case RegKind.DWord: return ((uint)Data).ToString(CultureInfo.InvariantCulture);
                case RegKind.QWord: return ((ulong)Data).ToString(CultureInfo.InvariantCulture);
                case RegKind.String:
                case RegKind.ExpandString: return (string)Data;
                case RegKind.MultiString: return string.Join("\n", (string[])Data);
                default: return BitConverter.ToString((byte[])Data).Replace("-", "");
            }
        }

        public static RegValue Deserialize(RegKind kind, string s)
        {
            s = s ?? "";
            switch (kind)
            {
                case RegKind.DWord: return DWord(uint.Parse(s, CultureInfo.InvariantCulture));
                case RegKind.QWord: return QWord(ulong.Parse(s, CultureInfo.InvariantCulture));
                case RegKind.String: return Str(s);
                case RegKind.ExpandString: return Expand(s);
                case RegKind.MultiString: return Multi(s.Length == 0 ? new string[0] : s.Split('\n'));
                default:
                    var bytes = new byte[s.Length / 2];
                    for (int i = 0; i < bytes.Length; i++)
                        bytes[i] = byte.Parse(s.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return Bin(bytes);
            }
        }

        public bool Equals(RegValue other)
        {
            if (other is null) return false;
            bool numeric = (Kind == RegKind.DWord || Kind == RegKind.QWord) && (other.Kind == RegKind.DWord || other.Kind == RegKind.QWord);
            if (numeric) return Convert.ToUInt64(Data) == Convert.ToUInt64(other.Data);
            bool text = (Kind == RegKind.String || Kind == RegKind.ExpandString) && (other.Kind == RegKind.String || other.Kind == RegKind.ExpandString);
            if (text) return string.Equals((string)Data, (string)other.Data, StringComparison.OrdinalIgnoreCase);
            if (Kind != other.Kind) return false;
            if (Kind == RegKind.MultiString) return ((string[])Data).SequenceEqual((string[])other.Data);
            return ((byte[])Data).SequenceEqual((byte[])other.Data);
        }

        public override bool Equals(object obj) => Equals(obj as RegValue);
        public override int GetHashCode() => Serialize().ToLowerInvariant().GetHashCode();

        /// <summary>Human readable, used in dry-run output.</summary>
        public string Display()
        {
            switch (Kind)
            {
                case RegKind.DWord: return Data + " (DWORD)";
                case RegKind.QWord: return Data + " (QWORD)";
                case RegKind.String: return "\"" + Data + "\" (REG_SZ)";
                case RegKind.ExpandString: return "\"" + Data + "\" (REG_EXPAND_SZ)";
                case RegKind.MultiString: return "[" + string.Join(", ", (string[])Data) + "] (REG_MULTI_SZ)";
                default: return Serialize() + " (REG_BINARY)";
            }
        }
    }
}
