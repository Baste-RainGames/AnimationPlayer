namespace Animation_Player {
    /// <summary>
    /// Helper struct for identifying an AnimationPlayer state.
    ///
    /// You can just send in a string or int, and let the implicit conversion do the rest of the job. Using this directly might help readability and such.
    ///
    /// Name comparisons are made with StringComparison.InvariantCulture.
    /// </summary>
    public struct StateID {
        public int index;
        public string name;
        public bool isNameBased;

        public static implicit operator StateID(string name) {
            return new StateID {
                isNameBased = true,
                name = name,
                index = -1
            };
        }

        public static implicit operator StateID(int index) {
            return new StateID {
                isNameBased = false,
                index = index,
                name = null,
            };
        }

        public override string ToString() {
            return isNameBased ? $"State {name}" : $"State {index}";
        }
    }

}