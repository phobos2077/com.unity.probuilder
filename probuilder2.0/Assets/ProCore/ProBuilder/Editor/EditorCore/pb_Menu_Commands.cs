﻿#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_3_6 || UNITY_3_5_7 || UNITY_3_8
#define UNITY_3
#endif

using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using ProBuilder2.Common;
using ProBuilder2.EditorCommon;
using System.Reflection;
using ProBuilder2.MeshOperations;
using System.Linq;
using ProBuilder2.Math;

#if PB_DEBUG
using Parabox.Debug;
#endif

/**
 * Contains Menu commands for most ProBuilder operations.  Will 
 * also attempt to Update the pb_Editor if possible.
 */
public class pb_Menu_Commands : Editor
{
	private static pb_Editor editor { get { return pb_Editor.instance; } }

#region Object Level
	/**
	 * Combine selected pb_Objects to a single object.
	 * ProBuilder only.
	 */
	#if !PROTOTYPE
	public static void MenuMergeObjects(pb_Object[] selected)	
	{
		if(selected.Length < 2)
		{
			pb_Editor_Utility.ShowNotification("Must Select 2+ Objects");
			return;
		}

		int option = EditorUtility.DisplayDialogComplex(
			"Save or Delete Originals?",
			"Saved originals will be deactivated and hidden from the Scene, but available in the Hierarchy.",
			"Merge Delete",		// 0
			"Merge Save",		// 1
			"Cancel");			// 2

		pb_Object pb = null;

		if(option == 2) return;

		if( pbMeshOps.CombineObjects(selected, out pb) )
		{
			pb_Editor_Utility.SetEntityType(EntityType.Detail, pb.gameObject);
			pb_Lightmap_Editor.SetObjectUnwrapParamsToDefault(pb);			
			pb.gameObject.AddComponent<MeshCollider>().convex = false;
			pb.GenerateUV2(true);

			pb.gameObject.name = "pb-MergedObject" + pb.id;

			switch(option)
			{
				case 0: 	// Delete donor objects
					for(int i = 0; i < selected.Length; i++)
					{
						pbUndo.DestroyImmediate(selected[i].gameObject, "Delete Merged Objects");
					}

					break;

				case 1:
					foreach(pb_Object sel in selected)
						#if UNITY_3
						sel.gameObject.active = false;
						#else
						sel.gameObject.SetActive(false);
						#endif
					break;
			}

			Undo.RegisterCreatedObjectUndo(pb.gameObject, "Merge Objects");

			Selection.activeTransform = pb.transform;
		}

		if(editor)
			editor.UpdateSelection();
	}
	#endif

	/**
	 * Set the pivot to the center of the current element selection.
	 * ProBuilder only.
	 */
	public static void MenuSetPivot(pb_Object[] selection)
	{
		if(selection.Length > 0)
			pb_Editor_Utility.ShowNotification("Set Pivot");
		else
			pb_Editor_Utility.ShowNotification("Nothing Selected");

		Object[] objects = new Object[selection.Length * 2];
		System.Array.Copy(selection, 0, objects, 0, selection.Length);
		for(int i = selection.Length; i < objects.Length; i++)
			objects[i] = selection[i-selection.Length].transform;

		pbUndo.RecordObjects(objects, "Set Pivot");

		foreach (pb_Object pb in selection)
		{
			if (pb.SelectedTriangles.Length > 0)
				pb.CenterPivot(pb.SelectedTriangles);
			else
				pb.CenterPivot(null);
		}
		
		SceneView.RepaintAll();

		if(editor != null)
			editor.UpdateSelection();
	}	

	/**
	 * Set the pb_Entity entityType on selection.
	 */
	public static void MenuSetEntityType(pb_Object[] selection, EntityType entityType)
	{
		if(selection.Length < 1)
		{
			pb_Editor_Utility.ShowNotification("Nothing Selected");
			return;
		}
		
		Object[] undoObjects = new Object[selection.Length * 3];

		int len = selection.Length;

		System.Array.Copy(selection, 0, undoObjects, 0, len);

		for(int i = 0; i < len; i++)
		{
			undoObjects[len + i] = selection[i].gameObject;
			undoObjects[len*2 + i] = selection[i].transform.GetComponent<pb_Entity>();
		}

		pbUndo.RecordObjects(undoObjects, "Set Entity Type");

		foreach(pb_Object pb in selection)
			pb_Editor_Utility.SetEntityType(entityType, pb.gameObject);

		pb_Editor_Utility.ShowNotification("Set " + entityType);
	}

	// /**
	//  * Union operation between two ProBuilder objects.
	//  */
	// public static void MenuUnion(pb_Object[] selection)
	// {
	// 	pb_Object[] sel = pbUtil.GetComponents<pb_Object>(Selection.transforms);

