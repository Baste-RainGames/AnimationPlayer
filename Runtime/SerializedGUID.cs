using System;
using UnityEngine;

namespace Animation_Player
{
    /// <summary>
    /// This becomes redundant if this ever goes out of review:
    /// https://issuetracker.unity3d.com/issues/serialization-system-dot-guid-can-not-be-serialized
    /// </summary>
    [Serializable]
    public struct SerializedGUID : ISerializationCallbackReceiver, IFormattable, IComparable, IComparable<SerializedGUID>, IEquatable<SerializedGUID>
    {
        public static SerializedGUID Empty => new SerializedGUID {guid = Guid.Empty};

        public static SerializedGUID Create()
        {
            var newGuid = new SerializedGUID {guid = Guid.NewGuid()};
            newGuid.guidSerialized = newGuid.guid.ToString();
            return newGuid;
        }

        private Guid guid;
        public Guid GUID => guid;

#pragma warning disable 0649
        [SerializeField]
        private string guidSerialized;
#pragma warning restore 0649

        public void OnBeforeSerialize()
        {
            if (guid == Guid.Empty)
                guid = Guid.NewGuid();

            // Assumes that the guid never changes.
            if (string.IsNullOrEmpty(guidSerialized))
                guidSerialized = guid.ToString();
        }

        public void OnAfterDeserialize()
        {
            try
            {
                guid = Guid.ParseExact(guidSerialized, "D");
            }
            catch (FormatException)
            {
                if (string.IsNullOrEmpty(guidSerialized))
                {
                    throw new UnityException("SerializedGUID has an empty string for the serialized guid when deserializing!");
                }

                Debug.LogError("Got a format exception on the guid: " + guidSerialized);
                throw;
            }

            if (guid == Guid.Empty)
                guid = Guid.NewGuid();
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return guid.ToString(format, formatProvider);
        }

        public int CompareTo(object obj)
        {
            return guid.CompareTo(obj);
        }

        public int CompareTo(SerializedGUID other)
        {
            return guid.CompareTo(other.guid);
        }

        public bool Equals(SerializedGUID other)
        {
            return guid.Equals(other.guid);
        }

        public static bool operator ==(SerializedGUID a, SerializedGUID b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(SerializedGUID a, SerializedGUID b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SerializedGUID && Equals((SerializedGUID) obj);
        }

        public override int GetHashCode()
        {
            return guid.GetHashCode();
        }
    }
}