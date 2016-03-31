using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProBuilder2.Math;
using ProBuilder2.Common;
using ProBuilder2.EditorCommon;
using ProBuilder2.MeshOperations;
using System.Reflection;
using ProBuilder2.Interface;

#if PB_DEBUG
using Parabox.Debug;
#endif

#if !PROTOTYPE
namespace ProBuilder2.EditorCommon
{
public class pb_UV_Editor : EditorWindow
{

#if PB_DEBUG
	static pb_Profiler profiler = new pb_Profiler("pb_UV_Editor");
#endif

#region Fields

	pb_Editor editor { get { return pb_Editor.instance; } }

	const int WINDOW_HEADER_OFFSET = 48;

	public static pb_UV_Editor instance;

	const int LEFT_MOUSE_BUTTON = 0;
	const int RIGHT_MOUSE_BUTTON = 1;
	const int MIDDLE_MOUSE_BUTTON = 2;
	const int PAD = 4;
	const float SCROLL_MODIFIER = 1f;
	const float ALT_SCROLL_MODIFIER = .07f;
	const int DOT_SIZE = 6;
	const int HALF_DOT = 3;
	const int HANDLE_SIZE = 128;
	const int MIN_ACTION_WINDOW_SIZE = 128;
	const float MIN_GRAPH_SCALE = .0001f;
	const float MAX_GRAPH_SCALE = 250f;			/// Max canvas zoom
	const float MAX_GRAPH_SCALE_SCROLL = 20f;	/// When scrolling use this value to taper the scroll effect

	const float MAX_PROXIMITY_SNAP_DIST_UV = .15f; 		///< The maximum allowable distance magnitude between coords to be considered for proximity snapping (UV coordinates)
	const float MAX_PROXIMITY_SNAP_DIST_CANVAS = 12f;	///< The maximum allowable distance magnitude between coords to be considered for proximity snapping (Canvas coordinates)
	const float MIN_DIST_MOUSE_EDGE = 8f;

	const int ACTION_WINDOW_WIDTH_MANUAL = 128;
	const int ACTION_WINDOW_WIDTH_AUTO = 210;

	private float pref_gridSnapValue = .0625f;

	static readonly Color DRAG_BOX_COLOR_BASIC 	= new Color(0f, .7f, 1f, .2f);
	static readonly Color DRAG_BOX_COLOR_PRO 	= new Color(0f, .7f, 1f, 1f);
	static readonly Color DRAG_BOX_COLOR = EditorGUIUtility.isProSkin ? DRAG_BOX_COLOR_PRO : DRAG_BOX_COLOR_BASIC;

	static readonly Color HOVER_COLOR_MANUAL 	= new Color(1f, .68f, 0f, .23f);
	static readonly Color HOVER_COLOR_AUTO 		= new Color(0f, 1f, 1f, .23f);

	static readonly Color SELECTED_COLOR_MANUAL = new Color(1f, .68f, 0f, .39f);
	static readonly Color SELECTED_COLOR_AUTO	= new Color(0f, .785f, 1f, .39f);

	#if UNITY_STANDALONE_OSX
	public bool ControlKey { get { return Event.current.modifiers == EventModifiers.Command; } }
	#else
	public bool ControlKey { get { return Event.current.modifiers == EventModifiers.Control; } }
	#endif
	public bool ShiftKey { get { return Event.current.modifiers == EventModifiers.Shift; } }

	private bool pref_showMaterial = true;	///< Show a preview texture for the first selected face in UV space 0,1?

	Color GridColorPrimary;
	Color BasicBackgroundColor;
	Color UVColorPrimary, UVColorSecondary, UVColorGroupIndicator;

	Texture2D 	dot,
				icon_textureMode_on,
				icon_textureMode_off,
				icon_sceneUV_on,
				icon_sceneUV_off;

	GUIContent gc_SceneViewUVHandles = new GUIContent("", (Texture2D)null, "Lock the SceneView handle tools to UV manipulation mode.  This allows you to move UV coordinates directly on your 3d object.");
	GUIContent gc_ShowPreviewTexture = new GUIContent("", (Texture2D)null, "When toggled on, a preview image of the first selected face's material will be drawn from coordinates 0,0 - 1,1.\n\nNote that this depends on the Material's shader having a _mainTexture property.");
	GUIContent gc_ConvertToManual = new GUIContent("Convert to Manual", "There are 2 methods of unwrapping UVs in ProBuilder; Automatic unwrapping and Manual.  Auto unwrapped UVs are generated dynamically using a set of parameters, which may be set.  Manual UVs are akin to traditional UV unwrapping, in that once you set them they will not be updated as your mesh changes.");
	GUIContent gc_ConvertToAuto = new GUIContent("Convert to Auto", "There are 2 methods of unwrapping UVs in ProBuilder; Automatic unwrapping and Manual.  Auto unwrapped UVs are generated dynamically using a set of parameters, which may be set.  Manual UVs are akin to traditional UV unwrapping, in that once you set them they will not be updated as your mesh changes.");
	GUIContent gc_RenderUV = new GUIContent((Texture2D)null, "Renders the current UV workspace from coordinates {0,0} to {1,1} to a 256px image.");

	// Full grid size in pixels (-1, 1)
	private int uvGridSize = 256;
	private float uvGraphScale = 1f;

	enum UVMode 
	{
		Auto,
		Manual,
		Mixed
	};

	UVMode mode = UVMode.Auto;

#if PB_DEBUG
	int[] UV_CHANNELS = new int[] { 0, 2, 3, 4 };
	bool debug_showCoordinates = false;
#endif
	
	// what uv channel to modify
	int channel = 0;

	private Vector2 uvGraphOffset = Vector2.zero;

	/// inspected data
	pb_Object[] selection;
	int[][] distinct_indices;

	List<pb_Face[]>[] incompleteTextureGroupsInSelection = new List<pb_Face[]>[0];
	List<List<Vector4>> incompleteTextureGroupsInSelection_CoordCache = new List<List<Vector4>>();

	int selectedUVCount = 0;
	int selectedFaceCount = 0;
	int screenWidth, screenHeight;

	// true when uvs are being moved around
	bool modifyingUVs = false;

	// The first selected face's material.  Used to draw texture preview in 0,0 - 1,1 space.
	Material preview_material;

	Tool tool = Tool.Move;
	SelectMode selectionMode { get { return editor != null ? editor.selectionMode : SelectMode.Face; } set { if(editor) editor.SetSelectionMode(value); } }

	GUIContent[] ToolIcons;
	GUIContent[] SelectionIcons;

	struct ObjectElementIndex
	{
		public int objectIndex;
		public int elementIndex;
		public int elementSubIndex;
		public bool valid;

		public void Clear()
		{
			this.objectIndex = -1;
			this.elementIndex = -1;
			this.elementSubIndex = -1;
			this.valid = false;
		}

		public ObjectElementIndex(int obj, int elem, int sub)
		{
			this.objectIndex = obj;
			this.elementIndex = elem;
			this.elementSubIndex = sub;
			this.valid = false;
		}

		public bool Equals(ObjectElementIndex oei)
		{
			return 	this.objectIndex == oei.objectIndex &&
					this.elementIndex == oei.elementIndex &&
					this.elementSubIndex == oei.elementSubIndex && 
					this.valid == oei.valid;
		}

		public override string ToString()
		{
			return valid ? objectIndex + " : " + elementIndex + " -> " + elementSubIndex : "Invalid";
		}
	}

	ObjectElementIndex nearestElement = new ObjectElementIndex(-1, -1, -1);
#endregion

#region Menu

	[MenuItem("Tools/" + pb_Constant.PRODUCT_NAME + "/UV Editor Window", false, pb_Constant.MENU_WINDOW + 0)]
	public static void MenuOpenUVEditor()
	{
		if(pb_Editor.instance != null && pb_Editor.instance.editLevel == EditLevel.Top)
			pb_Editor.instance.SetEditLevel(EditLevel.Geometry);

		EditorWindow.GetWindow<pb_UV_Editor>(pb_Preferences_Internal.GetBool(pb_Constant.pbUVEditorFloating), "UV Editor", true);		 
	}

	void OpenContextMenu()
	{
		GenericMenu menu = new GenericMenu();

		menu.AddItem (new GUIContent("Selection/Select Island", ""), false, Menu_SelectUVIsland);
		menu.AddItem (new GUIContent("Selection/Select Face", ""), false, Menu_SelectUVFace);

		menu.AddSeparator("");

		menu.AddItem (new GUIContent("Window/Open as Floating Window", ""), false, ContextMenu_OpenFloatingWindow);
		menu.AddItem (new GUIContent("Window/Open as Dockable Window", ""), false, ContextMenu_OpenDockableWindow);

		menu.ShowAsContext ();
	}

	void ScreenshotMenu()
	{
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
		// On Mac ShowAsDropdown and ShowAuxWindow both throw stack pop exceptions when initialized.
		pb_UV_Render_Options renderOptions = EditorWindow.GetWindow<pb_UV_Render_Options>(true, "Save UV Image", true);
		renderOptions.position = new Rect(	this.position.x + (Screen.width/2f - 128),
											this.position.y + (Screen.height/2f - 76),
											256f,
											152f);
		renderOptions.screenFunc = InitiateScreenshot;
#else
		pb_UV_Render_Options renderOptions = (pb_UV_Render_Options)ScriptableObject.CreateInstance(typeof(pb_UV_Render_Options));
		renderOptions.screenFunc = InitiateScreenshot;
		renderOptions.ShowAsDropDown(new Rect(	this.position.x + 348,
												this.position.y + 32,
												0,
												0),
												new Vector2(256, 152));
#endif										
	}

	static void ContextMenu_OpenFloatingWindow()
	{
		EditorPrefs.SetBool(pb_Constant.pbUVEditorFloating, true);

		EditorWindow.GetWindow<pb_UV_Editor>().Close();
		EditorWindow.GetWindow<pb_UV_Editor>(true, "UV Editor", true);
	}

	static void ContextMenu_OpenDockableWindow()
	{
		EditorPrefs.SetBool(pb_Constant.pbUVEditorFloating, false);

		EditorWindow.GetWindow<pb_UV_Editor>().Close();
		EditorWindow.GetWindow<pb_UV_Editor>(false, "UV Editor", true);
	}
#endregion

#region Enable

	void OnEnable()
	{
		this.minSize = new Vector2(500f, 300f);
		
		InitGUI();

		this.wantsMouseMove = true;

		/**
		 * Register for delegates
		 */
		pb_Editor.OnSelectionUpdate += OnSelectionUpdate;
		if(editor != null) OnSelectionUpdate(editor.selection);

		instance = this;

		pb_Object_Editor.OnGetFrameBoundsEvent += OnGetFrameBoundsEvent;
		
		nearestElement.Clear();

		// Find preferences
		pref_showMaterial = pb_Preferences_Internal.GetBool(pb_Constant.pbUVMaterialPreview);
		pref_gridSnapValue = pb_Preferences_Internal.GetFloat(pb_Constant.pbUVGridSnapValue);
	}

	void OnDisable()
	{
		instance = null;

		if(editor && editor.editLevel == EditLevel.Texture)
			editor.PopEditLevel();	

		// EditorApplication.delayCall -= this.Close;							// not sure if this is necessary?
		pb_Editor.OnSelectionUpdate -= OnSelectionUpdate;
		pb_Object_Editor.OnGetFrameBoundsEvent -= OnGetFrameBoundsEvent;
	}