	// 	if(sel.Length < 2)
	// 	{
	// 		pb_Editor_Utility.ShowNotification("Must Select 2 Objects");	
	// 		return;
	// 	}

	// 	Mesh c = Parabox.CSG.CSG.Union(sel[0].gameObject, sel[1].gameObject);

	// 	GameObject go = new GameObject();

	// 	go.AddComponent<MeshRenderer>().sharedMaterial = pb_Constant.DefaultMaterial;
	// 	go.AddComponent<MeshFilter>().sharedMesh = c;

	// 	pb_Editor_Utility.ShowNotification("Union");
	// }

	// public static void MenuSubtract(pb_Object[] selection)
	// {
	// 	pb_Object[] sel = pbUtil.GetComponents<pb_Object>(Selection.transforms);

	// 	if(sel.Length < 2)
	// 	{
	// 		pb_Editor_Utility.ShowNotification("Must Select 2 Objects");	
	// 		return;
	// 	}

	// 	Mesh c = Parabox.CSG.CSG.Subtract(sel[1].gameObject, sel[0].gameObject);

	// 	GameObject go = new GameObject();

	// 	go.AddComponent<MeshRenderer>().sharedMaterial = pb_Constant.DefaultMaterial;
	// 	go.AddComponent<MeshFilter>().sharedMesh = c;

	// 	pb_Editor_Utility.ShowNotification("Subtract");
	// }

	// public static void MenuIntersect(pb_Object[] selection)
	// {
	// 	pb_Object[] sel = pbUtil.GetComponents<pb_Object>(Selection.transforms);

	// 	if(sel.Length < 2)
	// 	{
	// 		pb_Editor_Utility.ShowNotification("Must Select 2 Objects");	
	// 		return;
	// 	}

	// 	Mesh c = Parabox.CSG.CSG.Intersect(sel[0].gameObject, sel[1].gameObject);

	// 	GameObject go = new GameObject();

	// 	go.AddComponent<MeshRenderer>().sharedMaterial = pb_Constant.DefaultMaterial;
	// 	go.AddComponent<MeshFilter>().sharedMesh = c;

	// 	pb_Editor_Utility.ShowNotification("Intersect");
	// }

#endregion

#region Normals

	/**
	 * Flips all face normals if editLevel == EditLevel.Top, else flips only pb_Object->SelectedFaces
	 */
	public static void MenuFlipNormals(pb_Object[] selected)
	{
		pbUndo.RecordObjects(selected, "Flip Face Normals.");

		foreach(pb_Object pb in selected)
			pb.ReverseWindingOrder(pb.SelectedFaces.Length < 1 ? pb.faces : pb.SelectedFaces);

		if(selected.Length > 0)
			pb_Editor_Utility.ShowNotification("Flip Normals");
		else
			pb_Editor_Utility.ShowNotification("Nothing Selected");
	}
#endregion

#region Extrude / Bridge

	public static void ExtrudeButtonGUI()
	{
		float extrudeAmount = EditorPrefs.HasKey(pb_Constant.pbExtrudeDistance) ? EditorPrefs.GetFloat(pb_Constant.pbExtrudeDistance) : .5f;
		
		EditorGUI.BeginChangeCheck();
			GUILayout.BeginHorizontal();
				GUILayout.Label("Dist", GUILayout.MaxWidth(36));
				Rect al = GUILayoutUtility.GetLastRect();
				extrudeAmount = EditorGUI.FloatField(new Rect(al.x + al.width, al.y, 38, al.height+2), "", extrudeAmount);
			GUILayout.EndHorizontal();

		if(EditorGUI.EndChangeCheck())
			EditorPrefs.SetFloat(pb_Constant.pbExtrudeDistance, extrudeAmount);
	}

