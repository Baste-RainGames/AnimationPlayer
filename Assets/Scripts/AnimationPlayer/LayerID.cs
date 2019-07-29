namespace Animation_Player
{
    /// <summary>
    /// Helper struct for identifying an AnimationPlayer layer.
    ///
    /// Exactly the same layout as StateID, but using a different struct name so you can be explicit about what you're sending around the identifier of.
    /// The default value gives index = 0, name = null and isNameBased = false, which is equivalent to the default layer in the player.
    /// </summary>
    public struct LayerID
    {
        public int    index;
        public string name;
        public bool   isNameBased;

        public static implicit operator LayerID(string name)
        {
            return new LayerID
            {
                isNameBased = true,
                name        = name,
                index       = -1
            };
        }

        public static implicit operator LayerID(int index)
        {
            return new LayerID
            {
                isNameBased = false,
                index       = index,
                name        = null,
            };
        }

        public override string ToString()
        {
            return isNameBased ? $"Layer {name}" : $"Layer {index}";
        }
    }
}