using AngryWasp.Helpers;
using AngryWasp.Math;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AngryWasp.Net
{
    public class ConnectionIdJsonConverer : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(ConnectionId);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            ConnectionId hk = (string)reader.Value;
            return hk;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            ConnectionId hk = (ConnectionId)value;
            writer.WriteValue(hk.ToString());
        }
    }

    [JsonConverter(typeof(ConnectionIdJsonConverer))]
    public struct ConnectionId : IReadOnlyList<byte>, IEquatable<ConnectionId>, IEquatable<byte[]>
    {
        private readonly byte[] value;

        public byte this[int index]
        {
            get
            {
                if (this.value != null)
                    return this.value[index];

                return default(byte);
            }
        }

        public const int LENGTH = 20;

        public static readonly ConnectionId Empty = new byte[20];

        public int Count => LENGTH;

        public bool IsNullOrEmpty()
        {
            if (value == null)
                return true;

            if (value.SequenceEqual(Empty))
                return true;

            return false;
        }

        public ConnectionId(byte[] bytes)
        {
            value = bytes;
        }

        public bool Equals(ConnectionId other) => this.SequenceEqual(other);
        public bool Equals(byte[] other) => this.SequenceEqual(other);
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            return obj is ConnectionId && this.Equals((ConnectionId)obj);
        }

        public IEnumerator<byte> GetEnumerator()
        {
            if (this.value != null)
                return ((IList<byte>)this.value).GetEnumerator();

            return Enumerable.Repeat(default(byte), 20).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public override int GetHashCode()
        {
            if (this.value == null)
                return 0;

            int offset = 0;
            return
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset);
        }

        public static bool TryParse(string input, out ConnectionId output)
        {
            output = ConnectionId.Empty;
            if (input == null)
                return false;

            if (input.StartsWith("0x"))
                input = input.Substring(2);

            if (input.Length != 40)
                return false;

            try
            {
                output = new ConnectionId(input.FromByteHex());
                return true;
            }
            catch { return false; }
        }

        public static bool operator ==(ConnectionId left, ConnectionId right) => left.Equals(right);

        public static bool operator !=(ConnectionId left, ConnectionId right) => !left.Equals(right);

        public static bool operator ==(byte[] left, ConnectionId right) => right.Equals(left);

        public static bool operator !=(byte[] left, ConnectionId right) => !right.Equals(left);

        public static bool operator ==(ConnectionId left, byte[] right) => left.Equals(right);

        public static bool operator !=(ConnectionId left, byte[] right) => !left.Equals(right);

        public static implicit operator ConnectionId(byte[] value) => new ConnectionId(value);

        public static implicit operator byte[](ConnectionId value) => value.ToByte();

        public static implicit operator List<byte>(ConnectionId value) => value.ToList();

        public static implicit operator ConnectionId(List<byte> value) => new ConnectionId(value.ToArray());

        public static implicit operator ConnectionId(string hex)
        {
            if (!TryParse(hex, out ConnectionId cid))
                throw new InvalidCastException("Input string is not valid hex");

            return cid;
        }

        public static implicit operator string(ConnectionId value) => value.ToString();

        public override string ToString() => "0x" + value.ToHex();

        public byte[] ToByte() => value;
    }
}