	/**
	 * Infers the correct context and extrudes the selected elements.
	 */
	public static void MenuExtrude(pb_Object[] selection)
	{
		pb_Object[] pbs = pbUtil.GetComponents<pb_Object>(Selection.transforms);

		pbUndo.RecordObjects(pbUtil.GetComponents<pb_Object>(Selection.transforms), "Extrude selected.");

		int extrudedFaceCount = 0;
		bool success = false;

		foreach(pb_Object pb in pbs)
		{
			if(editor && editor.selectionMode == SelectMode.Edge)
			{
				if(pb.SelectedEdges.Length < 1 || pb.SelectedFaceCount > 0)
				{
					success = false;
				}
				else
				{
					extrudedFaceCount += pb.SelectedEdges.Length;
					pb_Edge[] newEdges;
					
					success = pb.Extrude(pb.SelectedEdges, pb_Preferences_Internal.GetFloat(pb_Constant.pbExtrudeDistance), pb_Preferences_Internal.GetBool(pb_Constant.pbManifoldEdgeExtrusion), out newEdges);
	
					if(success)
						pb.SetSelectedEdges(newEdges);
					else
						extrudedFaceCount -= pb.SelectedEdges.Length;
				}

			}

			if(!success)
			{
				if(pb.SelectedFaces.Length < 1)
					continue;
				
				extrudedFaceCount += pb.SelectedFaces.Length;

				pb.Extrude(pb.SelectedFaces, pb_Preferences_Internal.GetFloat(pb_Constant.pbExtrudeDistance));
				
				pb.SetSelectedFaces(pb.SelectedFaces);
			}

			pb.GenerateUV2(pb_Editor.show_NoDraw);
			pb.Refresh();
		}

		if(extrudedFaceCount > 0)
			pb_Editor_Utility.ShowNotification("Extrude");// + val, "Extrudes the selected faces / edges.");

		if(editor != null)
			editor.UpdateSelection();

		SceneView.RepaintAll();
	}

	/**
	 * Create a face between two edges.
	 */
	#if !PROTOTYPE
	public static void MenuBridgeEdges(pb_Object[] selection)
	{
		pbUndo.RecordObjects(selection, "Bridge Edges");

		bool success = false;
		bool limitToPerimeterEdges = pb_Preferences_Internal.GetBool(pb_Constant.pbPerimeterEdgeBridgeOnly);
	
		foreach(pb_Object pb in selection)
		{
			if(pb.SelectedEdges.Length == 2)
			{
				if(pb.Bridge(pb.SelectedEdges[0], pb.SelectedEdges[1], limitToPerimeterEdges))
				{
					success = true;
					pb.GenerateUV2(pb_Editor.show_NoDraw);
					pb.Refresh();
				}
			}
		}

		if(success)
		{
			pb_Editor.instance.UpdateSelection();
			pb_Editor_Utility.ShowNotification("Bridge Edges");
		}
		else
		{
			Debug.LogWarning("Failed Bridge Edges.  Bridge Edges requires that only 2 edges be selected, and they must both only have one connecting face (non-manifold).");
		}
		
		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}
	#endif
#endregion

#region Selection

	/**
	 * Grow selection to plane using max angle diff.
	 */
	public static void MenuGrowSelection(pb_Object[] selection)
	{
		pbUndo.RecordObjects(selection, "Grow Selection");

		int grown = 0;

		foreach(pb_Object pb in pbUtil.GetComponents<pb_Object>(Selection.transforms))
		{
			int sel = pb.SelectedTriangleCount;

			switch( editor != null ? editor.selectionMode : (SelectMode)0 )
			{
				case SelectMode.Vertex:
					pb.SetSelectedEdges(pbMeshUtils.GetConnectedEdges(pb, pb.SelectedTriangles));
					break;

				case SelectMode.Edge:
					pb.SetSelectedEdges(pbMeshUtils.GetConnectedEdges(pb, pb.SelectedTriangles));
					break;
				
				case SelectMode.Face:
					
					if( pb_Preferences_Internal.GetBool(pb_Constant.pbGrowSelectionUsingAngle) )
					{
						List<pb_Face> newFaceSelection = new List<pb_Face>( pb.SelectedFaces );

						foreach(pb_Face f in pb.SelectedFaces)
						{
							Vector3 nrm = pb_Math.Normal( pb.GetVertices(f.indices) );

							List<pb_Face> adjacent = pbMeshUtils.GetNeighborFaces(pb, f);

							foreach(pb_Face connectedFace in adjacent)
							{
								float angle = Vector3.Angle( nrm, pb_Math.Normal( pb.GetVertices(connectedFace.indices)) );

								if( angle < pb_Preferences_Internal.GetFloat("pbGrowSelectionAngle") )
									newFaceSelection.Add(connectedFace);
							}
						}

						pb.SetSelectedFaces(newFaceSelection.Distinct().ToArray());
					}
					else
					{
						pb_Face[] all = pbMeshUtils.GetNeighborFaces(pb, pb.SelectedFaces);
						pb.SetSelectedFaces(all);
					}

					break;
			}

			grown += pb.SelectedTriangleCount - sel;
		}

		if(editor != null)
			editor.UpdateSelection();

		pb_Editor_Utility.ShowNotification(grown > 0 ? "Grow Selection" : "Nothing to Grow");

		SceneView.RepaintAll();
	}

