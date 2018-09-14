using System;
using UnityEngine;

namespace UnityEditor.ProBuilder.UI
{
	/// <summary>
	/// Collection of commonly used styles in the editor.
	/// </summary>
	static class EditorStyles
	{
		static readonly Color k_TextColorWhiteNormal = new Color(0.7f, 0.7f, 0.7f, 1f);
		static readonly Color k_TextColorWhiteHover = new Color(0.7f, 0.7f, 0.7f, 1f);
		static readonly Color k_TextColorWhiteActive = new Color(0.5f, 0.5f, 0.5f, 1f);

		static bool s_Initialized;
		static GUIStyle s_ButtonStyle;
		static GUIStyle s_ToolbarHelpIcon;
		static GUIStyle s_SettingsGroup;
		static GUIStyle s_RowStyle;
		static GUIStyle s_HeaderLabel;
		static GUIStyle s_SceneTextBox;
		static GUIStyle s_IndentedSettingBlock;

		public static GUIStyle buttonStyle { get { Init(); return s_ButtonStyle; } }
		public static GUIStyle toolbarHelpIcon { get { Init(); return s_ToolbarHelpIcon; } }
		public static GUIStyle settingsGroup { get { Init(); return s_SettingsGroup; } }
		public static GUIStyle rowStyle { get { Init(); return s_RowStyle; } }
		public static GUIStyle headerLabel { get { Init(); return s_HeaderLabel; } }
		public static GUIStyle sceneTextBox { get { Init(); return s_SceneTextBox; } }
		public static GUIStyle indentedSettingBlock { get { Init(); return s_IndentedSettingBlock; } }

		static void Init()
		{
			if (s_Initialized)
				return;

			s_Initialized = true;

			s_ButtonStyle = new GUIStyle()
			{
				normal = new GUIStyleState()
				{
					background = IconUtility.GetIcon("Toolbar/Background/RoundedRect_Normal"),
					textColor = UnityEditor.EditorGUIUtility.isProSkin ? k_TextColorWhiteNormal : Color.black
				},
				hover = new GUIStyleState()
				{
					background = IconUtility.GetIcon("Toolbar/Background/RoundedRect_Hover"),
					textColor = UnityEditor.EditorGUIUtility.isProSkin ? k_TextColorWhiteHover : Color.black,
				},
				active = new GUIStyleState()
				{
					background = IconUtility.GetIcon("Toolbar/Background/RoundedRect_Pressed"),
					textColor = UnityEditor.EditorGUIUtility.isProSkin ? k_TextColorWhiteActive : Color.black,
				},
				alignment = ProBuilderEditor.s_IsIconGui ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft,
				border = new RectOffset(3, 3, 3, 3),
				stretchWidth = true,
				stretchHeight = false,
				margin = new RectOffset(4, 4, 4, 4),
				padding = new RectOffset(4, 4, 4, 4)
			};

			s_ToolbarHelpIcon = new GUIStyle()
			{
				margin = new RectOffset(0, 0, 0, 0),
				padding = new RectOffset(0, 0, 0, 0),
				alignment = TextAnchor.MiddleCenter,
				fixedWidth = 18,
				fixedHeight = 18
			};

			s_SettingsGroup = new GUIStyle()
			{
				normal = new GUIStyleState()
				{
					background = IconUtility.GetIcon("Toolbar/RoundedBorder")
				},
				hover = new GUIStyleState()
				{
					background = IconUtility.GetIcon("Toolbar/RoundedBorder")
				},
				active = new GUIStyleState()
				{
					background = IconUtility.GetIcon("Toolbar/RoundedBorder")
				},
				border = new RectOffset(3, 3, 3, 3),
				stretchWidth = true,
				stretchHeight = false,
				margin = new RectOffset(4, 4, 4, 4),
				padding = new RectOffset(4, 4, 4, 6)
			};

			s_RowStyle = new GUIStyle()
			{
				normal = new GUIStyleState() { background = UnityEditor.EditorGUIUtility.whiteTexture },
				stretchWidth = true,
				stretchHeight = false,
				margin = new RectOffset(4, 4, 4, 4),
				padding = new RectOffset(4, 4, 4, 4)
			};

			s_HeaderLabel = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
			{
				alignment = TextAnchor.LowerLeft,
				fontSize = 18,
				stretchWidth = true,
				stretchHeight = false
			};

			Font asap = FileUtility.LoadInternalAsset<Font>("About/Font/Asap-Regular.otf");
			if (asap != null)
				s_HeaderLabel.font = asap;

			s_SceneTextBox = new GUIStyle(GUI.skin.box)
			{
				wordWrap = false,
				richText = true,
				stretchWidth = false,
				stretchHeight = false,
				border = new RectOffset(2, 2, 2, 2),
				padding = new RectOffset(4, 4, 4, 4),
				alignment = TextAnchor.UpperLeft,
				normal = new GUIStyleState()
				{
					textColor = k_TextColorWhiteNormal,
					background = IconUtility.GetIcon("Scene/TextBackground")
				}
			};

			s_IndentedSettingBlock = new GUIStyle()
			{
				padding = new RectOffset(16, 0, 0, 0)
			};
		}

		public class IndentedBlock : IDisposable
		{
			public IndentedBlock()
			{
				UnityEditor.EditorGUIUtility.labelWidth -= indentedSettingBlock.padding.left - 4;
				GUILayout.BeginVertical(indentedSettingBlock);
			}

			public void Dispose()
			{
				GUILayout.EndVertical();
				UnityEditor.EditorGUIUtility.labelWidth += indentedSettingBlock.padding.left - 4;
			}
		}
	}
}
