﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace EditorGUITable
{

	/// <summary>
	/// Main Class of the Table Plugin.
	/// This contains static functions to draw a table, from the most basic
	/// to the most customizable.
	/// </summary>
	public static class GUITable
	{

		const string FULL_VERSION_URL = "https://assetstore.unity.com/packages/tools/gui/editor-gui-table-108795";

		/// <summary>
		/// Draw a table just from the collection's property.
		/// This will create columns for all the visible members in the elements' class,
		/// similar to what Unity would show in the classic vertical collection display, but as a table instead.
		/// </summary>
		/// <returns>The updated table state.</returns>
		/// <param name="rect">The table's containing rectangle.</param>
		/// <param name="tableState">The Table state.</param>
		/// <param name="collectionProperty">The serialized property of the collection.</param>
		/// <param name="options">The table options.</param>
		public static GUITableState DrawTable (
			Rect rect,
			GUITableState tableState,
			SerializedProperty collectionProperty,
			params GUITableOption[] options) 
		{
			List<string> properties = new List<string>();
			string firstElementPath = collectionProperty.propertyPath + ".Array.data[0]";
			foreach (SerializedProperty prop in collectionProperty.serializedObject.FindProperty(firstElementPath))
			{
				string subPropName = prop.propertyPath.Substring(firstElementPath.Length + 1);
				// Avoid drawing properties more than 1 level deep
				if (!subPropName.Contains("."))
					properties.Add (subPropName);
			}
			return DrawTable (rect, tableState, collectionProperty, properties, options);
		}

		/// <summary>
		/// Draw a table using just the paths of the properties to display.
		/// This will create columns automatically using the property name as title, and will create
		/// PropertyCell instances for each element.
		/// </summary>
		/// <returns>The updated table state.</returns>
		/// <param name="rect">The table's containing rectangle.</param>
		/// <param name="tableState">The Table state.</param>
		/// <param name="collectionProperty">The serialized property of the collection.</param>
		/// <param name="properties">The paths (names) of the properties to display.</param>
		/// <param name="options">The table options.</param>
		public static GUITableState DrawTable (
			Rect rect,
			GUITableState tableState,
			SerializedProperty collectionProperty, 
			List<string> properties, 
			params GUITableOption[] options) 
		{
			List<SelectorColumn> columns = properties.Select(prop => (SelectorColumn)new SelectFromPropertyNameColumn(
				prop, ObjectNames.NicifyVariableName (prop))).ToList();

			return DrawTable (rect, tableState, collectionProperty, columns, options);
		}

		/// <summary>
		/// Draw a table from the columns' settings, the path for the corresponding properties and a selector function
		/// that takes a SerializedProperty and returns the TableCell to put in the corresponding cell.
		/// </summary>
		/// <returns>The updated table state.</returns>
		/// <param name="rect">The table's containing rectangle.</param>
		/// <param name="tableState">The Table state.</param>
		/// <param name="collectionProperty">The serialized property of the collection.</param>
		/// <param name="columns">The Selector Columns.</param>
		/// <param name="options">The table options.</param>
		public static GUITableState DrawTable (
			Rect rect,
			GUITableState tableState,
			SerializedProperty collectionProperty, 
			List<SelectorColumn> columns, 
			params GUITableOption[] options) 
		{
			GUITableEntry tableEntry = new GUITableEntry (options);
			List<List<TableCell>> rows = new List<List<TableCell>>();
			for (int i = 0 ; i < collectionProperty.arraySize ; i++)
			{
				SerializedProperty sp = collectionProperty.serializedObject.FindProperty (string.Format ("{0}.Array.data[{1}]", collectionProperty.propertyPath, i));
				if (tableEntry.filter != null && !tableEntry.filter (sp))
					continue;
				List<TableCell> row = new List<TableCell>();
				foreach (SelectorColumn col in columns)
				{
					row.Add ( col.GetCell (sp));
				}
				rows.Add(row);
			}

			return DrawTable (rect, tableState, columns.Select((col) => (TableColumn) col).ToList(), rows, collectionProperty, options);
		}

		/// <summary>
		/// Draw a table completely manually.
		/// Each cell has to be created and given as parameter in cells.
		/// A collectionProperty is needed for reorderable tables. Use an overload with a collectionProperty.
		/// </summary>
		/// <returns>The updated table state.</returns>
		/// <param name="rect">The table's containing rectangle.</param>
		/// <param name="tableState">The Table state.</param>
		/// <param name="columns">The Columns of the table.</param>
		/// <param name="cells">The Cells as a list of rows.</param>
		/// <param name="options">The table options.</param>
		public static GUITableState DrawTable (
			Rect rect,
			GUITableState tableState,
			List<TableColumn> columns, 
			List<List<TableCell>> cells, 
			params GUITableOption[] options)
		{
			return DrawTable(rect, tableState, columns, cells, null, options);
		}

		// Used for ReorderableList's callbacks access
		static List<List<TableCell>> orderedRows;
		static List<List<TableCell>> staticCells;

		/// <summary>
		/// Draw a table completely manually.
		/// Each cell has to be created and given as parameter in cells.
		/// </summary>
		/// <returns>The updated table state.</returns>
		/// <param name="rect">The table's containing rectangle.</param>
		/// <param name="tableState">The Table state.</param>
		/// <param name="columns">The Columns of the table.</param>
		/// <param name="cells">The Cells as a list of rows.</param>
		/// <param name="collectionProperty">The SerializeProperty of the collection. This is useful for reorderable tables.</param>
		/// <param name="options">The table options.</param>
		public static GUITableState DrawTable (
			Rect rect,
			GUITableState tableState,
			List<TableColumn> columns, 
			List<List<TableCell>> cells, 
			SerializedProperty collectionProperty,
			params GUITableOption[] options)
		{

			GUITableEntry tableEntry = new GUITableEntry (options);

			if (tableState == null)
				tableState = new GUITableState();
			
			if (tableEntry.reorderable)
			{
				if (collectionProperty == null)
				{
					Debug.LogError ("The collection's serialized property is needed to draw a reorderable table.");
					return tableState;
				}

				staticCells = cells;
			
				if (tableState.reorderableList == null)
				{
					ReorderableList list = new ReorderableList (
						collectionProperty.serializedObject, 
						collectionProperty,
						true, true, true, true);

					list.drawElementCallback = (Rect r, int index, bool isActive, bool isFocused) => {
						DrawLine (tableState, columns, orderedRows[index], r.xMin + (list.draggable ? 0 : 14), r.yMin, tableEntry.rowHeight, tableEntry);
					};

					list.elementHeight = tableEntry.rowHeight;

					list.drawHeaderCallback = (Rect r) => { 
						DrawHeaders(r, tableState, columns, r.xMin + 12, r.yMin, tableEntry); 
					};

					list.onRemoveCallback = (l) => 
					{
						l.serializedProperty.DeleteArrayElementAtIndex (staticCells.IndexOf (orderedRows[l.index]));
					};

					tableState.SetReorderableList (list);
				}
			}
			
			tableState.CheckState(columns, tableEntry, rect.width);

			orderedRows = cells;
			if (tableState.sortByColumnIndex >= 0)
			{
				if (tableState.sortIncreasing)
					orderedRows = cells.OrderBy (row => row [tableState.sortByColumnIndex]).ToList();
				else
					orderedRows = cells.OrderByDescending (row => row [tableState.sortByColumnIndex]).ToList();
			}

			if (tableEntry.reorderable)
			{
				Rect listRect = new Rect (rect.x, rect.y, tableState.totalWidth + 23f, cells.Count * tableEntry.rowHeight + EditorGUIUtility.singleLineHeight);
				tableState.reorderableList.DoList(listRect);
				collectionProperty.serializedObject.ApplyModifiedProperties ();
				if (tableEntry.demoVersion)
					DrawFullVersionButton (listRect);
				return tableState;
			}


			float rowHeight = tableEntry.rowHeight;

			float currentX = rect.x;
			float currentY = rect.y;

			bool displayScrollView = tableState.totalWidth > rect.width && tableEntry.allowScrollView;

			tableState.RightClickMenu (columns, rect);

			DrawHeaders(rect, tableState, columns, currentX - tableState.scrollPos.x, currentY, tableEntry);

			GUI.enabled = true;

			currentY += EditorGUIUtility.singleLineHeight;

			if (tableEntry.allowScrollView)
			{
				tableState.scrollPos = GUI.BeginScrollView (
					new Rect (currentX, currentY, rect.width, Mathf.Min (rect.height, Screen.height / EditorGUIUtility.pixelsPerPoint - rect.y - 40)),
					tableState.scrollPos, 
					new Rect(0f, 0f, tableState.totalWidth, tableEntry.rowHeight * cells.Count));
				currentX = 0f;
				currentY = 0f;
			}

			foreach (List<TableCell> row in orderedRows)
			{
				currentX = tableEntry.allowScrollView ? 0 : rect.x;
				DrawLine (tableState, columns, row, currentX, currentY, rowHeight, tableEntry);
				currentY += rowHeight;
			}

			GUI.enabled = true;

			if (tableEntry.allowScrollView)
			{
				GUI.EndScrollView ();
			}

			tableState.Save();

			if (tableEntry.demoVersion)
			{
				DrawFullVersionButton (new Rect (rect.x, rect.y, tableState.totalWidth, cells.Count * tableEntry.rowHeight + EditorGUIUtility.singleLineHeight));
			}

			return tableState;
		}

	

		public static void DrawHeaders (
			Rect rect,
			GUITableState tableState,
			List<TableColumn> columns,
			float currentX,
			float currentY,
			GUITableEntry tableEntry)
		{
			for (int i = 0 ; i < columns.Count ; i++)
			{
				TableColumn column = columns[i];
				if (!tableState.columnVisible [i])
					continue;
				string columnName = column.title;
				if (tableState.sortByColumnIndex == i)
				{
					if (tableState.sortIncreasing)
						columnName += " " + '\u25B2'.ToString();
					else
						columnName += " " + '\u25BC'.ToString();
				}

				GUI.enabled = true;

				tableState.ResizeColumn (i, currentX, rect);

				if (tableEntry.demoVersion)
					GUI.enabled = false;
				else
					GUI.enabled = column.entry.enabledTitle;

				if (GUI.Button(new Rect(currentX, currentY, tableState.columnSizes[i]+4, EditorGUIUtility.singleLineHeight), columnName, EditorStyles.miniButtonMid) && column.entry.isSortable)
				{
					if (tableState.sortByColumnIndex == i && tableState.sortIncreasing)
					{
						tableState.sortIncreasing = false;
					}
					else if (tableState.sortByColumnIndex == i && !tableState.sortIncreasing)
					{
						tableState.sortByColumnIndex = -1;
					}
					else
					{
						tableState.sortByColumnIndex = i;
						tableState.sortIncreasing = true;
					}
				}

				currentX += tableState.columnSizes[i] + 4f;
			}
		}

		public static void DrawLine (
			GUITableState tableState,
			List<TableColumn> columns,
			List<TableCell> row, 
			float currentX,
			float currentY,
			float rowHeight,
			GUITableEntry tableEntry)
		{

			for (int i = 0 ; i < row.Count ; i++)
			{
				if (i >= columns.Count)
				{
					Debug.LogWarning ("The number of cells in this row is more than the number of columns");
					continue;
				}
				if (!tableState.columnVisible [i])
					continue;
				TableColumn column = columns [i];
				TableCell property = row[i];
				if (tableEntry.demoVersion)
					GUI.enabled = false;
				else
					GUI.enabled = column.entry.enabledCells;	
				property.DrawCell (new Rect(currentX, currentY, tableState.columnSizes[i], rowHeight));
				currentX += tableState.columnSizes[i] + 4f;
			}
		}

		static void DrawFullVersionButton (Rect tableRect)
		{
			GUI.enabled = true;
			if (goodButton (new Rect (tableRect.center.x - 100f, tableRect.center.y - 30f, 200f, 60f), "Get the Full Version"))
				Application.OpenURL (FULL_VERSION_URL);
		}



		static bool goodButton(Rect bounds, string caption) {
			GUIStyle btnStyle = GUI.skin.FindStyle("button");
			int controlID = GUIUtility.GetControlID(bounds.GetHashCode(), FocusType.Passive);

			bool isMouseOver = bounds.Contains(Event.current.mousePosition);
			bool isDown = GUIUtility.hotControl == controlID;

			if (GUIUtility.hotControl != 0 && !isDown) {
				// ignore mouse while some other control has it
				// (this is the key bit that GUI.Button appears to be missing)
				isMouseOver = false;
			}

			if (Event.current.type == EventType.Repaint) {
				btnStyle.Draw(bounds, new GUIContent(caption), isMouseOver, isDown, false, false);
			}
			switch (Event.current.GetTypeForControl(controlID)) {
				case EventType.mouseDown:
					if (isMouseOver) {  // (note: isMouseOver will be false when another control is hot)
						GUIUtility.hotControl = controlID;
					}
					break;

				case EventType.mouseUp:
					if (GUIUtility.hotControl == controlID) GUIUtility.hotControl = 0;
					if (isMouseOver) return true;
					break;
			}

			return false;
		}

	}
}