	public static void GrowSelectionGUI()
	{
		bool angleGrow = pb_Preferences_Internal.GetBool(pb_Constant.pbGrowSelectionUsingAngle);

		EditorGUI.BeginChangeCheck();

		GUILayout.Label("Angle", GUILayout.MaxWidth(36));
		
		Rect al = GUILayoutUtility.GetLastRect();
		angleGrow = EditorGUI.Toggle(new Rect(al.x + al.width + 18, al.y-1, 36, al.height), "", angleGrow);

		float angleVal = pb_Preferences_Internal.GetFloat(pb_Constant.pbGrowSelectionAngle);

		bool te = GUI.enabled;

		GUI.enabled = angleGrow;

		GUILayout.BeginHorizontal();
			GUILayout.Label("Max", GUILayout.MaxWidth(36));
			al = GUILayoutUtility.GetLastRect();
			angleVal = EditorGUI.FloatField(new Rect(al.x + al.width, al.y, 36, al.height+2), "", angleVal);
		GUILayout.EndHorizontal();

		GUI.enabled = te;

		if( EditorGUI.EndChangeCheck() )
		{
			EditorPrefs.SetBool(pb_Constant.pbGrowSelectionUsingAngle, angleGrow);
			EditorPrefs.SetFloat(pb_Constant.pbGrowSelectionAngle, angleVal);
		}
	}

	/**
	 * Shrink selection.
	 * Note - requires a reference to an open pb_Editor be passed.  This is because shrink
	 * vertices requires access to the Selected_Universal_Edges_All array.
	 */
	public static void MenuShrinkSelection(pb_Object[] selection)
	{
		if(editor == null)
		{
			pb_Editor_Utility.ShowNotification("ProBuilder Editor Not Open!");
			return;
		}

		// find perimeter edges
		int[] perimeter = null;
		int rc = 0;
		for(int i = 0; i < selection.Length; i++)
		{
			pb_Object pb = selection[i];

			switch(editor.selectionMode)
			{
				case SelectMode.Edge:
				{
					perimeter = pbMeshUtils.GetPerimeterEdges(pb, pb.SelectedEdges);		
					pb.SetSelectedEdges( pb.SelectedEdges.RemoveAt(perimeter) );
					break;
				}

				case SelectMode.Face:
				{
					perimeter = pbMeshUtils.GetPerimeterFaces(pb, pb.SelectedFaces);
					pb.SetSelectedFaces( pb.SelectedFaces.RemoveAt(perimeter) );
					break;
				}

				case SelectMode.Vertex:
				{
					perimeter = pbMeshUtils.GetPerimeterVertices(pb, pb.SelectedTriangles, editor.Selected_Universal_Edges_All[i]);
					pb.SetSelectedTriangles( pb.SelectedTriangles.RemoveAt(perimeter) );
					break;
				}
			}

			rc += perimeter != null ? perimeter.Length : 0;
		}

		if(selection.Length > 0 && rc > 0)
			pb_Editor_Utility.ShowNotification("Shrink Selection");
		else
			pb_Editor_Utility.ShowNotification("Unable to Shrink");

		if(editor)
			editor.UpdateSelection();

		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}

	/**
	 * Invert the current selection.
	 */
	public static void MenuInvertSelection(pb_Object[] selection)
	{
		pbUndo.RecordObjects(selection, "Invert Selection");

		switch( editor != null ? editor.selectionMode : (SelectMode)0 )
		{
			case SelectMode.Vertex:
				foreach(pb_Object pb in selection)
				{
					pb_IntArray[] sharedIndices = pb.sharedIndices;
					List<int> selSharedIndices = new List<int>();

					foreach(int i in pb.SelectedTriangles)
						selSharedIndices.Add( sharedIndices.IndexOf(i) );				

					List<int> inverse = new List<int>();

					for(int i = 0; i < sharedIndices.Length; i++)
					{
						if(!selSharedIndices.Contains(i))
							inverse.Add(sharedIndices[i][0]);
					}

					pb.SetSelectedTriangles(inverse.ToArray());
				}
				break;

			case SelectMode.Face:
				foreach(pb_Object pb in selection)
				{
					List<pb_Face> inverse = new List<pb_Face>();

					for(int i = 0; i < pb.faces.Length; i++)
						if( System.Array.IndexOf(pb.SelectedFaceIndices, i) < 0 )
							inverse.Add(pb.faces[i]);
					
					pb.SetSelectedFaces(inverse.ToArray());	
				}
				break;

			case SelectMode.Edge:
				
				if(!editor) break;

				for(int i = 0; i < selection.Length; i++)
				{
					pb_Edge[] universal_selected_edges = pb_Edge.GetUniversalEdges(selection[i].SelectedEdges, selection[i].sharedIndices).Distinct().ToArray();
					pb_Edge[] inverse_universal = System.Array.FindAll(editor.Selected_Universal_Edges_All[i], x => !universal_selected_edges.Contains(x));
					pb_Edge[] inverse = new pb_Edge[inverse_universal.Length];
					
					for(int n = 0; n < inverse_universal.Length; n++)
						inverse[n] = new pb_Edge( selection[i].sharedIndices[inverse_universal[n].x][0], selection[i].sharedIndices[inverse_universal[n].y][0] );

					selection[i].SetSelectedEdges(inverse);
				}
				break;
		}

		if(editor)
			editor.UpdateSelection();
		
		pb_Editor_Utility.ShowNotification("Invert Selection");

		SceneView.RepaintAll();
	}

