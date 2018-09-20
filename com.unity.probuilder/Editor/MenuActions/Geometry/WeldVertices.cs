using UnityEngine;
using System.Linq;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace UnityEditor.ProBuilder.Actions
{
	sealed class WeldVertices : MenuAction
	{
		Pref<float> m_WeldDistance = new Pref<float>("WeldVertices.weldDistance", .01f);
		static readonly GUIContent gc_weldDistance = new GUIContent("Weld Distance", "The maximum distance between two vertices in order to be welded together.");
		const float k_MinWeldDistance = .00001f;

		public override ToolbarGroup group
		{
			get { return ToolbarGroup.Geometry; }
		}

		public override Texture2D icon
		{
			get { return IconUtility.GetIcon("Toolbar/Vert_Weld", IconSkin.Pro); }
		}

		public override TooltipContent tooltip
		{
			get { return s_Tooltip; }
		}

		static readonly TooltipContent s_Tooltip = new TooltipContent
		(
			"Weld Vertices",
			@"Searches the current selection for vertices that are within the specified distance of on another and merges them into a single vertex.",
			keyCommandAlt, 'V'
		);

		public override SelectMode validSelectModes
		{
			get { return SelectMode.Vertex; }
		}

		public override bool enabled
		{
			get { return base.enabled && MeshSelection.selectedSharedVertexCountObjectMax > 1; }
		}

		protected override MenuActionState optionsMenuState
		{
			get { return MenuActionState.VisibleAndEnabled; }
		}

		protected override void OnSettingsGUI()
		{
			GUILayout.Label("Weld Settings", EditorStyles.boldLabel);

			EditorGUI.BeginChangeCheck();

			m_WeldDistance.value = EditorGUILayout.FloatField(gc_weldDistance, m_WeldDistance);

			if (EditorGUI.EndChangeCheck())
			{
				if (m_WeldDistance < k_MinWeldDistance)
					m_WeldDistance.value = k_MinWeldDistance;
				Settings.Save();
			}

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Weld Vertices"))
				DoAction();
		}

		public override ActionResult DoAction()
		{
			var selection = MeshSelection.TopInternal();

			if(selection == null || selection.Length < 1)
				return ActionResult.NoSelection;

			ActionResult res = ActionResult.NoSelection;

			UndoUtility.RegisterCompleteObjectUndo(selection, "Weld Vertices");

			int weldCount = 0;

			foreach(ProBuilderMesh mesh in selection)
			{
				weldCount += mesh.sharedVerticesInternal.Length;

				if(mesh.selectedIndexesInternal.Length > 1)
				{
					mesh.ToMesh();

					int[] welds = mesh.WeldVertices(mesh.selectedIndexesInternal, m_WeldDistance);
					res = welds != null ? new ActionResult(ActionResult.Status.Success, "Weld Vertices") : new ActionResult(ActionResult.Status.Failure, "Failed Weld Vertices");

					if(res)
					{
						if(mesh.RemoveDegenerateTriangles() != null)
						{
							mesh.ToMesh();
							welds = new int[0];	// @todo
						}

						mesh.SetSelectedVertices(welds ?? new int[0] {});
					}

					mesh.Refresh();
					mesh.Optimize();
				}

				weldCount -= mesh.sharedVerticesInternal.Length;
			}

			ProBuilderEditor.Refresh();

			if(res && weldCount > 0)
				return new ActionResult(ActionResult.Status.Success, "Weld " + weldCount + (weldCount > 1 ? " Vertices" : " Vertex"));

			return new ActionResult(ActionResult.Status.Failure, "Nothing to Weld");
		}
	}
}