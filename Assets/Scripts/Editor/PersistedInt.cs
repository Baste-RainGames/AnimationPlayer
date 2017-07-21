using UnityEditor;

/// <summary>
/// Persisted (to EditorPrefs) int value for edit mode that survives assembly reloads.
/// Use for making editors not reset when you recompile scripts. 
/// 
/// Uses the edited element's instanceID (or any int, but you know) to match value with object.
/// </summary>
public class PersistedInt
{
    private readonly string key;
    private int cachedVal;

    public PersistedInt(string key, int instanceID)
    {
        this.key = key + instanceID;
        cachedVal = Get();
    }

    public void SetTo(int value)
    {
        if (value != cachedVal)
        {
            EditorPrefs.SetInt(key, value);
            cachedVal = value;
        }
    }

    public int Get()
    {
        return EditorPrefs.GetInt(key, 0);
    }

    public static implicit operator int(PersistedInt p)
    {
        return p.Get();
    }
    
}