	/**
	 * Loads icons, sets default colors using prefs, etc.
	 */
	void InitGUI()
	{
		bool isProSkin = true;

		GridColorPrimary = isProSkin ? new Color(1f, 1f, 1f, .2f) : new Color(0f, 0f, 0f, .2f);
		UVColorPrimary = isProSkin ? Color.green : new Color(0f, .8f, 0f, 1f);
		UVColorSecondary = isProSkin ? new Color(1f, 1f, 1f, .7f) : Color.blue;
		UVColorGroupIndicator = isProSkin ? new Color(0f, 1f, .2f, .15f) : new Color(0f, 1f, .2f, .3f);
		BasicBackgroundColor = new Color(.24f, .24f, .24f, 1f);

		dot = EditorGUIUtility.whiteTexture;

		MethodInfo loadIconMethod = typeof(EditorGUIUtility).GetMethod("LoadIcon", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
		
		isProSkin = EditorGUIUtility.isProSkin;

		Texture2D moveIcon 		= (Texture2D)loadIconMethod.Invoke(null, new object[] {"MoveTool"} );
		Texture2D rotateIcon 	= (Texture2D)loadIconMethod.Invoke(null, new object[] {"RotateTool"} );
		Texture2D scaleIcon 	= (Texture2D)loadIconMethod.Invoke(null, new object[] {"ScaleTool"} );
		Texture2D viewIcon 		= (Texture2D)loadIconMethod.Invoke(null, new object[] {"ViewToolMove"} );

		Texture2D face_Graphic_off 		= (Texture2D)(Resources.Load(isProSkin ? "GUI/ProBuilderGUI_Mode_Face-Off_Small-Pro" : "GUI/ProBuilderGUI_Mode_Face-Off_Small", typeof(Texture2D)));
		Texture2D vertex_Graphic_off 	= (Texture2D)(Resources.Load(isProSkin ? "GUI/ProBuilderGUI_Mode_Vertex-Off_Small-Pro" : "GUI/ProBuilderGUI_Mode_Vertex-Off_Small", typeof(Texture2D)));
		Texture2D edge_Graphic_off 		= (Texture2D)(Resources.Load(isProSkin ? "GUI/ProBuilderGUI_Mode_Edge-Off_Small-Pro" : "GUI/ProBuilderGUI_Mode_Edge-Off_Small", typeof(Texture2D)));

		icon_textureMode_on		= (Texture2D)(Resources.Load("GUI/ProBuilderGUI_UV_ShowTexture_On", typeof(Texture2D)));
		icon_textureMode_off	= (Texture2D)(Resources.Load("GUI/ProBuilderGUI_UV_ShowTexture_Off", typeof(Texture2D)));

		icon_sceneUV_on			= (Texture2D)(Resources.Load("GUI/ProBuilderGUI_UV_Manip_On", typeof(Texture2D)));
		icon_sceneUV_off		= (Texture2D)(Resources.Load("GUI/ProBuilderGUI_UV_Manip_Off", typeof(Texture2D)));

		gc_RenderUV.image = (Texture2D)(Resources.Load(isProSkin ? "GUI/camera-64x64" : "GUI/camera-64x64-dark", typeof(Texture2D)));

		ToolIcons = new GUIContent[4]
		{
			new GUIContent(viewIcon, "View Tool"),
			new GUIContent(moveIcon, "Move Tool"),
			new GUIContent(rotateIcon, "Rotate Tool"),
			new GUIContent(scaleIcon, "Scale Tool")
		};

		SelectionIcons = new GUIContent[3]
		{
			new GUIContent(vertex_Graphic_off, "Vertex Selection"),
			new GUIContent(edge_Graphic_off, "Edge Selection"),
			new GUIContent(face_Graphic_off, "Face Selection")
		};
	}
#endregion

#region GUI Loop

	Rect 	graphRect,
			toolbarRect, 
			actionWindowRect = new Rect(6, 64, 128, 240);

#if PB_DEBUG
	Rect buggerRect;
#endif

	Vector4 mousePosition_initial;

	Rect dragRect = new Rect(0,0,0,0);
	bool m_mouseDragging = false;

	bool needsRepaint = false;
	Rect ScreenRect = new Rect(0f, 0f, 0f, 0f);

	enum ScreenshotStatus
	{
		PrepareCanvas,
		CanvasReady,
		RenderComplete,
		Done
	}

	ScreenshotStatus screenshotStatus = ScreenshotStatus.Done;

	void OnGUI()
	{
		if(screenshotStatus != ScreenshotStatus.Done)
		{
			this.minSize = new Vector2(ScreenRect.width, ScreenRect.height);
			this.maxSize = new Vector2(ScreenRect.width, ScreenRect.height);

			pb_GUI_Utility.DrawSolidColor(new Rect(-1, -1, ScreenRect.width + 10, ScreenRect.height + 10), screenshot_backgroundColor);

			DrawUVGraph(graphRect);

			if(screenshotStatus == ScreenshotStatus.PrepareCanvas)
			{
				if(Event.current.type == EventType.Repaint)
				{
					screenshotStatus = ScreenshotStatus.CanvasReady;
					DoScreenshot();
				}

				return;
			}
			else
			{
				DoScreenshot();
			}
		}		

		if(tool == Tool.View || m_draggingCanvas)	
			EditorGUIUtility.AddCursorRect(new Rect(0,toolbarRect.y + toolbarRect.height,screenWidth,screenHeight), MouseCursor.Pan);

		ScreenRect.width = this.position.width;
		ScreenRect.height = this.position.height;

		/**
		 * if basic skin, manually tint the background
		 */
		if(!EditorGUIUtility.isProSkin)
		{
			GUI.backgroundColor = BasicBackgroundColor;
			GUI.Box(ScreenRect, "");
			GUI.backgroundColor = Color.white;
		}
		
		#if PB_DEBUG
		profiler.BeginSample("pb_UV_Editor::OnGUI");
		profiler.BeginSample("GUI Calculations");
		#endif

		if(Screen.width != screenWidth || Screen.height != screenHeight)
			OnScreenResize();

		toolbarRect = new Rect(PAD, PAD, Screen.width-PAD*2, 29);
		graphRect = new Rect(PAD, PAD, Screen.width-PAD*2, Screen.height-PAD*2);

		actionWindowRect.x = (int)Mathf.Clamp(actionWindowRect.x, PAD, Screen.width-PAD-PAD-actionWindowRect.width);
		actionWindowRect.y = (int)Mathf.Clamp(actionWindowRect.y, PAD, Screen.height-MIN_ACTION_WINDOW_SIZE);
		actionWindowRect.height = (int)Mathf.Min(Screen.height - actionWindowRect.y - 24, 350);

		switch(mode)
		{
			case UVMode.Manual:
			case UVMode.Mixed:
				actionWindowRect.width = ACTION_WINDOW_WIDTH_MANUAL;
				break;

			case UVMode.Auto:
				actionWindowRect.width = ACTION_WINDOW_WIDTH_AUTO;
				break;
		}

		#if PB_DEBUG
		profiler.EndSample();
		profiler.BeginSample("HandleInput");
		#endif

		// Mouse drags, canvas movement, etc
		HandleInput();
		
		#if PB_DEBUG
		profiler.EndSample();
		profiler.BeginSample("DrawUVGraph");
		#endif

		DrawUVGraph( graphRect );		

		#if PB_DEBUG
		profiler.EndSample();
		profiler.BeginSample("Tools");
		#endif

		// Draw AND update translation handles
		if(selection != null && selectedUVCount > 0)
		{
			switch(tool)
			{
				case Tool.Move:
					MoveTool();
					break;

				case Tool.Rotate:
					RotateTool();
					break;
			
				case Tool.Scale:
					ScaleTool();
					break;
			}
		}

		#if PB_DEBUG
		profiler.EndSample();
		profiler.BeginSample("UpdateNearestElement");
		#endif

		if(UpdateNearestElement(Event.current.mousePosition))
			Repaint();
		
		#if PB_DEBUG
		profiler.EndSample();
		profiler.BeginSample("MouseDrag");
		#endif

		if(m_mouseDragging && pb_Handle_Utility.CurrentID < 0 && !m_draggingCanvas && !m_rightMouseDrag)
		{
			Color oldColor = GUI.backgroundColor;
			GUI.backgroundColor = DRAG_BOX_COLOR;
			GUI.Box(dragRect, "");
			GUI.backgroundColor = oldColor;
		}

		#if PB_DEBUG
		profiler.EndSample();
		profiler.BeginSample("DrawUVTools");
		#endif

		DrawUVTools(toolbarRect);

		#if PB_DEBUG
		profiler.EndSample();
		profiler.BeginSample("DrawActionWindow");
		#endif

		BeginWindows();
			actionWindowRect = GUILayout.Window( 1, actionWindowRect, DrawActionWindow, "Actions" );
		EndWindows();

		#if PB_DEBUG
		profiler.EndSample();
		#endif

		if(needsRepaint)
		{
			Repaint();
			needsRepaint = false;
		}

		#if PB_DEBUG
		profiler.EndSample();
		#endif

		#if PB_DEBUG
		buggerRect = new Rect(Screen.width - 226, PAD, 220, 300);
		DrawDebugInfo(buggerRect);
		#endif
	}
#endregion

#region Editor Delegate and Event

	void OnSelectionUpdate(pb_Object[] selection)
	{	
		this.selection = selection;

		SetSelectedUVsWithSceneView();

		RefreshUVCoordinates();

		/**
		 * Get incompletely selected texture groups
		 */
		int len = selection == null ? 0 : selection.Length;

		incompleteTextureGroupsInSelection = new List<pb_Face[]>[len];
		incompleteTextureGroupsInSelection_CoordCache.Clear();

		for(int i = 0; i < len; i++)
		{
			incompleteTextureGroupsInSelection[i] = GetIncompleteTextureGroups(selection[i], selection[i].SelectedFaces);
			
			if(incompleteTextureGroupsInSelection[i].Count < 1)
			{
				continue;
			}
			else
			{
				pb_Object pb = selection[i];


				foreach(pb_Face[] incomplete_group in incompleteTextureGroupsInSelection[i])
				{
					List<Vector4> coords = new List<Vector4>();

					foreach(pb_Face face in incomplete_group)
						coords.Add(pb_Bounds2D.Center(pb.uv0.ValuesWithIndices(face.distinctIndices)));

					coords.Insert(0, pb_Bounds2D.Center(coords));

					incompleteTextureGroupsInSelection_CoordCache.Add(coords);
				}
			}
		}

		
		Repaint();
	}

	/**
	 * Automatically select textureGroup buddies, and copy origins of all UVs.
	 * Also resets the mesh to PB data, removing vertices appended by 
	 * UV2 generation.
	 */
	internal void OnBeginUVModification()
	{
		pb_Lightmapping.PushGIWorkflowMode();

		modifyingUVs = true;

		bool update = false;

		// Make sure all TextureGroups are auto-selected
		for(int i = 0; i < selection.Length; i++)
		{
			if(selection[i].SelectedFaceCount > 0)
			{
				int fc = selection[i].SelectedFaceCount;
				selection[i].SetSelectedFaces( SelectTextureGroups(selection[i], selection[i].SelectedFaces) );

				// kinda lame... this will cause setSelectedUVsWithSceneView to be called again.
				if(fc != selection[i].SelectedFaceCount)
					update = true;
			}

			selection[i].ToMesh();	 // Reset the Mesh to PB data only.
			selection[i].Refresh();
		}

		if(update)
		{
			/// UpdateSelection clears handlePosition
			Vector2 storedHandlePosition = handlePosition;
			editor.UpdateSelection();
			SetHandlePosition(storedHandlePosition, true);
		}

		CopySelectionUVs(out uv_origins);
		uvOrigin = handlePosition;
	}

	/**
	 * Internal because pb_Editor needs to call this sometimes.
	 */
	internal void OnFinishUVModification()
	{	
		pb_Lightmapping.PopGIWorkflowMode();

		modifyingUVs = false;

		if((tool == Tool.Rotate || tool == Tool.Scale) && userPivot)
			SetHandlePosition(handlePosition, true);

		if(mode == UVMode.Mixed || mode == UVMode.Auto)
		{
			pbUndo.RegisterCompleteObjectUndo(selection, (tool == Tool.Move ? "Translate UVs" : tool == Tool.Rotate ? "Rotate UVs" : "Scale UVs") );

			foreach(pb_Object pb in selection)
			{
				if(pb.SelectedFaceCount > 0)
				{
					/**
					 * Sort faces into texture groups for re-projection
					 */
					Dictionary<int, List<pb_Face>> textureGroups = new Dictionary<int, List<pb_Face>>();

					int n = -2;
					foreach(pb_Face face in System.Array.FindAll(pb.SelectedFaces, x => !x.manualUV))
					{
						if(textureGroups.ContainsKey(face.textureGroup))
							textureGroups[face.textureGroup].Add(face);
						else
							textureGroups.Add( face.textureGroup > 0 ? face.textureGroup : n--, new List<pb_Face>() {face} );
					}

					foreach(KeyValuePair<int, List<pb_Face>> kvp in textureGroups)
					{
						/**
						 * Rotation - only applies to rotation tool
						 */
						if(tool == Tool.Rotate)
						{
							foreach(pb_Face face in kvp.Value)
							{
								if((face.uv.flipU ^ face.uv.flipV) ^ face.uv.swapUV) 
									uvRotation = -uvRotation;

								face.uv.rotation += uvRotation;
								if(face.uv.rotation > 360f) face.uv.rotation = face.uv.rotation % 360f;
								if(face.uv.rotation < 0f) face.uv.rotation = 360f + (face.uv.rotation % 360f);
							}
						}

						/**
						 * Scale is applied in real-time
						 */

						/**
						 * Reproject because uv.localPivot needs to be accurate for this to work properly
						 */
						foreach(pb_Face face in kvp.Value)
							face.uv.offset = Vector2.zero;

						Vector3 nrm = Vector3.zero;
						foreach(pb_Face face in kvp.Value)
						{
							nrm += pb_Math.Normal( 	pb.vertices[face.indices[0]],
													pb.vertices[face.indices[1]],
													pb.vertices[face.indices[2]] ); 
						}

						nrm /= (float)kvp.Value.Count;

						int[] tris = pb_Face.AllTriangles(kvp.Value).ToArray();

						if(kvp.Value[0].uv.useWorldSpace)
						{
							pb.transform.TransformDirection(nrm);
							pb_UVUtility.PlanarMap( pb.transform.ToWorldSpace(pb.vertices.ValuesWithIndices(tris)), kvp.Value[0].uv, nrm );
						}
						else
						{
							pb_UVUtility.PlanarMap( pb.vertices.ValuesWithIndices(tris), kvp.Value[0].uv, nrm );
						}

						foreach(pb_Face face in kvp.Value)
							face.uv.localPivot = kvp.Value[0].uv.localPivot;

						/**
						 * Translation - applies for every tool
						 */
						Vector2 handle = handlePosition;
						Vector2 cen = (Vector2) pb_Bounds2D.Center(pb.uv0.ValuesWithIndices(tris));

						foreach(pb_Face face in kvp.Value)
							face.uv.offset = -((handle - face.uv.localPivot) - (handle-cen));

					}
				}
				else
				{
					FlagSelectedFacesAsManual(pb);
				}
			}
		}
		else if(mode == UVMode.Manual)
		{
			foreach(pb_Object pb in selection)
			{
				if(pb.SelectedFaceIndices.Length > 0)
				{
					foreach(pb_Face face in pb.SelectedFaces)
					{
						face.textureGroup = -1;
						face.manualUV = true;
					}
				}
				else
				{
					FlagSelectedFacesAsManual(pb);
				}
			}

		}

		// Regenerate UV2s
		foreach(pb_Object pb in selection)
		{
			pb.ToMesh();
			pb.Refresh();
			pb.Optimize();
		}
	}

	void SetSelectedUVsWithSceneView()
	{
		if(selection == null)
		{
			distinct_indices = new int[0][];
			return;
		}

		distinct_indices = new int[selection.Length][];

		// Append shared UV indices to SelectedTriangles array (if necessary)
		for(int i = 0; i < selection.Length; i++)
		{
			if( selection[i].sharedIndicesUV == null )
				continue;

			// pb_IntArray[] sharedUVs = selection[i].sharedIndicesUV;
			// Dictionary<int, int> uvLookup = sharedUVs.ToDictionary();
			// int[] tris = selection[i].SelectedTriangles;
			// List<int> selectedTris = new List<int>();

			// *
			//  * Put sewn UVs into the selection if they aren't already.
				
			// for(int n = 0; n < selectedTris.Count; n++)
			// {
			// 	if( uvLookup[selectedTris[n]] > -1 )
			// 		selectedTris.AddRange((int[])sharedUVs[uvLookup[tris[n]]]);
			// 	else
			// 		selectedTris.Add(tris[n]);
			// }

			pb_IntArray[] sharedUVs = selection[i].sharedIndicesUV;
			
			List<int> selectedTris = new List<int>(selection[i].SelectedTriangles);

			/**
			 * Put sewn UVs into the selection if they aren't already.
			 */	
			if(sharedUVs != null)
			{
				foreach(int[] arr in sharedUVs)
				{
					if( System.Array.Exists(arr, element => System.Array.IndexOf(selection[i].SelectedTriangles, element) > -1 ) )
					{
						selectedTris.AddRange( arr );
					}
				}
			}
			distinct_indices[i] = selectedTris.Distinct().ToArray();
		}
	}

	void OnGetFrameBoundsEvent()
	{
		FrameSelection();
		Repaint();
	}

	void OnScreenResize()
	{
		screenWidth = Screen.width;
		screenHeight = Screen.height;
		RefreshUVCoordinates();
		Repaint();
	}

	/**
	 * return true if shortcut should eat the event
	 */
	internal bool ClickShortcutCheck(pb_Object pb, pb_Face selectedFace)
	{
		Event e = Event.current;

		// Copy UV settings
		if(e.modifiers == (EventModifiers.Control | EventModifiers.Shift))
		{
			// get first selected Auto UV face
			pb_Object firstObj;
			pb_Face source;

			pb_Editor.instance.GetFirstSelectedFace(out firstObj, out source);

			if( source != null )
			{
				pbUndo.RecordObject(pb, "Copy UV Settings");

				selectedFace.SetUV( new pb_UV(source.uv) );
				selectedFace.SetMaterial( source.material );
				pb_Editor_Utility.ShowNotification("Copy UV Settings");

				pb.ToMesh();
				pb.Refresh();
				pb.Optimize();
				
				RefreshUVCoordinates();

				Repaint();
				
				return true;
			}
			else
			{
				return false;
			}
		}
		else
		if(e.modifiers == EventModifiers.Control)
		{
			int len = pb.SelectedFaces == null ? 0 : pb.SelectedFaces.Length;

			if(len < 1)
				return false;

			pb_Face anchor = pb.SelectedFaces[len-1];

			if(anchor == selectedFace) return false;

			pbUndo.RecordObject(pb, "AutoStitch");

			pb.ToMesh();

			bool success = pbUVOps.AutoStitch(pb, anchor, selectedFace);
			
			if(success)
			{	
				RefreshElementGroups(pb);

				pb.SetSelectedFaces(new pb_Face[]{selectedFace});

				// // only need to do this for one pb_Object...
				// for(int i = 0; i < selection.Length; i++)
				// 	selection[i].RefreshUV( editor.SelectedFacesInEditZone[i] );

				pb.Refresh();
				pb.Optimize();

				SetSelectedUVsWithSceneView();

				RefreshUVCoordinates();

				pb_Editor_Utility.ShowNotification("Autostitch");

				if(editor != null)
					editor.UpdateSelection(false);

				Repaint();
			}
			else
			{
				pb.Refresh();
				pb.Optimize();
			}

			return success;
		}

		return false;
	}
#endregion

#region Key and Handle Input

	bool m_ignore = false;
	bool m_rightMouseDrag = false;
	bool m_draggingCanvas = false;
	bool m_doubleClick = false;

	void HandleInput()
	{
		Event e = Event.current;

		if(e.isKey)
		{
			HandleKeyInput(e);
			return;
		}

		switch(e.type)
		{
			case EventType.MouseDown:			
				
				#if PB_DEBUG
				if(toolbarRect.Contains(e.mousePosition) || actionWindowRect.Contains(e.mousePosition) || buggerRect.Contains(e.mousePosition))
				#else
				if(toolbarRect.Contains(e.mousePosition) || actionWindowRect.Contains(e.mousePosition))
				#endif
				{
					m_ignore = true;
					return;
				}

				if(e.clickCount > 1)
					m_doubleClick = true;

				mousePosition_initial = e.mousePosition;

				break;

			case EventType.MouseDrag:

				if(m_ignore || (e.mousePosition.y <= toolbarRect.y && !m_mouseDragging))
					break;
				
				m_mouseDragging = true;

				if(e.button == RIGHT_MOUSE_BUTTON || (e.button == LEFT_MOUSE_BUTTON && e.alt))
					m_rightMouseDrag = true;
				
				needsRepaint = true;

				/* If no handle is selected, do other stuff */
				if(pb_Handle_Utility.CurrentID < 0)
				{
					if( (e.alt && e.button == LEFT_MOUSE_BUTTON) || e.button == MIDDLE_MOUSE_BUTTON || Tools.current == Tool.View)
					{
						m_draggingCanvas = true;
						uvGraphOffset.x += e.delta.x;
						uvGraphOffset.y += e.delta.y;
					}
					else if(e.button == LEFT_MOUSE_BUTTON)
					{
						dragRect.x = mousePosition_initial.x < e.mousePosition.x ? mousePosition_initial.x : e.mousePosition.x;
						dragRect.y = mousePosition_initial.y > e.mousePosition.y ? e.mousePosition.y : mousePosition_initial.y;
						dragRect.width = Mathf.Abs(mousePosition_initial.x-e.mousePosition.x);
						dragRect.height = Mathf.Abs(mousePosition_initial.y-e.mousePosition.y);	
					}
					else if(e.alt && e.button == RIGHT_MOUSE_BUTTON)
					{
						SetCanvasScale(uvGraphScale + (e.delta.x - e.delta.y) * ((uvGraphScale/MAX_GRAPH_SCALE_SCROLL) * ALT_SCROLL_MODIFIER) );
					}
				}
				break;

			case EventType.Ignore:
			case EventType.MouseUp:

				modifyingUVs_AutoPanel = false;

				if(m_ignore)
				{
					m_ignore = false;
					m_mouseDragging = false;
					m_draggingCanvas = false;
					m_doubleClick = false;
					needsRepaint = true;
					return;
				}

				if(e.button == LEFT_MOUSE_BUTTON && !m_rightMouseDrag && !modifyingUVs && !m_draggingCanvas)
				{
					Vector2 hp = handlePosition;

					if(m_mouseDragging)
					{
						OnMouseDrag();
					}
					else
					{
						pbUndo.RecordSelection(selection, "Change Selection");

						if(Event.current.modifiers == (EventModifiers)0 && editor)
							editor.ClearFaceSelection();

						OnMouseClick(e.mousePosition);

						if(m_doubleClick)
							SelectUVShell();
					}

					if(!e.shift || !userPivot)
						SetHandlePosition(UVSelectionBounds().center, false);
					else
						SetHandlePosition( hp, true );
				}

				if(e.button != RIGHT_MOUSE_BUTTON)
					m_rightMouseDrag = false;

				m_mouseDragging = false;
				m_doubleClick = false;
				m_draggingCanvas = false;

				if(modifyingUVs)
					OnFinishUVModification();

				uvRotation = 0f;
				uvScale = Vector2.one;

				needsRepaint = true;
				break;

			case EventType.ScrollWheel:
				
				SetCanvasScale( uvGraphScale - e.delta.y * ((uvGraphScale/MAX_GRAPH_SCALE_SCROLL) * SCROLL_MODIFIER) );
				e.Use();
				
				needsRepaint = true;
				break;

			case EventType.ContextClick:
					
					if(!m_rightMouseDrag)
						OpenContextMenu();
					else
						m_rightMouseDrag = false;
					break;
					
			default:
				return;
		}
	}

	void HandleKeyInput(Event e)
	{
		if(e.type != EventType.KeyUp)
			return;

		bool used = false;

		switch(e.keyCode)
		{
			case KeyCode.Keypad0: 
			case KeyCode.Alpha0:
				if(GUI.GetNameOfFocusedControl().Equals(""))
				{
					ResetCanvas();
					uvGraphOffset = Vector2.zero;
					e.Use();
					needsRepaint = true;
					used = true;
				}
				break;

			case KeyCode.Q:
				SetTool_Internal(Tool.View);
				used = true;
				break;

			case KeyCode.W:
				SetTool_Internal(Tool.Move);
				used = true;
				break;
			
			case KeyCode.E:
				SetTool_Internal(Tool.Rotate);
				used = true;
				break;

			case KeyCode.R:
				SetTool_Internal(Tool.Scale);
				used = true;
				break;

			case KeyCode.F:
				FrameSelection();
				used = true;
				break;
		}

		if(!used && pb_Editor.instance)
			pb_Editor.instance.ShortcutCheck(e);
	}

	/**
	 * Finds the nearest edge to the mouse and sets the `nearestEdge` struct with it's info
	 */
	bool UpdateNearestElement(Vector2 mousePosition)
	{
		if(selection == null || m_mouseDragging || modifyingUVs || tool == Tool.View)// || pb_Handle_Utility.CurrentID > -1)
		{
			if(nearestElement.valid)
			{
				nearestElement.valid = false;
				return true;
			}
			else
			{
				return false;
			}
		}

		Vector2 mpos = GUIToUVPoint(mousePosition);
		List<Vector4> uv;
		Vector2 x, y;
		ObjectElementIndex oei = nearestElement;
		nearestElement.valid = false;

		switch(selectionMode)
		{
			case SelectMode.Edge:
				float dist, best = 100f;

				try
				{
					for(int i = 0; i < selection.Length; i++)
					{
						pb_Object pb = selection[i];
						uv = GetUVs(pb, channel);

						for(int n = 0; n < pb.faces.Length; n++)
						{
							for(int p = 0; p < pb.faces[n].edges.Length; p++)
							{
								x.x = uv[pb.faces[n].edges[p].x].x;
								x.y = uv[pb.faces[n].edges[p].x].y;
								y.x = uv[pb.faces[n].edges[p].y].x;
								y.y = uv[pb.faces[n].edges[p].y].y;

								dist = pb_Math.DistancePointLineSegment(mpos, x, y);

								if(dist < best)
								{
									nearestElement.objectIndex = i;
									nearestElement.elementIndex = n;
									nearestElement.elementSubIndex = p;
									best = dist;
								}
							}
						}
					}
				}
				catch {}

				nearestElement.valid = best < MIN_DIST_MOUSE_EDGE;
				break;

			case SelectMode.Face:

				try
				{
					bool superBreak = false;
					for(int i = 0; i < selection.Length; i++)
					{
						uv = GetUVs(selection[i], channel);

						for(int n = 0; n < selection[i].faces.Length; n++)
						{
							if( pb_Math.PointInPolygon(uv, selection[i].faces[n].edges.AllTriangles(), mpos) )
							{
								nearestElement.objectIndex = i;
								nearestElement.elementIndex = n;
								nearestElement.elementSubIndex = -1;
								nearestElement.valid = true;
								superBreak = true;
								break;
							}
							if(superBreak) break;
						}
					}
				} catch {}
				break;
		}

		return !nearestElement.Equals(oei);
	}

	/**
	 * Allows another window to set the current tool.
	 * Does *not* update any other editor windows.
	 */
	public void SetTool(Tool tool)
	{
		this.tool = tool;
		nearestElement.Clear();
		Repaint();
	}

	/**
	 * Sets the global Tool.current and updates any other windows.
	 */
	private void SetTool_Internal(Tool tool)
	{
		SetTool(tool);

		if(tool == Tool.View)
			Tools.current = Tool.View;
		else
			Tools.current = Tool.None;	

		if(editor)
		{
			editor.SetTool(tool);
			SceneView.RepaintAll();
		}
	}

	/**
	 * Handle click check and updating selection.
	 */
	void OnMouseClick(Vector2 mousePosition)
	{
		if (selection == null) return;

		#if PB_DEBUG
		profiler.BeginSample("OnMouseClick");
		#endif
		
		switch(selectionMode)
		{
			case SelectMode.Edge:
				if(nearestElement.valid)
				{
					pb_Object pb = selection[nearestElement.objectIndex];

					pb_Edge edge = pb.faces[nearestElement.elementIndex].edges[nearestElement.elementSubIndex];
					int ind = pb.SelectedEdges.IndexOf(edge, pb.sharedIndices.ToDictionary());

					if( ind > -1 )
						pb.SetSelectedEdges(pb.SelectedEdges.RemoveAt(ind));
					else
						pb.SetSelectedEdges(pb.SelectedEdges.Add(edge));
				}
				break;

			case SelectMode.Face:

				Vector2 mpos = GUIToUVPoint(mousePosition);
				bool superBreak = false;
				for(int i = 0; i < selection.Length; i++)
				{
					HashSet<pb_Face> selectedFaces = new HashSet<pb_Face>(selection[i].SelectedFaces);

					for(int n = 0; n < selection[i].faces.Length; n++)
					{
						if( pb_Math.PointInPolygon(selection[i].uv0, selection[i].faces[n].edges.AllTriangles(), mpos) )
						{						
							if( selectedFaces.Contains(selection[i].faces[n]) )
								selectedFaces.Remove(selection[i].faces[n]);
							else
								selectedFaces.Add(selection[i].faces[n]);

							// Only select one face per click
							superBreak = true;
							break;
						}
					}

					selection[i].SetSelectedFaces( selectedFaces.ToArray() );

					if(superBreak) break;
				}
				break;

			case SelectMode.Vertex:
				RefreshUVCoordinates( new Rect(mousePosition.x-8, mousePosition.y-8, 16, 16), true );
				break;
		}

		if(editor)
		{
			editor.UpdateSelection(false);
			SceneView.RepaintAll();
		}
		else
		{
			RefreshSelectedUVCoordinates();
		}
		
		#if PB_DEBUG
		profiler.EndSample();
		#endif
	}

	void OnMouseDrag()
	{
		Event e = Event.current;

		if(editor && !e.shift && !e.control && !e.command)
		{
			pbUndo.RecordSelection(selection, "Change Selection");
			editor.ClearFaceSelection();
		}

		RefreshUVCoordinates(dragRect, false);
		e.Use();
	}
#endregion

#region Tools

	// tool properties
	float uvRotation = 0f;
	Vector4 uvOrigin = Vector4.zero;
	Vector4[][] uv_origins = null;
	Vector2 handlePosition = Vector2.zero,
			handlePosition_offset = Vector2.zero;

	/**
	 * Draw an interactive 2d Move tool that affects the current selection of UV coordinates.
	 */
	void MoveTool()
	{
		Event e = Event.current;

		Vector2 t_handlePosition = UVToGUIPoint(handlePosition);

		#if PB_DEBUG
		profiler.BeginSample("Handle");
		#endif

		// enable right click drag
		pb_Handle_Utility.limitToLeftButton = false;

		t_handlePosition = pb_Handle_Utility.PositionHandle2d(1, t_handlePosition, HANDLE_SIZE);

		t_handlePosition = GUIToUVPoint(t_handlePosition);
		pb_Handle_Utility.limitToLeftButton = true;

		#if PB_DEBUG
		profiler.EndSample();
		#endif

		if (!e.isMouse) return;

		/**
		 *	Setting a custom pivot
		 */
		if((e.button == RIGHT_MOUSE_BUTTON || (e.alt && e.button == LEFT_MOUSE_BUTTON)) && !pb_Math.Approx(t_handlePosition, handlePosition, .0001f))
		{
			#if PB_DEBUG
			profiler.BeginSample("Set Custom Pivot");
			#endif

			// flag the handle as having been user set.
			userPivot = true;

			if(ControlKey)
			{
				handlePosition = pbUtil.SnapValue(t_handlePosition, (handlePosition - t_handlePosition).ToMask(pb_Math.HANDLE_EPSILON) * pref_gridSnapValue);
			}
			else
			{		
				handlePosition = t_handlePosition;

				/**
				 * Attempt vertex proximity snap if shift key is held
				 */
				if( ShiftKey )
				{
					float dist, minDist = MAX_PROXIMITY_SNAP_DIST_CANVAS;
					Vector2 offset = Vector2.zero;
					for(int i = 0; i < selection.Length; i++)
					{
						/// todo reset MAX_PROXIMITY_SNAP_DIST
						int index = pb_Handle_Utility.NearestPoint(handlePosition, selection[i].uv0, MAX_PROXIMITY_SNAP_DIST_CANVAS);

						if(index < 0) continue;
						
						dist = pb_VectorUtility.Distance2D(selection[i].uv0[index], handlePosition);

						if(dist < minDist)
						{
							minDist = dist;
							offset.x = selection[i].uv0[index].x - handlePosition.x;
							offset.y = selection[i].uv0[index].y - handlePosition.y;
						}
					}

					handlePosition += offset;
				}
			}

			SetHandlePosition(handlePosition, true);

			#if PB_DEBUG
			profiler.EndSample();
			#endif
			return;
		}

		/**
		 *	Tool activated - moving some UVs around.
		 * 	Unlike rotate and scale tools, if the selected faces are Auto the pb_UV changes will be applied
		 *	in OnFinishUVModification, not at real time.
		 */
		if( !pb_Math.Approx(t_handlePosition, handlePosition, pb_Math.HANDLE_EPSILON) )
		{
			/**
			 * Start of move UV operation
			 */
			if(!modifyingUVs)
			{
				// if auto uvs, the changes are applied after action is complete
				if(mode != UVMode.Auto)
					pbUndo.RegisterCompleteObjectUndo(selection, "Translate UVs");

				OnBeginUVModification();
			}

			needsRepaint = true;

			Vector2 newUVPosition = t_handlePosition;

			if(ControlKey)
				newUVPosition = pbUtil.SnapValue(newUVPosition, (handlePosition - t_handlePosition).ToMask(pb_Math.HANDLE_EPSILON) * pref_gridSnapValue);

			for(int n = 0; n < selection.Length; n++)
			{
				pb_Object pb = selection[n];
				List<Vector4> uvs = GetUVs(pb, channel);

				foreach(int i in distinct_indices[n])
					uvs[i] = pb_VectorUtility.Subtract2D(newUVPosition, pb_VectorUtility.Subtract2D(uvOrigin, uv_origins[n][i]));

				// set uv positions before figuring snap dist stuff
				// don't use ApplyUVs() here because we don't actually want to access the msh
				// til we have to.
				pb.SetUVs(channel, uvs);

				if(!ShiftKey || ControlKey)
					pb.ApplyUVs();
			}

			/**
			 * Proximity snapping
			 */
			if(ShiftKey && !ControlKey)
			{
				Vector2 nearestDelta = Vector2.one;

				for(int i = 0; i < selection.Length; i++)
				{
					List<Vector4> sel = pbUtil.ValuesWithIndices(GetUVs(selection[i], channel), distinct_indices[i]);

					for(int n = 0; n < selection.Length; n++)
					{
						Vector4 offset;
						if( pb_Handle_Utility.NearestPointDelta(sel, GetUVs(selection[n], channel), i == n ? distinct_indices[i] : null, MAX_PROXIMITY_SNAP_DIST_UV, out offset) )
						{
							if( pb_Handle_Utility.CurrentAxisConstraint.Mask(offset).sqrMagnitude < nearestDelta.sqrMagnitude)
								nearestDelta = offset;
						}
					}
				}

				if(nearestDelta.sqrMagnitude < .003f )
				{
					nearestDelta = pb_Handle_Utility.CurrentAxisConstraint.Mask(nearestDelta);

					for(int i = 0; i < selection.Length; i++)
					{
						List<Vector4> uvs = GetUVs(selection[i], channel);

						foreach(int n in distinct_indices[i])
							uvs[n] = pb_VectorUtility.Add2D(nearestDelta, uvs[n]);

						selection[i].SetUVs(channel, uvs);
						selection[i].ApplyUVs();
					}

					handlePosition = newUVPosition + nearestDelta;
				}
				else
				{
					for(int i = 0; i < selection.Length; i++)
						selection[i].ApplyUVs();
				}
			}

			RefreshSelectedUVCoordinates();
		}
	}

	internal void SceneMoveTool(Vector2 t_handlePosition, Vector2 handlePosition)
	{
		t_handlePosition = UVToGUIPoint(t_handlePosition);

		/**
		 *	Tool activated - moving some UVs around.
		 * 	Unlike rotate and scale tools, if the selected faces are Auto the pb_UV changes will be applied
		 *	in OnFinishUVModification, not at real time.
		 */
		if( !pb_Math.Approx(t_handlePosition, handlePosition, .0001f) )
		{
			/**
			 * Start of move UV operation
			 */
			if(!modifyingUVs)
			{
				pbUndo.RecordObjects(selection, "Move UVs");
				OnBeginUVModification();
				uvOrigin = GUIToUVPoint(t_handlePosition);	// have to set this one special
			}

			Vector2 newUVPosition = GUIToUVPoint(t_handlePosition);

			if(ControlKey)
				newUVPosition = pbUtil.SnapValue(newUVPosition, (handlePosition-t_handlePosition).ToMask(pb_Math.HANDLE_EPSILON) * pref_gridSnapValue);

			Vector4 newPosVec4 = (Vector4)newUVPosition;

			for (int n = 0; n < selection.Length; n++)
			{
				pb_Object pb = selection[n];
				List<Vector4> uvs = GetUVs(pb, channel);

				foreach(int i in distinct_indices[n])
					uvs[i] = newPosVec4 - (uvOrigin - uv_origins[n][i]);

				pb.SetUVs(channel, uvs);
				pb.ApplyUVs(channel);
			}

			RefreshSelectedUVCoordinates();
		}
	}

	void RotateTool()
	{
		float t_uvRotation = uvRotation;

		uvRotation = pb_Handle_Utility.RotationHandle2d(0, UVToGUIPoint(handlePosition), uvRotation, 128);

		if(uvRotation != t_uvRotation)
		{
			if(!modifyingUVs)
			{
				if(mode != UVMode.Auto)
					pbUndo.RegisterCompleteObjectUndo(selection, "Rotate UVs");

				OnBeginUVModification();
			}

			if(ControlKey)
				uvRotation = pbUtil.SnapValue(uvRotation, 15f);

			for(int n = 0; n < selection.Length; n++)
			{
				pb_Object pb = selection[n];
				List<Vector4> uvs = GetUVs(pb, channel);

				foreach(int i in distinct_indices[n])
					uvs[i] = uv_origins[n][i].RotateAroundPoint( uvOrigin, uvRotation );

				pb.ApplyUVs(channel);
			}

			nearestElement.valid = false;
		}

		needsRepaint = true;
	}

	internal void SceneRotateTool(float rotation)
	{
		if(rotation != uvRotation)
		{
			if(ControlKey)
				rotation = pbUtil.SnapValue(rotation, 15f);

			uvRotation = rotation;

			if(!modifyingUVs)
			{
				pbUndo.RecordObjects(selection, "Rotate UVs");
				OnBeginUVModification();
			}

			for(int n = 0; n < selection.Length; n++)
			{
				pb_Object pb = selection[n];
				List<Vector4> uvs = GetUVs(pb, channel);

				foreach(int i in distinct_indices[n])
				{
					uvs[i] = uv_origins[n][i].RotateAroundPoint( uvOrigin, rotation );
				}

				pb.ApplyUVs(channel);
			}

			nearestElement.valid = false;
		}
	}

	Vector2 uvScale = Vector2.one;
	void ScaleTool()
	{
		Vector2 t_uvScale = uvScale;
		uvScale = pb_Handle_Utility.ScaleHandle2d(2, UVToGUIPoint(handlePosition), uvScale, 128);

		if(ControlKey)
			uvScale = pbUtil.SnapValue(uvScale, pref_gridSnapValue);

		if(pb_Math.Approx(uvScale.x, 0f, Mathf.Epsilon)) uvScale.x = .0001f;
		if(pb_Math.Approx(uvScale.y, 0f, Mathf.Epsilon)) uvScale.y = .0001f;

		if(t_uvScale != uvScale)
		{
			if(!modifyingUVs)
			{		
				if(mode != UVMode.Auto)
					pbUndo.RegisterCompleteObjectUndo(selection, "Scale UVs");
					
				OnBeginUVModification();
			}

			if(mode == UVMode.Mixed || mode == UVMode.Manual)
			{
				for(int n = 0; n < selection.Length; n++)
				{
					pb_Object pb = selection[n];
					List<Vector4> uvs = GetUVs(pb, channel);

					foreach(int i in distinct_indices[n])
					{
						uvs[i] = uv_origins[n][i].ScaleAroundPoint(uvOrigin, uvScale);
					}
					
					pb.SetUVs(channel, uvs);
					pb.ApplyUVs(channel);
				}
			}

			/**
			 * Auto mode scales UVs prior to rotation, so we have to do it separately here.
			 */
			if(mode == UVMode.Mixed || mode == UVMode.Auto)
			{
				Vector2 scale = uvScale.DivideBy(t_uvScale);
				for(int n = 0; n < selection.Length; n++)
				{
					pb_Face[] autoFaces = System.Array.FindAll(selection[n].SelectedFaces, x => !x.manualUV);
					foreach(pb_Face face in autoFaces)
					{
						face.uv.scale = Vector2.Scale(face.uv.scale, scale);
					}
					selection[n].RefreshUV(autoFaces);
				}

				RefreshSelectedUVCoordinates();
			}

			nearestElement.valid = false;
			needsRepaint = true;
		}
	}

	/**
	 * New scale, previous scale
	 */
	internal void SceneScaleTool(Vector2 textureScale, Vector2 previousScale)
	{
		textureScale.x = 1f / textureScale.x;
		textureScale.y = 1f / textureScale.y;

		previousScale.x = 1f / previousScale.x;
		previousScale.y = 1f / previousScale.y;

		if(ControlKey)
			textureScale = pbUtil.SnapValue(textureScale, pref_gridSnapValue);

		if(!modifyingUVs)
		{		
			pbUndo.RecordObjects(selection, "Scale UVs");
			OnBeginUVModification();
		}

		if(mode == UVMode.Mixed || mode == UVMode.Manual)
		{
			for(int n = 0; n < selection.Length; n++)
			{
				pb_Object pb = selection[n];
				List<Vector4> uvs = GetUVs(pb, channel);

				foreach(int i in distinct_indices[n])
				{
					uvs[i] = uv_origins[n][i].ScaleAroundPoint(uvOrigin, textureScale);
				}
				
				pb.SetUVs(channel, uvs);
				pb.ApplyUVs(channel);
			}
		}

		/**
		 * Auto mode scales UVs prior to rotation, so we have to do it separately here.
		 */
		if(mode == UVMode.Mixed || mode == UVMode.Auto)
		{
			Vector2 delta = textureScale.DivideBy(previousScale);

			for(int n = 0; n < selection.Length; n++)
			{
				pb_Face[] autoFaces = System.Array.FindAll(selection[n].SelectedFaces, x => !x.manualUV);
				foreach(pb_Face face in autoFaces)
				{
					face.uv.scale = Vector2.Scale(face.uv.scale, delta);
				}
				selection[n].RefreshUV(autoFaces);
			}

			RefreshSelectedUVCoordinates();
		}

		nearestElement.valid = false;
		needsRepaint = true;
	}
#endregion

#region UV Graph Drawing

	Vector2 UVGraphCenter = Vector2.zero;
	
	// private class UVGraphCoordinates
	// {
		// Remember that Unity GUI coordinates Y origin is the bottom
	private static Vector2 UpperLeft = new Vector2(  0f, -1f);
	private static Vector2 UpperRight = new Vector2( 1f, -1f);
	private static Vector2 LowerLeft = new Vector2(  0f,  0f);
	private static Vector2 LowerRight = new Vector2( 1f,  0f);

	private Rect UVGraphZeroZero = new Rect(0,0,40,40);
	private Rect UVGraphOneOne = new Rect(0,0,40,40);

	/**
	 * Must be called inside GL immediate mode context
	 */
	internal void DrawUVGrid(Color gridColor)
	{		
		Color col = GUI.color;
		gridColor.a = .1f;

		if(Event.current.type == EventType.Repaint)
		{
			GL.PushMatrix();
			pb_Handle_Utility.handleMaterial.SetPass(0);
			GL.MultMatrix(Handles.matrix);

			GL.Begin( GL.LINES );
			GL.Color( gridColor );

			// Grid temp vars
			int GridLines = 64;
			float StepSize = pref_gridSnapValue;	// In UV coordinates

			// Exponentially scale grid size
			while(StepSize * uvGridSize * uvGraphScale < uvGridSize/10)
				StepSize *= 2f;

			// Calculate what offset the grid should be (different from uvGraphOffset in that we always want to render the grid)
			Vector2 gridOffset = uvGraphOffset;
			gridOffset.x = gridOffset.x % (StepSize * uvGridSize * uvGraphScale); // (uvGridSize * uvGraphScale);
			gridOffset.y = gridOffset.y % (StepSize * uvGridSize * uvGraphScale); // (uvGridSize * uvGraphScale);

			Vector2 p0 = Vector2.zero, p1 = Vector2.zero;

			///==== X axis lines
			p0.x = ( ( StepSize * (GridLines/2) * uvGridSize ) * uvGraphScale) + UVGraphCenter.x + gridOffset.x;
			p1.x = ( ( -StepSize * (GridLines/2) * uvGridSize ) * uvGraphScale) + UVGraphCenter.x + gridOffset.x;

			for(int i = 0; i < GridLines + 1; i++)
			{
				p0.y = (((StepSize * i) - ((GridLines*StepSize)/2)) * uvGridSize) * uvGraphScale + UVGraphCenter.y + gridOffset.y;
				p1.y = p0.y;

				GL.Vertex( p0 );
				GL.Vertex( p1 );
			}

			///==== Y axis lines
			p0.y = ( ( StepSize * (GridLines/2) * uvGridSize ) * uvGraphScale) + UVGraphCenter.y + gridOffset.y;
			p1.y = ( ( -StepSize * (GridLines/2) * uvGridSize ) * uvGraphScale) + UVGraphCenter.y + gridOffset.y;

			for(int i = 0; i < GridLines + 1; i++)
			{
				p0.x = (((StepSize * i) - ((GridLines*StepSize)/2)) * uvGridSize) * uvGraphScale + UVGraphCenter.x + gridOffset.x;
				p1.x = p0.x;

				GL.Vertex( p0 );
				GL.Vertex( p1 );
			}

			// Box
			if(screenshotStatus == ScreenshotStatus.Done)
			{
				GL.Color( Color.gray );

				GL.Vertex(UVGraphCenter + (UpperLeft * uvGridSize) * uvGraphScale + uvGraphOffset );
				GL.Vertex(UVGraphCenter + (UpperRight * uvGridSize) * uvGraphScale + uvGraphOffset );

				GL.Vertex(UVGraphCenter + (UpperRight * uvGridSize) * uvGraphScale + uvGraphOffset );
				GL.Vertex(UVGraphCenter + (LowerRight * uvGridSize) * uvGraphScale + uvGraphOffset );

				GL.Color( pb_Constant.ProBuilderBlue );

				GL.Vertex(UVGraphCenter + (LowerRight * uvGridSize) * uvGraphScale + uvGraphOffset );
				GL.Vertex(UVGraphCenter + (LowerLeft * uvGridSize) * uvGraphScale + uvGraphOffset );

				GL.Vertex(UVGraphCenter + (LowerLeft * uvGridSize) * uvGraphScale + uvGraphOffset );
				GL.Vertex(UVGraphCenter + (UpperLeft * uvGridSize) * uvGraphScale + uvGraphOffset );
			}

			GL.End();
			GL.PopMatrix();	// Pop pop!
		}

		GUI.color = gridColor;

		UVGraphZeroZero.x = UVRectIdentity.x + 4;
		UVGraphZeroZero.y = UVRectIdentity.y + UVRectIdentity.height + 1;

		UVGraphOneOne.x = UVRectIdentity.x + UVRectIdentity.width + 4;
		UVGraphOneOne.y = UVRectIdentity.y;

		Handles.BeginGUI();
			GUI.Label(UVGraphZeroZero, "0, 0" );
			GUI.Label(UVGraphOneOne, "1, 1" );
		Handles.EndGUI();

		GUI.color = col;
	}

	Rect UVRectIdentity = new Rect(0,0,1,1);
	/// re-usable rect for drawing graphs
	Rect r = new Rect(0,0,0,0);

	private void DrawUVGraph(Rect rect)
	{
		UVGraphCenter = rect.center;

		UVRectIdentity.width = uvGridSize * uvGraphScale;
		UVRectIdentity.height = UVRectIdentity.width;

		UVRectIdentity.x = UVGraphCenter.x + uvGraphOffset.x;
		UVRectIdentity.y = UVGraphCenter.y + uvGraphOffset.y - UVRectIdentity.height;

		if(pref_showMaterial && preview_material && preview_material.mainTexture)
			EditorGUI.DrawPreviewTexture(UVRectIdentity, preview_material.mainTexture, null, ScaleMode.StretchToFill, 0);

		if( (screenshotStatus != ScreenshotStatus.PrepareCanvas && screenshotStatus != ScreenshotStatus.CanvasReady) || !screenshot_hideGrid)
		{
			#if PB_DEBUG
				profiler.BeginSample("Draw Base Graph");
					DrawUVGrid(GridColorPrimary);
				profiler.EndSample();
			#else
				DrawUVGrid(GridColorPrimary);
			#endif
		}

		if(selection == null || selection.Length < 1)
			return;

		/**
		 * Draw regular old outlines
		 */
		#if PB_DEBUG
		profiler.BeginSample("Draw Base Edges + Vertices");
		#endif

		Vector2 p = Vector2.zero;
		List<Vector4> uv;
		r.width = DOT_SIZE;
		r.height = DOT_SIZE;

		/**
		 * Draw all vertices if in vertex mode
		 */
		if(selectionMode == SelectMode.Vertex && screenshotStatus == ScreenshotStatus.Done)
		{

			for(int i = 0; i < selection.Length; i++)
			{
				uv = selection[i].uv0;

				GUI.color = UVColorSecondary;
				for(int n = 0; n < uv.Count; n++)
				{
					p = UVToGUIPoint(uv[n]);
					r.x = p.x - HALF_DOT;
					r.y = p.y - HALF_DOT;
					GUI.DrawTexture(r, dot, ScaleMode.ScaleToFit);
				}
	
				GUI.color = UVColorPrimary;
				foreach(int index in selection[i].SelectedTriangles)
				{
					p = UVToGUIPoint(uv[index]);
					r.x = p.x - HALF_DOT;
					r.y = p.y - HALF_DOT;
					GUI.DrawTexture(r, dot, ScaleMode.ScaleToFit);
				}
			}
		}

		Handles.color = UVColorGroupIndicator;

		foreach(List<Vector4> lines in incompleteTextureGroupsInSelection_CoordCache)
			for(int i = 1; i < lines.Count; i++)
				Handles.CircleCap(-1, UVToGUIPoint(lines[i]), Quaternion.identity, 8f);

		#if PB_DEBUG
		if(debug_showCoordinates)
		{
			Handles.BeginGUI();
			r.width = 256f;
			r.height = 40f;
			foreach(pb_Object pb in selection)
			{
				foreach(int i in pb.SelectedTriangles)
				{
					Vector2 v = pb.uv[i];
					Vector2 sv =  UVToGUIPoint(v);
					r.x = sv.x;
					r.y = sv.y;
					GUI.Label(r, "UV:" + v.ToString("F2") + "\nScreen: " + (int)sv.x + ", " + (int)sv.y);
				}
			}
			Handles.EndGUI();
		}
		#endif

		GUI.color = Color.white;

		if( Event.current.type == EventType.Repaint )
		{
			GL.PushMatrix();
			pb_Handle_Utility.handleMaterial.SetPass(0);
			GL.MultMatrix(Handles.matrix);

			/**
			 * Draw incomplete texture group indicators (unless taking a screenshot)
			 */
			if(screenshotStatus == ScreenshotStatus.Done)
			{
				GL.Begin(GL.LINES);
				GL.Color(UVColorGroupIndicator);

				foreach(List<Vector4> lines in incompleteTextureGroupsInSelection_CoordCache)
				{
					Vector2 cen = lines[0];

					for(int i = 1; i < lines.Count; i++)
					{
						GL.Vertex(UVToGUIPoint(cen));
						GL.Vertex(UVToGUIPoint(lines[i]));
					}
				}
				GL.End();
			}

			GL.Begin(GL.LINES);

			if(screenshotStatus != ScreenshotStatus.Done)
				GL.Color(screenshot_lineColor);
			else
				GL.Color(UVColorSecondary);

			Vector2 x = Vector2.zero, y = Vector2.zero;

			for(int i = 0; i < selection.Length; i++)
			{
				pb_Object pb = selection[i];
				uv = pb.uv0;

				for(int n = 0; n < pb.faces.Length; n++)
				{
					pb_Face face = pb.faces[n];

					foreach(pb_Edge edge in face.edges)
					{
						x = UVToGUIPoint(uv[edge.x]);
						y = UVToGUIPoint(uv[edge.y]);

						GL.Vertex3(x.x, x.y, 0f);
						GL.Vertex3(y.x, y.y, 0f);
					}
				}	
			}
			GL.End();

			#if PB_DEBUG
			profiler.EndSample();
			#endif

			/**
			 * Draw selected UVs with shiny green color and dots
			 */
			#if PB_DEBUG
			profiler.BeginSample("Draw Selected Edges + Vertices");
			#endif

			if(screenshotStatus != ScreenshotStatus.Done)
			{
				GL.PopMatrix();
				return;
			}

			GL.Begin(GL.LINES);
			GL.Color(UVColorPrimary);

			for(int i = 0; i < selection.Length; i++)
			{
				pb_Object pb = selection[i];
				uv = GetUVs(pb, channel);

				if(pb.SelectedEdges.Length > 0)
				{
					foreach(pb_Edge edge in pb.SelectedEdges)
					{
						x = UVToGUIPoint(uv[edge.x]);
						y = UVToGUIPoint(uv[edge.y]);

						GL.Vertex3(x.x, x.y, 0f);
						GL.Vertex3(y.x, y.y, 0f);
						
						// #if PB_DEBUG
						// GUI.Label( new Rect(x.x, x.y, 120, 20), pb.uv[edge.x].ToString() );
						// GUI.Label( new Rect(y.x, y.y, 120, 20), pb.uv[edge.y].ToString() );
						// #endif
					}
				}
			}

			GL.End();

			#if PB_DEBUG
			profiler.EndSample();
			#endif

			switch(selectionMode)
			{
				case SelectMode.Edge:

					#if PB_DEBUG
					profiler.BeginSample("Draw Nearest Edge Highlight");
					#endif

					GL.Begin(GL.LINES);
					GL.Color(Color.red);
					if(nearestElement.valid && nearestElement.elementSubIndex > -1 && !modifyingUVs)
					{
						pb_Edge edge = selection[nearestElement.objectIndex].faces[nearestElement.elementIndex].edges[nearestElement.elementSubIndex];
						GL.Vertex( UVToGUIPoint(selection[nearestElement.objectIndex].uv0[edge.x]) );
						GL.Vertex( UVToGUIPoint(selection[nearestElement.objectIndex].uv0[edge.y]) );
					}
					GL.End();
					
					#if PB_DEBUG
					profiler.EndSample();
					#endif

					break;

				case SelectMode.Face:
				{
					#if PB_DEBUG
					profiler.BeginSample("Draw Nearest Face Highlight GL");
					#endif

					Vector3 v = Vector3.zero;

					if(nearestElement.valid && !m_mouseDragging)
					{
						GL.Begin(GL.TRIANGLES);

						GL.Color( selection[nearestElement.objectIndex].faces[nearestElement.elementIndex].manualUV ? HOVER_COLOR_MANUAL : HOVER_COLOR_AUTO);
						int[] tris = selection[nearestElement.objectIndex].faces[nearestElement.elementIndex].indices;
						uv = GetUVs(selection[nearestElement.objectIndex], channel);
						
						for(int i = 0; i < tris.Length; i+=3)
						{
							v = UVToGUIPoint(uv[tris[i+0]]);
							GL.Vertex3(v.x, v.y, 0f);
							v = UVToGUIPoint(uv[tris[i+1]]);
							GL.Vertex3(v.x, v.y, 0f);
							v = UVToGUIPoint(uv[tris[i+2]]);
							GL.Vertex3(v.x, v.y, 0f);
						}

						GL.End();
					}

					#if PB_DEBUG
					profiler.EndSample();
					profiler.BeginSample("Draw Selected Face Highlights GL");
					#endif

					GL.Begin(GL.TRIANGLES);
					for(int i = 0; i < selection.Length; i++)
					{
						foreach(pb_Face face in selection[i].SelectedFaces)
						{
							GL.Color(face.manualUV ? SELECTED_COLOR_MANUAL : SELECTED_COLOR_AUTO);

							int[] tris = face.indices;
							uv = GetUVs(selection[i], channel);

							for(int n = 0; n < tris.Length; n+=3)
							{
								v = UVToGUIPoint(uv[tris[n+0]]);
								GL.Vertex3(v.x, v.y, 0f);
								v = UVToGUIPoint(uv[tris[n+1]]);
								GL.Vertex3(v.x, v.y, 0f);
								v = UVToGUIPoint(uv[tris[n+2]]);
								GL.Vertex3(v.x, v.y, 0f);
							}
						}
					}
					GL.End();

					#if PB_DEBUG
					profiler.EndSample();
					#endif
				}
				break;

			}

			GL.PopMatrix();
		}
	}	

	#if PB_DEBUG
	void DrawDebugInfo(Rect rect)
	{
		Vector2 mpos = Event.current.mousePosition;

		GUI.BeginGroup(rect);
		GUILayout.BeginVertical(GUILayout.MaxWidth(rect.width-6));

		GUILayout.Label("Scale: " + uvGraphScale);

		GUILayout.Label("Object: " + nearestElement.ToString());

		int t_channel = channel;
		channel = EditorGUILayout.IntPopup(channel, new string[] {"0/1", "2", "3", "4"}, UV_CHANNELS);
		if(channel != t_channel)
			RefreshUVCoordinates();

			GUILayout.Label(mpos + " (" + Screen.width + ", " + Screen.height + ")");

		// GUILayout.Label("m_mouseDragging: " + m_mouseDragging);
		// GUILayout.Label("m_rightMouseDrag: " + m_rightMouseDrag);
		// GUILayout.Label("m_draggingCanvas: " + m_draggingCanvas);
		// GUILayout.Label("modifyingUVs: " + modifyingUVs);

		debug_showCoordinates = EditorGUILayout.Toggle("Show UV coordinates", debug_showCoordinates);

		GUILayout.Label("Handle: " + handlePosition.ToString("F3"));
		GUILayout.Label("Offset: " + handlePosition_offset.ToString("F3"));

		GUI.EndGroup();
	}
	#endif
#endregion

#region UV Canvas Operations

	/**
	 * Zooms in on the current UV selection
	 */
	void FrameSelection()
	{
		needsRepaint = true;

		if(selection == null || selection.Length < 1 || (editor && editor.selectedVertexCount < 1))
		{
			SetCanvasCenter(Event.current.mousePosition - UVGraphCenter - uvGraphOffset);
			return;
		}

		SetCanvasCenter(selectedGuiBounds.center - uvGraphOffset - UVGraphCenter);

		if(UVSelectionBounds().size.sqrMagnitude > 0f)
		{
			pb_Bounds2D bounds = UVSelectionBounds();

			float x = (float) screenWidth / ((bounds.size.x * uvGridSize) * 1.5f);
			float y = (float) (screenHeight-96) / ((bounds.size.y * uvGridSize) * 1.5f);

			SetCanvasScale( Mathf.Min(x, y) );
		}
	}

	/**
	 * Sets the canvas scale.  1 is full size, .1 is super zoomed, and 2 would be 2x out.
	 */
	void SetCanvasScale(float zoom)
	{
		Vector2 center = -(uvGraphOffset / uvGraphScale);
		uvGraphScale = Mathf.Clamp(zoom, MIN_GRAPH_SCALE, MAX_GRAPH_SCALE);
		SetCanvasCenter( center * uvGraphScale );
	}

	/**
	 * Center the canvas on this point.  Should be in GUI coordinates.
	 */
	void SetCanvasCenter(Vector2 center)
	{
		uvGraphOffset = center;
		uvGraphOffset.x = -uvGraphOffset.x;
		uvGraphOffset.y = -uvGraphOffset.y;
	}

	void ResetCanvas()
	{
		uvGraphScale = 1f;
		SetCanvasCenter( new Vector2(.5f, -.5f) * uvGridSize * uvGraphScale );
	}

	/**
	 * Set the handlePosition to this UV coordinate.
	 */
	bool userPivot = false;
	void SetHandlePosition(Vector2 uvPoint, bool isUserSet)
	{
		if(float.IsNaN(uvPoint.x) || float.IsNaN(uvPoint.y))
			return;

		userPivot = isUserSet;
		handlePosition_offset = UVSelectionBounds().center - uvPoint;
		handlePosition = uvPoint;
	}

	/**
	 * Used by pb_Editor to reset the pivot offset when adding or removing faces in the scenview.
	 */
	public void ResetUserPivot()
	{
		handlePosition_offset = Vector4.zero;
	}

	pb_Bounds2D GetBounds(int i, int f, Vector2[][] array)
	{
		return new pb_Bounds2D( pbUtil.ValuesWithIndices(array[i], selection[i].faces[f].distinctIndices) );
	}

	private pb_Bounds2D _selected_gui_bounds = new pb_Bounds2D(Vector2.zero, Vector2.zero);

	/**
	 * Returns the bounds of the current selection in GUI space.
	 */
	pb_Bounds2D selectedGuiBounds
	{
		get
		{
			pb_Bounds2D uvBounds = UVSelectionBounds();
			_selected_gui_bounds.center = UVToGUIPoint(uvBounds.center);
			_selected_gui_bounds.size = uvBounds.size * uvGridSize * uvGraphScale;
			return _selected_gui_bounds;
		}
	}

	public Vector2 UVToGUIPoint(Vector2 v)
	{
		return pb_Handle_Utility.UVToGUIPoint(v, UVGraphCenter, uvGraphOffset, uvGraphScale, uvGridSize);
	}

	public Vector2 GUIToUVPoint(Vector2 v)
	{
		return pb_Handle_Utility.GUIToUVPoint(v, UVGraphCenter, uvGraphOffset, uvGraphScale, uvGridSize);
	}

	public Vector4 UVToGUIPoint(Vector4 v)
	{
		return pb_Handle_Utility.UVToGUIPoint(v, UVGraphCenter, uvGraphOffset, uvGraphScale, uvGridSize);
	}

	public Vector4 GUIToUVPoint(Vector4 v)
	{
		return pb_Handle_Utility.GUIToUVPoint(v, UVGraphCenter, uvGraphOffset, uvGraphScale, uvGridSize);
	}

	/**
	 * Returns the bounds of the current selection in UV space
	 */
	pb_Bounds2D UVSelectionBounds()
	{	
		float xMin = 0f, xMax = 0f, yMin = 0f, yMax = 0f;
		bool first = true;
		for(int n = 0; n < selection.Length; n++)
		{
			List<Vector4> uv = GetUVs(selection[n], channel);

			foreach(int i in distinct_indices[n])
			{
				if(first)
				{ 
					xMin = uv[i].x; 
					xMax = xMin; 
					yMin = uv[i].y; 
					yMax = yMin; 
					first = false;
				} else {
					xMin = Mathf.Min(xMin, uv[i].x);
					yMin = Mathf.Min(yMin, uv[i].y);

					xMax = Mathf.Max(xMax, uv[i].x);
					yMax = Mathf.Max(yMax, uv[i].y);
				}
			}
		}

		return new pb_Bounds2D( new Vector2( (xMin+xMax)/2f, (yMin+yMax)/2f ), new Vector2(xMax-xMin, yMax-yMin) );
	}
#endregion

#region Refresh / Set

	// Doesn't call Repaint for you
	void RefreshUVCoordinates()
	{
		RefreshUVCoordinates(null, false);
	}

	/**
	 * If dragRect is null, the selected UV array will be derived using the selected ProBuilder faces.
	 * If it ain't null, selected UVs will be set to the UV coordinates contained within the drag rect.
	 */
	void RefreshUVCoordinates(Rect? dragRect, bool isClick)
	{	
		if(editor == null || selection == null) return;

		#if PB_DEBUG
		profiler.BeginSample("RefreshUVCoordinates");
		#endif

		// Convert dragrect from Unity GUI space to UV coordinates
		pb_Bounds2D dragBounds;
		if(dragRect != null)
			dragBounds = new pb_Bounds2D( GUIToUVPoint(((Rect)dragRect).center), new Vector2( ((Rect)dragRect).width, ((Rect)dragRect).height) / (uvGraphScale * uvGridSize) );
		else
			dragBounds = new pb_Bounds2D( Vector2.zero, Vector2.zero );
	
		selectedUVCount   = editor.selectedVertexCount;
		selectedFaceCount = editor.selectedFaceCount;

		for(int i = 0; i < selection.Length; i++)
		{
			pb_Object pb = selection[i];

			List<Vector4> mshUV = GetUVs(pb, channel);

			// if this isn't the uv2 channel and the uv count doesn't match pb vertex count, reset
			if(channel != 2 && (mshUV == null || mshUV.Count != pb.vertexCount || mshUV.Any(x => float.IsNaN(x.x) || float.IsNaN(x.y))))
			{
				Debug.LogWarning("RefreshUVCoordinates reset UV channel " + channel + " due to null or invalid UV data.");
				mshUV = pbUtil.Fill<Vector4>(Vector4.zero, pb.vertexCount);
				pb.SetUVs(channel, mshUV);
				pb.ApplyUVs(channel);
			}
			
			int len = mshUV.Count;

			// this should be separate from RefreshUVCoordinates
			if(dragRect != null)
			{	
				switch(selectionMode)
				{
					case SelectMode.Vertex:
						List<int> selectedTris = new List<int>(pb.SelectedTriangles);

						for(int j = 0; j < len; j++)
						{
							if( dragBounds.ContainsPoint( mshUV[j] ) )
							{
								int indx = selectedTris.IndexOf(j);

								if(indx > -1)
									selectedTris.RemoveAt(indx);
								else
									selectedTris.Add(j);

								// if this is a click, only do one thing per-click
								if(isClick)
									break;
							}
						}

						pb.SetSelectedTriangles(selectedTris.ToArray());
						break;

					case SelectMode.Edge:
						List<pb_Edge> selectedEdges = new List<pb_Edge>(pb.SelectedEdges);

						for(int n = 0; n < pb.faces.Length; n++)
						{
							for(int p = 0; p < pb.faces[n].edges.Length; p++)
							{
								pb_Edge edge = pb.faces[n].edges[p];

								if( dragBounds.IntersectsLineSegment( mshUV[edge.x], mshUV[edge.y]) )	
								{
									if(!selectedEdges.Contains(edge))
										selectedEdges.Add( edge );
									else
										selectedEdges.Remove( edge );
								}
							}
						}

						pb.SetSelectedEdges(selectedEdges.ToArray());
						break;
				
					/**
					 * Check if any of the faces intersect with the mousedrag rect.
					 */
					case SelectMode.Face:

						HashSet<int> selectedFaces = new HashSet<int>(selection[i].SelectedFaceIndices);
						for(int n = 0; n < pb.faces.Length; n++)
						{
							int[] distinctIndices = pb.faces[n].distinctIndices;

							bool allPointsContained = true;

							for(int t = 0; t < distinctIndices.Length; t++)
							{
								if( !dragBounds.ContainsPoint(mshUV[distinctIndices[t]]) )
								{
									allPointsContained = false;
									break;
								}
							}

							// // if(dragBounds.Intersects(faceBounds))
							// for(int t = 0; t < uvs.Length; t++)
							// {
							// 	if(!dragBounds.ContainsPoint(uvs[t]))
							// 	{
							// 		allPointsContained = false;
							// 		break;
							// 	}
							// }

							if(allPointsContained)
							{
								if( selectedFaces.Contains(n) )
										selectedFaces.Remove(n);
									else
										selectedFaces.Add(n);
							}
						}
						selection[i].SetSelectedFaces(selectedFaces.ToArray());

						break;
				}

				editor.UpdateSelection(false);
				SceneView.RepaintAll();
			}
		}

		// figure out what the mode of selected faces is
		if(editor.selectedFaceCount > 0)
		{
			// @todo write a more effecient method for this
			List<bool> manual = new List<bool>();
			for(int i = 0; i < selection.Length; i++)
				manual.AddRange( selection[i].SelectedFaces.Select(x => x.manualUV).ToList() );
			int c = manual.Distinct().Count();
			if(c > 1)
				mode = UVMode.Mixed;
			else if (c > 0)
				mode = manual[0] ? UVMode.Manual : UVMode.Auto;
		}
		else
		{
			mode = UVMode.Manual;
		}

		editor.GetFirstSelectedMaterial(ref preview_material);

		handlePosition = UVSelectionBounds().center - handlePosition_offset;

		#if PB_DEBUG
		profiler.EndSample();
		#endif
	}

	/**
	 * Get a UV channel.
	 */
	static List<Vector4> GetUVs(pb_Object pb, int channel)
	{
		switch(channel)
		{
			case 2:
				return pb.uv2;

			case 3:
				return pb.uv3;

			case 4:
				return pb.uv4;

			default:
				return pb.uv0;
		}
	}

	/**
	 * Refresh only the selected UV coordinates.
	 */
	void RefreshSelectedUVCoordinates()
	{	
		handlePosition = UVSelectionBounds().center - handlePosition_offset;
	}
#endregion

#region UV Toolbar

	Rect toolbarRect_tool = new Rect(PAD, PAD, 130f, 24f);
	Rect toolbarRect_select = new Rect(PAD + 130 + PAD, PAD, 130f, 24f);
	void DrawUVTools(Rect rect)
	{
		GUI.BeginGroup(rect);

		/**
		 * Handle toggles and SelectionMode toggles.
		 */
		EditorGUI.BeginChangeCheck();

			tool = (Tool)GUI.Toolbar(toolbarRect_tool, (int)tool < 0 ? 0 : (int)tool, ToolIcons, "Command");

		if(EditorGUI.EndChangeCheck())
		{
			SetTool_Internal(tool);
			SceneView.RepaintAll();
		}

		int t_selectionMode = (int)selectionMode;
		
		t_selectionMode = GUI.Toolbar(toolbarRect_select, (int)t_selectionMode, SelectionIcons, "Command");

		if(t_selectionMode != (int)selectionMode)
			selectionMode = (SelectMode)t_selectionMode;

		/**
		 * Begin Editor pref toggles (Show Texture, Lock UV sceneview handle, etc)
		 */

		Rect editor_toggles_rect = new Rect(toolbarRect_select.x + 130, PAD, 36f, 22f);

		if(editor)
		{
			gc_SceneViewUVHandles.image = editor.editLevel == EditLevel.Texture ? icon_sceneUV_on : icon_sceneUV_off;
			if(GUI.Button(editor_toggles_rect, gc_SceneViewUVHandles))
			{
				if(editor.editLevel == EditLevel.Texture)
					editor.PopEditLevel();
				else
					editor.SetEditLevel(EditLevel.Texture);
			}
		}

		editor_toggles_rect.x += editor_toggles_rect.width + PAD;

		gc_ShowPreviewTexture.image =  pref_showMaterial ? icon_textureMode_on : icon_textureMode_off;
		if(GUI.Button(editor_toggles_rect, gc_ShowPreviewTexture))
		{
			pref_showMaterial = !pref_showMaterial;
			EditorPrefs.SetBool(pb_Constant.pbUVMaterialPreview, pref_showMaterial);
		}

		editor_toggles_rect.x += editor_toggles_rect.width + PAD;

		if(GUI.Button(editor_toggles_rect, gc_RenderUV))
		{
			ScreenshotMenu();
		}
		
		GUI.EndGroup();

	}

	static Rect ActionWindowDragRect = new Rect(0,0,10000,20);
	void DrawActionWindow(int windowIndex)
	{
		GUILayout.Label("UV Mode: " + mode.ToString(), EditorStyles.boldLabel);
	
		switch(mode)
		{
			case UVMode.Auto:
				DrawAutoModeUI((int)actionWindowRect.width);
				break;

			case UVMode.Manual:
				DrawManualModeUI((int)actionWindowRect.width);
				break;

			case UVMode.Mixed:

				if(GUILayout.Button( gc_ConvertToManual, EditorStyles.miniButton))
					Menu_SetManualUV();

				if(GUILayout.Button( gc_ConvertToAuto, EditorStyles.miniButton))
					Menu_SetAutoUV();

				break;
		}

		// Get some draggage up in hurrr
		GUI.DragWindow(ActionWindowDragRect);
	}

	bool modifyingUVs_AutoPanel = false;

	void DrawAutoModeUI(int width)
	{
		if(GUILayout.Button("Convert to Manual", EditorStyles.miniButton))
			Menu_SetManualUV();

		#if PB_DEBUG
		profiler.BeginSample("pb_AutoUV_Editor");
		#endif


		if( pb_AutoUV_Editor.OnGUI(selection, (int)actionWindowRect.width) )
		{
			if(!modifyingUVs_AutoPanel)
			{				
				modifyingUVs_AutoPanel = true;

				foreach(pb_Object pb in selection)
				{
					pb.ToMesh();
					pb.Refresh();
				}
			}

			for(int i = 0; i < selection.Length; i++)
			{
				selection[i].RefreshUV(editor.SelectedFacesInEditZone[i] );
			}

			RefreshSelectedUVCoordinates();
		}

		#if PB_DEBUG
		profiler.EndSample();
		#endif

		GUI.enabled = selectedFaceCount > 0;
		
	}

	bool tool_weldButton = false;

	Vector2 scroll = Vector2.zero;
	void DrawManualModeUI(int width)
	{
		GUI.enabled = selectedFaceCount > 0;
		if(GUILayout.Button(gc_ConvertToAuto, EditorStyles.miniButton))
			Menu_SetAutoUV();

		scroll = EditorGUILayout.BeginScrollView(scroll);

		/**
		 * Projection Methods
		 */
		GUILayout.Label("Project UVs", EditorStyles.miniBoldLabel);

		GUILayout.BeginHorizontal();
			if(GUILayout.Button("Planar", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
				Menu_PlanarProject();

			if(GUILayout.Button("Box", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
				Menu_BoxProject();
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();

		// GUI.enabled = selectedUVCount > 0;
		// 	if(GUILayout.Button("Spherical", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
		// 		Menu_SphericalProject();

			// if(GUILayout.Button("Box", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
				// Menu_BoxProject();
		GUILayout.EndHorizontal();

		/**
		 * Selection
		 */
		GUI.enabled = selectedUVCount > 0;
		GUILayout.Label("Selection", EditorStyles.miniBoldLabel);

		if(GUILayout.Button("Select Island", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
			Menu_SelectUVIsland();

		GUI.enabled = selectedUVCount > 0 && selectionMode != SelectMode.Face;
		if(GUILayout.Button("Select Face", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
			Menu_SelectUVFace();

		/**
		 * Edit
		 */
		GUILayout.Label("Edit", EditorStyles.miniBoldLabel);

		GUI.enabled = selectedUVCount > 1;

		tool_weldButton = pb_GUI_Utility.ToolSettingsGUI("Weld", "Merge selected vertices that are within a specified distance of one another.",
			tool_weldButton,
			Menu_SewUVs,
			WeldButtonGUI,
			width,
			20,
			selection);

		if(GUILayout.Button("Collapse UVs", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
			Menu_CollapseUVs();

		GUI.enabled = selectedUVCount > 1;
		if(GUILayout.Button("Split UVs", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
			Menu_SplitUVs();

		GUILayout.Space(4);

		if(GUILayout.Button("Flip Horizontal", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
			Menu_FlipUVs(Vector2.up);

		if(GUILayout.Button("Flip Vertical", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
			Menu_FlipUVs(Vector2.right);

		GUILayout.Space(4);

		if(GUILayout.Button("Fit UVs", EditorStyles.miniButton, GUILayout.MaxWidth(actionWindowRect.width)))
			Menu_FitUVs();

		EditorGUILayout.EndScrollView();

		GUI.enabled = true;
	}

	/**
	 * Expose the distance parameter used in Weld operations.
	 * ProBuilder only.
	 */
	const float MIN_WELD_DISTANCE = .001f;
	private void WeldButtonGUI(int width)
	{
		EditorGUI.BeginChangeCheck();

		float weldDistance = pb_Preferences_Internal.GetFloat(pb_Constant.pbUVWeldDistance);
		
		if(weldDistance <= MIN_WELD_DISTANCE)
			weldDistance = MIN_WELD_DISTANCE;

		EditorGUIUtility.labelWidth = width - 70;
		weldDistance = EditorGUILayout.FloatField(new GUIContent("Max", "The maximum distance between two vertices in order to be welded together."), weldDistance);
	
		if( EditorGUI.EndChangeCheck() )
		{
			if(weldDistance < MIN_WELD_DISTANCE)
				weldDistance = MIN_WELD_DISTANCE;
			EditorPrefs.SetFloat(pb_Constant.pbUVWeldDistance, weldDistance);
		}
	}
#endregion

#region UV Selection

	/**
	 * Given selected tris, return an array of all indices attached to face
	 */
	private void SelectUVShell()
	{
		if(selection == null || selection.Length < 1) return;

		foreach(pb_Object pb in selection)
		{
			pb_Face[] faces = GetFaces(pb, pb.SelectedTriangles);
			
			List<int> elementGroups = new List<int>();
			List<int> textureGroups = new List<int>();
			
			foreach(pb_Face f in faces)
			{
				if(f.manualUV)
					elementGroups.Add(f.elementGroup);
				else
					textureGroups.Add(f.textureGroup);
			}

			IEnumerable<pb_Face> matches = System.Array.FindAll(pb.faces, x => 
																(x.manualUV && x.elementGroup > -1 && elementGroups.Contains(x.elementGroup)) ||
																(!x.manualUV && x.textureGroup > 0 && textureGroups.Contains(x.textureGroup)) );

			pb.SetSelectedFaces( faces.Union(matches).ToArray() );
	
			if(editor != null)
				editor.UpdateSelection(false);
		}
	}

	/**
	 * If any of the faces in @selection are AutoUV and in a texture group, this 
	 * augments the texture group buddies to the selection and returns it.
	 */
	private pb_Face[] SelectTextureGroups(pb_Object pb, pb_Face[] selection)
	{	
		List<int> texGroups = selection.Select(x => x.textureGroup).Where(x => x > 0).Distinct().ToList();
		pb_Face[] sel = System.Array.FindAll(pb.faces, x => !x.manualUV && texGroups.Contains(x.textureGroup));

		return selection.Union(sel).ToArray();
	}

	/**
	 * If selection contains faces that are part of a texture group, and not all of those group faces are in the selection,
	 * return a pb_Face[] of that entire group so that we can show the user some indication of that groupage.
	 */
	private List<pb_Face[]> GetIncompleteTextureGroups(pb_Object pb, pb_Face[] selection)
	{
		// get distinct list of all selected texture groups
		List<int> groups = selection.Select(x => x.textureGroup).Where(x => x > 0).Distinct().ToList();
		List<pb_Face[]> incompleteGroups = new List<pb_Face[]>(); 
		
		// figure out how many 
		for(int i = 0; i < groups.Count; i++)
		{
			pb_Face[] whole_group = System.Array.FindAll(pb.faces, x => !x.manualUV && groups[i] == x.textureGroup);
			int inSelection = System.Array.FindAll(selection, x => x.textureGroup == groups[i]).Length;

			if(inSelection != whole_group.Length)
				incompleteGroups.Add(whole_group);
		}

		return incompleteGroups;
	}

	/**
	 * Sets the SceneView and UV selection to include any faces with currently selected indices.
	 */
	private void SelectUVFace()
	{
		if(selection == null || selection.Length < 1) return;

		foreach(pb_Object pb in selection)
		{
			pb_Face[] faces = GetFaces(pb, pb.SelectedTriangles);
			pb.SetSelectedFaces(faces);
	
			if(editor != null)
				editor.UpdateSelection(false);
		}
	}

	/**
	 *	Element Groups are used to associate faces that share UV seams.  In this 
	 *	way, we can easily select UV shells by grouping all elements as opposed
	 *	to iterating through and checking nearby faces every time.
	 */
	private void RefreshElementGroups(pb_Object pb)
	{
		foreach(pb_Face f in pb.faces)
			f.elementGroup = -1;

		pb_IntArray[] sharedUVs = pb.sharedIndicesUV;

		int eg = 0;
		foreach(pb_IntArray pint in sharedUVs)
		{
			if(pint.array.Length < 2) continue;

			pb_Face[] faces = GetFaces(pb, pint);

			int cur = pb.UnusedElementGroup(eg++);

			foreach(pb_Face f in faces)
			{
				if(f.elementGroup > -1)
				{
					int g = f.elementGroup;

					foreach(pb_Face fin in pb.faces)
						if(fin.elementGroup == g)
							fin.elementGroup = cur;
				}
				
				f.elementGroup = cur;
			}
		}
	}

	/**
	 * Get all faces that contain any of the passed vertex indices.
	 */
	private pb_Face[] GetFaces(pb_Object pb, int[] indices)
	{
		#if PB_DEBUG
		profiler.BeginSample("GetFaces");
		#endif

		List<pb_Face> faces = new List<pb_Face>();
		foreach(pb_Face f in pb.faces)
		{
			foreach(int i in f.distinctIndices)
			{
				if(System.Array.IndexOf(indices, i) > -1)
				{
					faces.Add(f);
					break;
				}
			}
		}

		#if PB_DEBUG
		profiler.EndSample();
		#endif

		return faces.Distinct().ToArray();
	}

	/**
	 * Finds all faces attached to the current selection and marks the faces as having been manually modified.
	 */
	private void FlagSelectedFacesAsManual(pb_Object pb)
	{
		// Mark selected UV faces manualUV flag true
		foreach(pb_Face f in GetFaces(pb, pb.SelectedTriangles))
		{
			f.textureGroup = -1;
			f.manualUV = true;
		}
	}

	/**
	 * Creates a copy of each msh.uv array in a jagged array, and stores the average of all points.
	 */
	private void CopySelectionUVs(out Vector4[][] uvCopy)
	{		
		uvCopy = new Vector4[selection.Length][];

		for(int i = 0; i < selection.Length; i++)
		{
			pb_Object pb = selection[i];
			uvCopy[i] = GetUVs(pb, channel).ToArray();
		}
	}
#endregion

#region Menu Commands

	/**
	 * Planar projecct UVs on all selected faces in selection.
	 */
	public void Menu_PlanarProject()
	{
		pbUndo.RecordObjects(selection, "Planar Project Faces");
		int projected = 0;
	
		for(int i = 0; i < selection.Length; i++)
		{
			if(selection[i].SelectedFaces.Length > 0)
			{
				selection[i].ToMesh();	// Remove UV2 modifications

				pbUVOps.SplitUVs(selection[i], selection[i].SelectedTriangles);

				pbUVOps.ProjectFacesAuto(selection[i], selection[i].SelectedFaces);
				
				foreach(int f in selection[i].SelectedFaceIndices)
					selection[i].faces[f].manualUV = true;

				selection[i].Refresh();	// refresh afer UVs are sorted, since tangents need them
				selection[i].Optimize();
				
				RefreshElementGroups(selection[i]);

				projected++;
			}

		}

		SetSelectedUVsWithSceneView();

		if(projected > 0)
		{
			if(pb_Preferences_Internal.GetBool(pb_Constant.pbNormalizeUVsOnPlanarProjection))
				Menu_FitUVs();
			else
				CenterUVsAtPoint( handlePosition );

			ResetUserPivot();
		}
		
		pb_Editor_Utility.ShowNotification(this, projected > 0 ? "Planar Project" : "Nothing Selected");
	
		// Special case
		RefreshUVCoordinates();
		needsRepaint = true;
	}

	/**
	 * Box project all selected faces in selection.
	 */
	public void Menu_BoxProject()
	{
		int p = 0;
		pbUndo.RegisterCompleteObjectUndo(selection, "Box Project Faces");

		for(int i = 0; i < selection.Length; i++)
		{
			if(selection[i].SelectedFaces.Length > 0)
			{
				pbUVOps.ProjectFacesBox(selection[i], selection[i].SelectedFaces);
				
				foreach(int f in selection[i].SelectedFaceIndices)
					selection[i].faces[f].manualUV = true;

				p ++;

				selection[i].Optimize();
			}
		}

		SetSelectedUVsWithSceneView();

		if(p > 0)
		{
			CenterUVsAtPoint( handlePosition );

			ResetUserPivot();
		}

		pb_Editor_Utility.ShowNotification(this, "Box Project UVs");
		
		// Special case
		RefreshUVCoordinates();
		needsRepaint = true;
	}

	public void Menu_SphericalProject()
	{
		pbUndo.RecordObjects(selection, "Spherical Project UVs");
		int count = 0;

		foreach(pb_Object pb in selection)
		{
			if(pb.SelectedTriangleCount > 1)
			{
				count += pb.SelectedTriangleCount;

				pb.ToMesh();
				pbUVOps.UnwrapSpherical(pb, pb.SelectedTriangles.Distinct().ToArray());
				pb.Refresh();
				pb.Optimize();
			}
		}

		if(count > 0)
		{
			ResetUserPivot();
		}

		pb_Editor_Utility.ShowNotification(this, "Spherical Project UVs");
		
		// Special case
		RefreshUVCoordinates();
		needsRepaint = true;
	}

	/**
	 * Reset all selected faces to use the default Automatic unwrapping.  Removes
	 * any modifications made by the user.
	 */
	public void Menu_SetAutoUV()
	{
		SetIsManual(false);
	}

	/**
	 * Sets all faces to manual UV mode.
	 */
	public void Menu_SetManualUV()
	{
		SetIsManual(true);
	}

	public void SetIsManual(bool isManual)
	{
		pbUndo.RegisterCompleteObjectUndo(selection, isManual ? "Set Faces Manual" : "Set Faces Auto");
		
		foreach(pb_Object pb in selection)
		{
			pb.ToMesh();
			pbUVOps.SetAutoUV(pb, pb.SelectedFaces, !isManual);
			pb.Refresh();
			pb.Optimize();
		}

		SetSelectedUVsWithSceneView();
		RefreshUVCoordinates();

		pb_Editor_Utility.ShowNotification(this, "Set " + selectedFaceCount + " Faces " + (isManual ? "Manual" : "Auto"));
	}

	public void Menu_SelectUVIsland()
	{
		pbUndo.RecordObjects(selection, "Select Island");
		
		SelectUVShell(); 
		pb_Editor_Utility.ShowNotification(this, "Select UV Island");
	}

	public void Menu_SelectUVFace()
	{
		pbUndo.RecordObjects(selection, "Select Face");

		SelectUVFace(); 
		pb_Editor_Utility.ShowNotification(this, "Select UV Face");
	}

	public void Menu_CollapseUVs()
	{
		if(channel == 1)
		{
			pb_Editor_Utility.ShowNotification(this, "Invalid UV2 Operation");
			return;
		}

		pbUndo.RecordObjects(selection, "Collapse UVs");

		for(int i = 0; i < selection.Length; i++)
		{			
			selection[i].ToMesh();

			selection[i].CollapseUVs(distinct_indices[i]);

			selection[i].Refresh();
			selection[i].Optimize();
		}

		RefreshSelectedUVCoordinates();

		pb_Editor_Utility.ShowNotification(this, "Collapse UVs");
	}

	public pb_ActionResult Menu_SewUVs(pb_Object[] selection)
	{
		if(channel == 1)
		{
			pb_Editor_Utility.ShowNotification(this, "Invalid UV2 Operation");
			return new pb_ActionResult(Status.Canceled, "Invalid UV2 Operation");
		}

		float weldDistance = pb_Preferences_Internal.GetFloat(pb_Constant.pbUVWeldDistance);

		pbUndo.RecordObjects(selection, "Sew UV Seams");
		for(int i = 0; i < selection.Length; i++)
		{
			selection[i].ToMesh();

			selection[i].SewUVs(distinct_indices[i], weldDistance);
			RefreshElementGroups(selection[i]);

			selection[i].Refresh();
			selection[i].Optimize();
		}
		
		RefreshSelectedUVCoordinates();

		pb_Editor_Utility.ShowNotification(this, "Weld UVs");
		return new pb_ActionResult(Status.Success, "Invalid UV2 Operation");
	}

	public void Menu_SplitUVs()
	{
		if(channel == 1)
		{
			pb_Editor_Utility.ShowNotification(this, "Invalid UV2 Operation");
			return;
		}

		pbUndo.RecordObjects(selection, "Split UV Seams");

		foreach(pb_Object pb in selection)
		{
			pb.ToMesh();
			
			pb.SplitUVs(pb.SelectedTriangles);
			RefreshElementGroups(pb);

			pb.Refresh();
			pb.Optimize();
		}

		SetSelectedUVsWithSceneView();
		RefreshSelectedUVCoordinates();

		pb_Editor_Utility.ShowNotification(this, "Split UVs");
	}

	/**
	 * Flips UVs across the provided direction. The current pivot position is used as origin.  Can be horizontal, vertical, or anything in between.
	 */
	public void Menu_FlipUVs(Vector2 direction)
	{
		pbUndo.RecordObjects(selection, "Flip " + direction);

		Vector2 center = handlePosition;

		for(int i = 0; i < selection.Length; i++)
		{
			selection[i].ToMesh();

			selection[i].SplitUVs(selection[i].SelectedTriangles);

			List<Vector4> uv = GetUVs(selection[i], channel);

			foreach(int n in selection[i].SelectedTriangles.Distinct())
				uv[n] = pb_Math.ReflectPoint(uv[n], center, center + direction);

			selection[i].ApplyUVs(channel);
			
			RefreshElementGroups(selection[i]);

			selection[i].Refresh();
			selection[i].Optimize();
		}

		SetSelectedUVsWithSceneView();
		RefreshSelectedUVCoordinates();

		if( direction == Vector2.right )
		{
			pb_Editor_Utility.ShowNotification(this, "Flip UVs Vertically");
		}
		else if( direction == Vector2.up )
		{
			pb_Editor_Utility.ShowNotification(this, "Flip UVs Horizontally");
		} 
		else
		{
			pb_Editor_Utility.ShowNotification(this, "Flip UVs");
		}
	}

	/**
	 * Fit selected UVs to 0,1 space.
	 */
	public void Menu_FitUVs()
	{
		pbUndo.RecordObjects(selection, "Fit UVs");

		for(int i = 0; i < selection.Length; i++)
		{
			if(selection[i].SelectedTriangleCount < 3) continue;

			selection[i].ToMesh();

			List<Vector4> uv = GetUVs(selection[i], channel);
			List<Vector4> uvs = pbUtil.ValuesWithIndices( uv, distinct_indices[i] );

			uvs = pbUVOps.FitUVs(uvs);

			for(int n = 0; n < uvs.Count; n++)
				uv[ distinct_indices[i][n] ] = uvs[n];

			selection[i].Refresh();
			selection[i].Optimize();
		}

		RefreshSelectedUVCoordinates();
		pb_Editor_Utility.ShowNotification(this, "Fit UVs");
	}

	/**
	 * Moves the selected UVs to where their bounds center is now point, where point is in UV space.
	 * Does not call ToMesh or Refresh.
	 */
	private void CenterUVsAtPoint(Vector2 point)
	{
		Vector2 uv_cen = UVSelectionBounds().center;
		Vector4 delta = uv_cen - point;

		for(int i = 0; i < selection.Length; i++)
		{
			List<Vector4> uv = GetUVs(selection[i], channel);

			foreach(int n in selection[i].SelectedTriangles.Distinct())
				uv[n] -= delta;

			selection[i].SetUVs(channel, uv);
		}
	}
#endregion

#region Screenshot Rendering

	float curUvScale = 0f;					///< Store the user set positioning and scale before modifying them for a screenshot
	Vector2 curUvPosition = Vector2.zero;	///< ditto ^
	Texture2D screenshot;
	Rect screenshotCanvasRect = new Rect(0,0,0,0);
	Vector2 screenshotTexturePosition = Vector2.zero;

	// settings
	int screenshot_size = 1024;
	bool screenshot_hideGrid = true;
	bool screenshot_transparentBackground;
	Color screenshot_lineColor = Color.green;
	Color screenshot_backgroundColor = Color.black;
	string screenshot_path = "";

	readonly Color UV_FILL_COLOR = new Color(.192f,.192f,.192f,1f);	///< This is the default background of the UV editor - used to compare bacground pixels when rendering UV template

	void InitiateScreenshot(int ImageSize, bool HideGrid, Color LineColor, bool TransparentBackground, Color BackgroundColor)
	{
		screenshot_size = ImageSize;
		screenshot_hideGrid = HideGrid;
		screenshot_lineColor = LineColor;
		screenshot_transparentBackground = TransparentBackground;
		screenshot_backgroundColor = TransparentBackground ? UV_FILL_COLOR : BackgroundColor;

		// if line color and background color are the same but we want transparent backgruond,
		// make sure that the background fill will be distinguishable from the lines during the
		// opacity wipe
		if(TransparentBackground && (screenshot_lineColor.Approx(screenshot_backgroundColor, .001f)))
		{
			screenshot_backgroundColor.r += screenshot_backgroundColor.r < .9f ? .1f : -.1f;
			screenshot_backgroundColor.g += screenshot_backgroundColor.g < .9f ? .1f : -.1f;
			screenshot_backgroundColor.b += screenshot_backgroundColor.b < .9f ? .1f : -.1f;
		}

		screenshot_path = EditorUtility.SaveFilePanel("Save UV Template", Application.dataPath, "", "png");
	
		if(screenshot_path == "")
			return;

		screenshotStatus = ScreenshotStatus.Done;
		DoScreenshot();
	}

	/// Unity 5 changes the starting y position of a window now account for the tab
	float editorWindowTabOffset 
	{
		get
		{
			if( pb_Preferences_Internal.GetBool(pb_Constant.pbUVEditorFloating) )
				return 0;
			else
#if UNITY_4_6
				return 0;
#else
				return 11;
#endif
		}
	}

	void DoScreenshot()
	{
		switch(screenshotStatus)
		{
			// A new screenshot has been initiated
			case ScreenshotStatus.Done:
				curUvScale = uvGraphScale;
				curUvPosition = uvGraphOffset;

				uvGraphScale = screenshot_size / 256;

				// always begin texture grabs at bottom left
				uvGraphOffset = new Vector2(-ScreenRect.width/2f, ScreenRect.height/2f - editorWindowTabOffset);

				screenshot = new Texture2D(screenshot_size, screenshot_size);
				screenshot.hideFlags = (HideFlags)( 1 | 2 | 4 );
				screenshotStatus = ScreenshotStatus.PrepareCanvas;

				// set the current rect pixel boudns to the largest possible size.  if some parts are out of focus, they'll be grabbed in subsequent passes
				if( (bool) pb_Reflection.GetValue(this, "docked") )
					screenshotCanvasRect = new Rect(4, 2, (int)Mathf.Min(screenshot_size, ScreenRect.width - 4), (int)Mathf.Min(screenshot_size, ScreenRect.height - 2) );
				else
					screenshotCanvasRect = new Rect(0, 0, (int)Mathf.Min(screenshot_size, ScreenRect.width), (int)Mathf.Min(screenshot_size, ScreenRect.height) );

				screenshotTexturePosition = new Vector2(0,0);

				this.ShowNotification(new GUIContent("Rendering UV Graph\n..."));
				
				Repaint();

				return;

			case ScreenshotStatus.CanvasReady:
				
				// take screenshots vertically, then move right, repeat if necessary
				if(screenshotTexturePosition.y < screenshot_size)
				{
					screenshot.ReadPixels(screenshotCanvasRect, (int)screenshotTexturePosition.x, (int)screenshotTexturePosition.y);
					screenshotTexturePosition.y += screenshotCanvasRect.height;

					if(screenshotTexturePosition.y < screenshot_size)
					{
						// reposition canvas
						uvGraphOffset.y += screenshotCanvasRect.height;
						screenshotCanvasRect.height = (int)Mathf.Min(screenshot_size - screenshotTexturePosition.y, ScreenRect.height - 12);
						screenshotStatus = ScreenshotStatus.PrepareCanvas;
						Repaint();
						return;
					}
					else
					{
						screenshotTexturePosition.x += screenshotCanvasRect.width;

						if(screenshotTexturePosition.x < screenshot_size)
						{
							uvGraphOffset.x -= screenshotCanvasRect.width;	// move canvas offset to right
							uvGraphOffset.y = ScreenRect.height/2f - editorWindowTabOffset;	// reset canvas offset y value
							screenshotCanvasRect.width = (int)Mathf.Min(screenshot_size - screenshotTexturePosition.x, ScreenRect.width);
							screenshotTexturePosition.y = 0;
							screenshotStatus = ScreenshotStatus.PrepareCanvas;
							Repaint();
							return;
						}
					}
				}

				// reset the canvas to it's original position and scale
				uvGraphScale = curUvScale;
				uvGraphOffset = curUvPosition;

				this.RemoveNotification();
				screenshotStatus = ScreenshotStatus.RenderComplete;
				Repaint();
				break;

			case ScreenshotStatus.RenderComplete:

				if(screenshot_transparentBackground)
				{
					Color[] px = screenshot.GetPixels(0);

					for(int i = 0; i < px.Length; i++)

						if( Mathf.Abs(px[i].r - UV_FILL_COLOR.r) < .01f && 
							Mathf.Abs(px[i].g - UV_FILL_COLOR.g) < .01f && 
							Mathf.Abs(px[i].b - UV_FILL_COLOR.b) < .01f )
							px[i] = Color.clear;

					screenshot.SetPixels(px);
					screenshot.Apply();
				}

				this.minSize = Vector2.zero;
				this.maxSize = Vector2.one * 100000f;
				EditorApplication.delayCall += SaveUVRender;	// don't run the save image stuff in the UI loop
				screenshotStatus = ScreenshotStatus.Done;
				break;
		}
	}

	void SaveUVRender()
	{
		if(screenshot && screenshot_path != "")
		{
			pb_Editor_Utility.SaveTexture(screenshot, screenshot_path);
			DestroyImmediate(screenshot);
		}
	}
#endregion
}
}
#endif