	/**
	 * Expands the current selection using a "Ring" method.
	 */
	public static void MenuRingSelection(pb_Object[] selection)
	{
		pbUndo.RecordObjects(selection, "Select Edge Ring");

		#if PB_DEBUG
		pb_Profiler profiler = new pb_Profiler();
		profiler.BeginSample("MenuRingSelection");
		#endif

		bool success = false;

		foreach(pb_Object pb in pbUtil.GetComponents<pb_Object>(Selection.transforms))
		{
			pb_Edge[] edges = pbMeshUtils.GetEdgeRing(pb, pb.SelectedEdges);

			if(edges.Length > pb.SelectedEdges.Length)
				success = true;

			pb.SetSelectedEdges( edges );
		}

		#if PB_DEBUG
		profiler.EndSample();
		Bugger.Log( profiler.ToString() );
		#endif

		if(editor)
			editor.UpdateSelection();

		pb_Editor_Utility.ShowNotification(success ? "Select Edge Ring" : "Nothing to Ring");

		SceneView.RepaintAll();
	}
		
	/**
	 * Selects an Edge loop. Todo - support for face loops.
	 */
	public static void MenuLoopSelection(pb_Object[] selection)
	{
		pbUndo.RecordObjects(selection, "Select Edge Loop");

		bool foundLoop = false;

		foreach(pb_Object pb in selection)
		{
			pb_Edge[] loop;
			bool success = pbMeshUtils.GetEdgeLoop(pb, pb.SelectedEdges, out loop);
			if(success)
			{
				if(loop.Length > pb.SelectedEdges.Length)
					foundLoop = true;

				pb.SetSelectedEdges(loop);
			}
		}

		if(editor)
			editor.UpdateSelection();

		pb_Editor_Utility.ShowNotification(foundLoop ? "Select Edge Loop" : "Nothing to Loop");
		// Internal_UpdateSelectionFast();

		SceneView.RepaintAll();
	}
#endregion

#region Delete / Detach

	#if !PROTOTYPE

	/**
	 * Delete selected faces.
	 * ProBuilder only.
	 */
	public static void MenuDeleteFace(pb_Object[] selection)
	{
		pbUndo.RecordObjects(selection as Object[], "Delete Face(s)");

		foreach(pb_Object pb in selection)
		{
			pb.DeleteFaces(pb.SelectedFaces);
			pb.GenerateUV2(true);
			pb.Refresh();
		}

		if(editor)
		{
			editor.ClearFaceSelection();
			editor.UpdateSelection();
		}
		
		pb_Editor_Utility.ShowNotification("Delete Face");

		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}

	public static void MenuDetachFacesContext(pb_Object[] selection)
	{
		int option = EditorUtility.DisplayDialogComplex(
			"Rules of Detachment",
			"Detach face selection to submesh or new ProBuilder object?",
			"New Object",		// 0
			"Submesh",			// 1
			"Cancel");			// 2

		switch(option)
		{
			case 0:
				MenuDetachFacesToObject(selection);
				break;
			case 1:
				MenuDetachFacesToSubmesh(selection);
				break;
			case 2:
				break;
		}
	}

	/**
	 * Detach selected faces to submesh.
	 * ProBuilder only.
	 */
	public static void MenuDetachFacesToSubmesh(pb_Object[] selection)
	{
		pbUndo.RecordObjects(selection, "Detach Face(s)");

		foreach(pb_Object pb in selection)
		{
			foreach(pb_Face face in pb.SelectedFaces)
				pb.DetachFace(face);

			pb.GenerateUV2(true);
			pb.Refresh();
			
			pb.SetSelectedFaces(pb.SelectedFaces);
		}

		if(editor)
			editor.UpdateSelection();

		pb_Editor_Utility.ShowNotification("Detach Face");

		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}

