﻿using UnityEngine;
using UnityEditor;
using System.Collections;
using ProBuilder2.Common;

public class ShowHideFlags : Editor
{
	[MenuItem("Tools/Debug/Show HideFalgs")]
	static void Indot()
	{
		foreach(Transform t in Selection.transforms)
			Debug.Log(t.gameObject.hideFlags);
	}

	[MenuItem("Tools/Debug/Print Snap Settings")]
	public static void dflkajsdkflj()
	{
		string txt = "Snap Enabled: ";
		txt += pb_ProGrids_Interface.SnapEnabled();
		txt += "\nAxis Constraints: " + pb_ProGrids_Interface.UseAxisConstraints();
		txt += "\nSnap Value: " + pb_ProGrids_Interface.SnapValue();

		Debug.Log(txt);
	}

	[MenuItem("Tools/Debug/Show Prefab Info")]
	static void Indoadfadsfadsft()
	{
		foreach(pb_Object pb in Selection.transforms.GetComponents<pb_Object>())
		{
			if(	(PrefabUtility.GetPrefabType(pb.gameObject) == PrefabType.PrefabInstance ||
					 PrefabUtility.GetPrefabType(pb.gameObject) == PrefabType.Prefab ) )
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();

				foreach(PropertyModification pm in PrefabUtility.GetPropertyModifications(pb))
				{
					sb.AppendLine(	(pm.objectReference != null ? pm.objectReference.name : "") + ": " +
									(pm.propertyPath != null ? pm.propertyPath : "") + ", " +
									(pm.target != null ? pm.target.name : ""));
				}

				Debug.Log("Name: " + pb.name + "\n" + sb.ToString());
			}
		}
	}

}