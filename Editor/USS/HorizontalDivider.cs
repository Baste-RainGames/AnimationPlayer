using UnityEngine.UIElements;

namespace Animation_Player {
public class HorizontalDivider : VisualElement
{
    public HorizontalDivider()
    {
        AddToClassList("horizontal-divider");
    }

    public new class UxmlFactory : UxmlFactory<HorizontalDivider> { }
}
}