	/**
	 * Detaches currently selected faces to a new ProBuilder object.
	 * ProBuilder only.
	 */
	public static void MenuDetachFacesToObject(pb_Object[] selection)
	{
		if(!editor) return;

		pbUndo.RecordObjects(selection, "Detach Selection to PBO");

		int detachedFaceCount = 0;

		foreach(pb_Object pb in selection)
		{
			if(pb.SelectedFaceIndices.Length < 1 || pb.SelectedFaceIndices.Length == pb.faces.Length) continue;

			int[] primary = pb.SelectedFaceIndices;
			
			detachedFaceCount += primary.Length;

			List<int> inverse_list = new List<int>();
			for(int i = 0; i < pb.faces.Length; i++)
				if(System.Array.IndexOf(primary, i) < 0)
					inverse_list.Add(i);
					
			int[] inverse = inverse_list.ToArray();
		
			pb_Object copy = pb_Object.InitWithObject(pb);
			Undo.RegisterCreatedObjectUndo(copy.gameObject, "Detach Face");

			copy.transform.position = pb.transform.position;
			copy.transform.localScale = pb.transform.localScale;
			copy.transform.localRotation = pb.transform.localRotation;

			pb.DeleteFaces(primary);
			copy.DeleteFaces(inverse);

			pb.GenerateUV2(pb_Editor.show_NoDraw);
			copy.GenerateUV2(pb_Editor.show_NoDraw);

			pb.Refresh();
			copy.Refresh();

			pb.ClearSelection();
			copy.ClearSelection();
		
			copy.gameObject.name = pb.gameObject.name + "-detach";
		}
	
		if(editor)
			editor.UpdateSelection();

		if(detachedFaceCount > 0)
			pb_Editor_Utility.ShowNotification("Detach " + detachedFaceCount + " faces to new Object");
		else
			Debug.LogWarning("No faces selected! Please select some faces and try again.");

		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}

	#endif
#endregion

#region Vertex Operations

	#if !PROTOTYPE

	/**
	 * Collapse selected vertices
	 * ProBuilder only.
	 */
	public static void MenuCollapseVertices(pb_Object[] selection)
	{
		bool success = false;
		
		pbUndo.RecordObjects(selection, "Collapse Vertices");
		
		foreach(pb_Object pb in selection)
		{
			if(pb.SelectedTriangles.Length > 1)
			{

				int newIndex = -1;
				success = pb.MergeVertices(pb.SelectedTriangles, out newIndex);
					
				if(success)
				{
					int[] removed;
					pb.RemoveDegenerateTriangles(out removed);
					pb.SetSelectedTriangles(new int[] { newIndex });
				}
				
				pb.GenerateUV2(true);
				pb.Refresh();
			}
		}

		if(success)
			pb_Editor_Utility.ShowNotification("Collapse Vertices");

		if(editor)
			editor.UpdateSelection();

		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}

	/**
	 * Weld all selected vertices.
	 * ProBuilder only.
	 */
	public static void MenuWeldVertices(pb_Object[] selection)
	{
		bool success = false;

		pbUndo.RecordObjects(selection, "Weld Vertices");
		float weld = pb_Preferences_Internal.GetFloat(pb_Constant.pbWeldDistance);

		foreach(pb_Object pb in selection)
		{
			if(pb.SelectedTriangles.Length > 1)
			{
				int[] welds;
				success = pb.WeldVertices(pb.SelectedTriangles, weld, out welds);

				int[] removed;
				if( pb.RemoveDegenerateTriangles(out removed) )
				{
					welds = new int[0];	// @todo
				}

				if(success)
				{
					pb.SetSelectedTriangles(welds);

					pb.GenerateUV2(pb_Editor.show_NoDraw);

					pb.msh.vertices = pb.vertices;	// no need for a full ToMesh() call, just set the vertices.
					pb.Refresh();
				}
			}
		}

		if(success)
			pb_Editor_Utility.ShowNotification("Weld Vertices");

		if(editor)
			editor.UpdateSelection(true);

		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}

	const float MIN_WELD_DISTANCE = .0001f;

	/**
	 * Expose the distance parameter used in Weld operations.
	 * ProBuilder only.
	 */
	public static void WeldButtonGUI()
	{
		EditorGUI.BeginChangeCheck();

		float weldDistance = pb_Preferences_Internal.GetFloat(pb_Constant.pbWeldDistance);
		
		if(weldDistance <= MIN_WELD_DISTANCE)
			weldDistance = MIN_WELD_DISTANCE;

		GUILayout.BeginHorizontal();
			GUILayout.Label(new GUIContent("Max", "The maximum distance between two vertices in order to be welded together."), GUILayout.MaxWidth(36));
			Rect al = GUILayoutUtility.GetLastRect();
			weldDistance = EditorGUI.FloatField(new Rect(al.x + al.width, al.y, 36, al.height+2), "", weldDistance);
		GUILayout.EndHorizontal();

		if( EditorGUI.EndChangeCheck() )
		{
			if(weldDistance < MIN_WELD_DISTANCE)
				weldDistance = MIN_WELD_DISTANCE;
			EditorPrefs.SetFloat(pb_Constant.pbWeldDistance, weldDistance);
		}
	}

