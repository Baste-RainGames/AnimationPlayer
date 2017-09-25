using UnityEditor;

namespace Animation_Player
{
    /// <summary>
    /// Persisted (to EditorPrefs) int value for edit mode that survives assembly reloads.
    /// Use for making editors not reset when you recompile scripts. 
    /// 
    /// Uses the edited element's instanceID (or any int, but you know) to match value with object.
    /// </summary>
    public abstract class PersistedVal<T>
    {
        private readonly string key;
        private T cachedVal;

        protected PersistedVal(string key)
        {
            this.key = key;
            cachedVal = Get();
        }

        public void SetTo(T value)
        {
            if (ToInt(value) != ToInt(cachedVal))
            {
                EditorPrefs.SetInt(key, ToInt(value));
                cachedVal = value;
            }
        }

        private T Get()
        {
            return ToType(EditorPrefs.GetInt(key, 0));
        }

        public static implicit operator T(PersistedVal<T> p)
        {
            return p.Get();
        }

        protected abstract int ToInt(T val);

        protected abstract T ToType(int i);

        public override string ToString()
        {
            return cachedVal.ToString();
        }
    }

    public class PersistedInt : PersistedVal<int>
    {
        public PersistedInt(string key) : base(key)
        { }

        protected override int ToInt(int val)
        {
            return val;
        }

        protected override int ToType(int i)
        {
            return i;
        }
    }

    public class PersistedBool : PersistedVal<bool>
    {
        public PersistedBool(string key) : base(key)
        { }

        protected override int ToInt(bool val)
        {
            return val ? 1 : 0;
        }

        protected override bool ToType(int i)
        {
            return i != 0;
        }
    }
}