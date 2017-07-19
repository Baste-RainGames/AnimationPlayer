using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AnimTransition))]
public class AnimTransitionDrawer : PropertyDrawer
{

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		var singleLine = EditorGUIUtility.singleLineHeight + 1;
		var isCurve = property.FindPropertyRelative("type").enumValueIndex == (int) TransitionType.Curve;
		return singleLine * (isCurve ? 4f : 3f);
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		var durationProp = property.FindPropertyRelative("duration");
		var typeProp = property.FindPropertyRelative("type");
		var curveProp = property.FindPropertyRelative("curve");

		var lineHeigh = EditorGUIUtility.singleLineHeight;
		var lineIncrease = lineHeigh + 1f;
		position.height = lineHeigh;
		
		EditorGUI.LabelField(position, label);

		position.x += 10;
		position.width -= 10;
		position.y += lineIncrease;
		EditorGUI.PropertyField(position, typeProp);
		
		position.y += lineIncrease;
		EditorGUI.PropertyField(position, durationProp);

		var type = (TransitionType) typeProp.enumValueIndex;
		if (type == TransitionType.Curve)
		{
			position.y += lineIncrease;
			EditorGUI.PropertyField(position, curveProp);
		} 
	}
}