	/**
	 * Split selected vertices from shared vertices.
	 * ProBuilder only.
	 */
	public static void MenuSplitVertices(pb_Object[] selection)
	{
		int splitCount = 0;
		pbUndo.RecordObjects(selection, "Split Vertices");

		foreach(pb_Object pb in selection)
		{
			List<int> tris = new List<int>(pb.SelectedTriangles);			// loose verts to split

			if(pb.SelectedFaces.Length > 0)
			{
				pb_IntArray[] sharedIndices = pb.sharedIndices;

				int[] selTrisIndices = new int[pb.SelectedTriangles.Length];

				// Get sharedIndices index for each vert in selection
				for(int i = 0; i < pb.SelectedTriangles.Length; i++)
					selTrisIndices[i] = sharedIndices.IndexOf(pb.SelectedTriangles[i]);

				// cycle through selected faces and remove the tris that compose full faces.
				foreach(pb_Face face in pb.SelectedFaces)
				{
					List<int> faceSharedIndices = new List<int>();

					for(int j = 0; j < face.distinctIndices.Length; j++)
						faceSharedIndices.Add( sharedIndices.IndexOf(face.distinctIndices[j]) );

					List<int> usedTris = new List<int>();
					for(int i = 0; i < selTrisIndices.Length; i++)	
						if( faceSharedIndices.Contains(selTrisIndices[i]) )
							usedTris.Add(pb.SelectedTriangles[i]);

					// This face *is* composed of selected tris.  Remove these tris from the loose index list
					foreach(int i in usedTris)	
						if(tris.Contains(i))
							tris.Remove(i);
				}
			}

			// Now split the faces, and any loose vertices
			foreach(pb_Face f in pb.SelectedFaces)
				pb.DetachFace(f);

			splitCount += pb.SelectedTriangles.Length;
			pb.SplitVertices(pb.SelectedTriangles);
	
			// Reattach detached face vertices (if any are to be had)
			if(pb.SelectedFaces.Length > 0)
			{
				int[] welds;
				pb.WeldVertices( pb_Face.AllTriangles(pb.SelectedFaces), Mathf.Epsilon, out welds);
			}
	
			// And set the selected triangles to the newly split
			List<int> newTriSelection = new List<int>(pb_Face.AllTriangles(pb.SelectedFaces));
			newTriSelection.AddRange(tris);
			pb.SetSelectedTriangles(newTriSelection.ToArray());
			
			pb.GenerateUV2(true);
			pb.Refresh();
		}

		pb_Editor_Utility.ShowNotification("Split " + splitCount + (splitCount > 1 ? " Vertices" : " Vertex"));

		if(editor)
			editor.UpdateSelection();
		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}

	#endif
#endregion

#region Subdivide / Split

	#if !PROTOTYPE

	/**
	 * Attempts to subdivide the selected objects.  If Edge or Face selection mode, splits at the 
	 * center of the edge.  Otherwise from Vertex.
	 * ProBuilder only.
	 */
	public static void MenuSubdivide(pb_Object[] selection)
	{
		if(selection.Length < 1)
		{
			pb_Editor_Utility.ShowNotification("Nothing Selected");
			return;
		}

		pbUndo.RecordObjects(selection, "Subdivide Selection");
		int success = 0;

		foreach(pb_Object pb in selection)
		{
			if( pbSubdivideSplit.Subdivide(pb) )
				success++;

			pb.GenerateUV2(pb_Editor.show_NoDraw);
			pb.Refresh();
		}

		/*
			// Vertex subdivision
			foreach(pb_Object pb in selection)
			{
				List<VertexConnection> vertexConnections = new List<VertexConnection>();

				foreach(pb_Face f in pb.faces)
					vertexConnections.Add( new VertexConnection(f, new List<int>(f.distinctIndices) ) );

				pb_Face[] faces;

				if(pb.ConnectVertices(vertexConnections, out faces))	
				{
					pb.SetSelectedFaces(faces);
					success++;

					pb.GenerateUV2(pb_Editor.show_NoDraw);
					pb.Refresh();
				}
			}
		h*/

		pb_Editor_Utility.ShowNotification("Subdivide Object");

		if(editor)
			editor.UpdateSelection();
	}

