﻿using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using ProBuilder2.Common;
using ProBuilder2.Interface;

namespace ProBuilder2.EditorCommon
{
	[System.Serializable]
	public class pb_EditorToolbar : ScriptableObject
	{
		const int TOOLTIP_OFFSET = 4;

		[SerializeField] EditorWindow window;

		[SerializeField] List<pb_MenuAction> actions;

		public void InitWindowProperties(EditorWindow win)
		{
			win.wantsMouseMove = true;
			win.autoRepaintOnSceneChange = true;
			win.minSize = actions[0].GetSize() + new Vector2(6, 6);
			this.window = win;
		}

		void OnEnable()
		{
			actions = pb_EditorToolbarLoader.GetActions();
			pb_Editor.OnSelectionUpdate -= OnElementSelectionChange;
			pb_Editor.OnSelectionUpdate += OnElementSelectionChange;
		}

		void OnDisable()
		{
			pb_Editor.OnSelectionUpdate -= OnElementSelectionChange;
		}

		void OnElementSelectionChange(pb_Object[] selection)
		{
			if(!window)
				GameObject.DestroyImmediate(this);
			else
				window.Repaint();
		}

		Vector2 scroll = Vector2.zero;

		private void ShowTooltip(Rect rect, pb_MenuAction action, Vector2 scrollOffset)
		{
			Vector2 size = EditorStyles.boldLabel.CalcSize( pb_GUI_Utility.TempGUIContent(action.tooltip) );
			size += new Vector2(8,8);

			Vector2 p = new Vector2(
				(window.position.x + rect.x + rect.width + TOOLTIP_OFFSET) - scrollOffset.x,
				(window.position.y + rect.y + TOOLTIP_OFFSET) - scrollOffset.y);

			pb_TooltipWindow.Show(p, action.tooltip);
		}

		public void OnGUI()
		{
			Event e = Event.current;

			int max = (int)window.position.width - 24;
			int rows = System.Math.Max(max / (int)actions[0].GetSize().x, 1);

			int i = 1;

			scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUIStyle.none, GUIStyle.none);
			bool tooltipShown = false;

			GUILayout.BeginHorizontal();

			foreach(pb_MenuAction action in actions)
			{
				action.DoButton();

				Rect buttonRect = GUILayoutUtility.GetLastRect();

				if( e.shift &&
					e.type != EventType.Layout &&
					buttonRect.Contains(e.mousePosition) )
				{
					tooltipShown = true;
					ShowTooltip(buttonRect, action, scroll);
				}

				if(++i >= rows)
				{
					i = 1;
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
				}
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			GUILayout.EndScrollView();

			if((e.type == EventType.Repaint || e.type == EventType.MouseMove) && !tooltipShown)
				pb_TooltipWindow.Hide();

			if( (EditorWindow.mouseOverWindow == this && e.delta.sqrMagnitude > .001f) || e.isMouse )
				window.Repaint();
		}
	}
}