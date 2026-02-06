// NDI SDK for Unity is licensed for non-commercial use only.
// By using this script you agree to the NDI SDK non-commercial license terms.

using System;

namespace NDI
{
    [Serializable]
    public readonly struct NDISourceInfo : IEquatable<NDISourceInfo>
    {
        public string Name { get; }
        public string Address { get; }

        public NDISourceInfo(string name, string address)
        {
            Name = name ?? string.Empty;
            Address = address ?? string.Empty;
        }

        public bool Equals(NDISourceInfo other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(Address, other.Address, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is NDISourceInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Address);
        }

        public static bool operator ==(NDISourceInfo left, NDISourceInfo right) => left.Equals(right);
        public static bool operator !=(NDISourceInfo left, NDISourceInfo right) => !left.Equals(right);

        public override string ToString()
        {
            return string.IsNullOrEmpty(Address) ? Name : $"{Name} ({Address})";
        }
    }
}