	/**
	 * Subdivides all currently selected faces.
	 * ProBuilder only.
	 */
	public static void MenuSubdivideFace(pb_Object[] selection)
	{
		int success = 0;

		foreach(pb_Object pb in selection)
		{
			pbUndo.RecordObject(pb, "Subdivide Face");
			
			pb_Face[] faces;

			if(pb.SubdivideFace(pb.SelectedFaces, out faces))
			{
				success += pb.SelectedFaces.Length;
				pb.SetSelectedFaces(faces);
				pb.GenerateUV2(true);
				pb.Refresh();
			}
		}

		if(success > 0)
		{
	        pb_Editor_Utility.ShowNotification("Subdivide " + success + ((success > 1) ? " faces" : " face"));
			
			if(editor)
				editor.UpdateSelection();
		}
		else
		{
			Debug.LogWarning("Subdivide faces failed - did you not have any faces selected?");
		}

		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}

	/**
	 * Connects all currently selected edges.
	 * ProBuilder only.
	 */
	public static void MenuConnectEdges(pb_Object[] selection)
	{
		pbUndo.RecordObjects(selection, "Connect Edges");
		int success = 0;

		foreach(pb_Object pb in selection)
		{
			pb_Edge[] edges;
			if(pb.ConnectEdges(pb.SelectedEdges, out edges))
			{
				pb.SetSelectedEdges(edges);

				pb.GenerateUV2(pb_Editor.show_NoDraw);
				pb.Refresh();
				
				success++;
			}
		}

		if(success > 0)
		{
			if(editor)
				editor.UpdateSelection();

			pb_Editor_Utility.ShowNotification("Connect Edges");
		}
		else
		{
			Debug.LogWarning("No valid split paths found.  This is most likely because you are attempting to split edges that do belong to the same face, or do not have more than one edge selected.");
		}
	}

	/**
	 * Connects all currently selected vertices.
	 * ProBuilder only.
	 */
	public static void MenuConnectVertices(pb_Object[] selection)
	{
		int success = 0;

		pbUndo.RecordObjects(selection, "Connect Vertices");
		
		foreach(pb_Object pb in selection)
		{
			int[] selectedTriangles = pb.SelectedTriangles.Distinct().ToArray();
			int len = selectedTriangles.Length;

			List<VertexConnection> splits = new List<VertexConnection>();
			List<pb_Face>[] connectedFaces = new List<pb_Face>[len];

			// For each vertex, get all it's connected faces
			for(int i = 0; i < len; i++)
				connectedFaces[i] = pbMeshUtils.GetNeighborFaces(pb, selectedTriangles[i]);

			for(int i = 0; i < len; i++)
			{
				foreach(pb_Face face in connectedFaces[i])
				{
					int index = splits.IndexOf((VertexConnection)face);	// VertexConnection only compares face property
					if(index < 0)
						splits.Add( new VertexConnection(face, new List<int>(1) { selectedTriangles[i] } ) );
					else
						splits[index].indices.Add(selectedTriangles[i]);
				}
			}

			for(int i = 0; i < splits.Count; i++)
				splits[i] = splits[i].Distinct(pb.sharedIndices);

			int[] f;
			if(pb.ConnectVertices(splits, out f))
			{
				success++;
				pb.SetSelectedTriangles(f);
			}
		}
		
		foreach(pb_Object pb in selection)
		{
			pb.GenerateUV2(true);
			pb.Refresh();
		}

		if(success > 0)
		{
			pb_Editor_Utility.ShowNotification("Connect Vertices");
			if(editor)
				editor.UpdateSelection();
		}
		else
		{
			Debug.LogWarning("No valid split paths found.  This is most likely because you are attempting to split vertices that do not belong to the same face.  This is not currently supported, sorry!");
		}
	}

	/**
	 * Inserts an edge loop along currently selected Edges.
	 * ProBuilder only.
	 */
	public static void MenuInsertEdgeLoop(pb_Object[] selection)
	{
		int success = 0;
		pbUndo.RecordObjects(selection, "Insert Edge Loop");

		foreach(pb_Object pb in selection)
		{
			pb_Edge[] edges;
			if( pb.ConnectEdges( pbMeshUtils.GetEdgeRing(pb, pb.SelectedEdges), out edges) )
			{
				pb.SetSelectedEdges(edges);
				pb.GenerateUV2(true);
				pb.Refresh();
				success++;
			}
		}

		if(success > 0)
		{
			pb_Editor_Utility.ShowNotification("Insert Edge Loop");
		}

		if(editor)
			editor.UpdateSelection();

		EditorWindow.FocusWindowIfItsOpen(typeof(SceneView));
	}

	#endif
#endregion
}
