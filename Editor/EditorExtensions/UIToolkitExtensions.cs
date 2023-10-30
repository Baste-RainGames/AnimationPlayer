using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Animation_Player
{
public static class UIToolkitExtensions
{
    public static void Replace(this VisualElement parentElement, VisualElement toReplace, VisualElement replaceWith)
    {
        Assert.IsNotNull(parentElement, nameof(parentElement));
        Assert.IsNotNull(toReplace,     nameof(toReplace));
        Assert.IsNotNull(replaceWith,   nameof(replaceWith));

        var index = parentElement.IndexOf(toReplace);

        Assert.AreNotEqual(-1, index, $"{nameof(toReplace)} replace is not a child of {nameof(parentElement)}");
        Assert.AreEqual(-1, parentElement.IndexOf(replaceWith), $"{nameof(replaceWith)} is already child of {nameof(parentElement)}");

        parentElement.Remove(toReplace);
        parentElement.Insert(index, replaceWith);
    }

    public static void SetDisplayed(this VisualElement visualElement, bool displayed)
    {
        visualElement.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public static bool IsDisplayed(this VisualElement visualElement) => visualElement.style.display != DisplayStyle.None;

    public static VisualElement WithClass(this VisualElement visualElement, string @class)
    {
        visualElement.AddToClassList(@class);
        return visualElement;
    }

    public static VisualElement WithName(this VisualElement visualElement, string name)
    {
        visualElement.name = name;
        return visualElement;
    }

    public static VisualElement WithChild(this VisualElement visualElement, VisualElement child)
    {
        visualElement.Add(child);
        return visualElement;
    }
}
}