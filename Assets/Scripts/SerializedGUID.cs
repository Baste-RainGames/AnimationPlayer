using System;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using UnityEditor;
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
        public static SerializedGUID Create()
        {
            return new SerializedGUID {guid = Guid.NewGuid()};
        }
        
        private Guid guid;
        public Guid GUID => guid;

        [SerializeField]
        private string guidSerialized;
        
        public void OnBeforeSerialize()
        {
            if (guid == Guid.Empty)
                guid = Guid.NewGuid();
            guidSerialized = guid.ToString();
        }

        public void OnAfterDeserialize()
        {
            guid = Guid.ParseExact(guidSerialized, "D");
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
    }
} 