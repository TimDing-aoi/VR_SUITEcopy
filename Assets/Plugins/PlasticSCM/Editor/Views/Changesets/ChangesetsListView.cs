﻿using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor.IMGUI.Controls;

using Codice.CM.Common;
using Codice.UI;
using Codice.UI.Tree;
using PlasticGui;
using PlasticGui.WorkspaceWindow.QueryViews;

namespace Codice.Views.Changesets
{
    internal class ChangesetsListView : TreeView
    {
        internal ChangesetsListView(
            ChangesetsListHeaderState headerState,
            List<string> columnNames,
            ChangesetsViewMenu menu,
            Action sizeChangedAction,
            Action selectionChangedAction,
            Action doubleClickAction)
            : base(new TreeViewState())
        {
            mColumnNames = columnNames;
            mMenu = menu;
            mSizeChangedAction = sizeChangedAction;
            mSelectionChangedAction = selectionChangedAction;
            mDoubleClickAction = doubleClickAction;

            multiColumnHeader = new MultiColumnHeader(headerState);
            multiColumnHeader.canSort = true;
            multiColumnHeader.sortingChanged += SortingChanged;

            rowHeight = UnityConstants.TREEVIEW_ROW_HEIGHT;
            showAlternatingRowBackgrounds = true;

            mCooldownFilterAction = new CooldownWindowDelayer(
                DelayedSearchChanged, UnityConstants.SEARCH_DELAYED_INPUT_ACTION_INTERVAL);

            mCooldownSelectionAction = new CooldownWindowDelayer(
                DelayedSelectionChanged, UnityConstants.SELECTION_DELAYED_INPUT_ACTION_INTERVAL);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            mCooldownSelectionAction.Ping();
        }

        public override IList<TreeViewItem> GetRows()
        {
            return mRows;
        }

        protected override TreeViewItem BuildRoot()
        {
            return new TreeViewItem(0, -1, string.Empty);
        }

        protected override IList<TreeViewItem> BuildRows(
            TreeViewItem rootItem)
        {
            if (mQueryResult == null)
            {
                ClearRows(rootItem, mRows);

                return mRows;
            }

            RegenerateRows(
                mListViewItemIds,
                mQueryResult.GetObjects(),
                rootItem, mRows);

            return mRows;
        }

        protected override void SearchChanged(string newSearch)
        {
            mCooldownFilterAction.Ping();
        }

        protected override void ContextClickedItem(int id)
        {
            mMenu.Popup();
            Repaint();
        }

        public override void OnGUI(Rect rect)
        {
            if (Event.current.type == EventType.Layout)
            {
                if (IsSizeChanged(treeViewRect, mLastRect))
                    mSizeChangedAction();
            }

            mLastRect = treeViewRect;

            base.OnGUI(rect);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (args.item is ChangesetListViewItem)
            {
                ChangesetsListViewItemGUI(
                    mQueryResult,
                    (ChangesetListViewItem)args.item,
                    args);
                return;
            }

            base.RowGUI(args);
        }

        protected override void DoubleClickedItem(int id)
        {
            if (!HasSelection())
                return;

            mDoubleClickAction();
        }

        internal void BuildModel(ViewQueryResult queryResult)
        {
            mListViewItemIds.Clear();

            mQueryResult = queryResult;
        }

        internal void Refilter()
        {
            if (mQueryResult == null)
                return;

            Filter filter = new Filter(searchString);
            mQueryResult.ApplyFilter(filter, mColumnNames);
        }

        internal void Sort()
        {
            if (mQueryResult == null)
                return;

            int sortedColumnIdx = multiColumnHeader.state.sortedColumnIndex;
            bool sortAscending = multiColumnHeader.IsSortedAscending(sortedColumnIdx);

            mQueryResult.Sort(
                mColumnNames[sortedColumnIdx],
                sortAscending);
        }

        internal List<RepositorySpec> GetSelectedRepositories()
        {
            List<RepositorySpec> result = new List<RepositorySpec>();

            IList<int> selectedIds = GetSelection();

            if (selectedIds.Count == 0)
                return result;

            foreach (KeyValuePair<object, int> item
                in mListViewItemIds.GetInfoItems())
            {
                if (!selectedIds.Contains(item.Value))
                    continue;

                RepositorySpec repSpec =
                    mQueryResult.GetRepositorySpec(item.Key);
                result.Add(repSpec);
            }

            return result;
        }

        internal List<RepObjectInfo> GetSelectedRepObjectInfos()
        {
            List<RepObjectInfo> result = new List<RepObjectInfo>();

            IList<int> selectedIds = GetSelection();

            if (selectedIds.Count == 0)
                return result;

            foreach (KeyValuePair<object, int> item
                in mListViewItemIds.GetInfoItems())
            {
                if (!selectedIds.Contains(item.Value))
                    continue;

                RepObjectInfo repObjectInfo =
                    mQueryResult.GetRepObjectInfo(item.Key);
                result.Add(repObjectInfo);
            }

            return result;
        }

        internal void SelectRepObjectInfos(
            List<RepObjectInfo> repObjectsToSelect)
        {
            List<int> idsToSelect = new List<int>();

            foreach (RepObjectInfo repObjectInfo in repObjectsToSelect)
            {
                int repObjectInfoId = GetTreeIdForItem(repObjectInfo);

                if (repObjectInfoId == -1)
                    continue;

                idsToSelect.Add(repObjectInfoId);
            }

            TableViewOperations.SetSelectionAndScroll(this, idsToSelect);
        }

        int GetTreeIdForItem(RepObjectInfo repObjectInfo)
        {
            foreach (KeyValuePair<object, int> item in mListViewItemIds.GetInfoItems())
            {
                RepObjectInfo currentRepObjectInfo =
                    mQueryResult.GetRepObjectInfo(item.Key);

                if (!currentRepObjectInfo.Equals(repObjectInfo))
                    continue;

                if (!currentRepObjectInfo.GUID.Equals(repObjectInfo.GUID))
                    continue;

                return item.Value;
            }

            return -1;
        }

        void DelayedSearchChanged()
        {
            Refilter();

            Sort();

            Reload();
        }

        void DelayedSelectionChanged()
        {
            if (!HasSelection())
                return;

            mSelectionChangedAction();
        }

        void SortingChanged(MultiColumnHeader multiColumnHeader)
        {
            Sort();

            Reload();
        }

        static void RegenerateRows(
            ListViewItemIds<object> listViewItemIds,
            List<object> objectInfos,
            TreeViewItem rootItem,
            List<TreeViewItem> rows)
        {
            ClearRows(rootItem, rows);

            if (objectInfos.Count == 0)
                return;

            foreach (object objectInfo in objectInfos)
            {
                int objectId;
                if (!listViewItemIds.TryGetInfoItemId(objectInfo, out objectId))
                    objectId = listViewItemIds.AddInfoItem(objectInfo);

                ChangesetListViewItem changesetListViewItem =
                    new ChangesetListViewItem(objectId, objectInfo);

                rootItem.AddChild(changesetListViewItem);
                rows.Add(changesetListViewItem);
            }
        }

        static void ClearRows(
            TreeViewItem rootItem,
            List<TreeViewItem> rows)
        {
            if (rootItem.hasChildren)
                rootItem.children.Clear();

            rows.Clear();
        }

        static void ChangesetsListViewItemGUI(
            ViewQueryResult queryResult,
            ChangesetListViewItem item,
            RowGUIArgs args)
        {
            for (int visibleColumnIdx = 0; visibleColumnIdx < args.GetNumVisibleColumns(); visibleColumnIdx++)
            {
                Rect cellRect = args.GetCellRect(visibleColumnIdx);

                if (visibleColumnIdx == 0)
                {
                    cellRect.x += UnityConstants.FIRST_COLUMN_WITHOUT_ICON_INDENT;
                    cellRect.width -= UnityConstants.FIRST_COLUMN_WITHOUT_ICON_INDENT;
                }

                ChangesetsListColumn column =
                    (ChangesetsListColumn)args.GetColumn(visibleColumnIdx);

                ChangesetsListViewItemCellGUI(
                    cellRect,
                    queryResult,
                    item,
                    column,
                    args.selected,
                    args.focused);
            }
        }

        static void ChangesetsListViewItemCellGUI(
            Rect rect,
            ViewQueryResult queryResult,
            ChangesetListViewItem item,
            ChangesetsListColumn column,
            bool isSelected,
            bool isFocused)
        {
            string columnText = RepObjectInfoView.GetColumnText(
                queryResult.GetRepositorySpec(item.ObjectInfo),
                queryResult.GetRepObjectInfo(item.ObjectInfo),
                ChangesetsListHeaderState.GetColumnName(column));

            DefaultGUI.Label(
                rect, columnText, isSelected, isFocused);
        }

        static bool IsSizeChanged(
            Rect currentRect, Rect lastRect)
        {
            if (currentRect.width != lastRect.width)
                return true;

            if (currentRect.height != lastRect.height)
                return true;

            return false;
        }

        Rect mLastRect;

        ListViewItemIds<object> mListViewItemIds = new ListViewItemIds<object>();
        List<TreeViewItem> mRows = new List<TreeViewItem>();

        ViewQueryResult mQueryResult;

        readonly CooldownWindowDelayer mCooldownFilterAction;
        readonly CooldownWindowDelayer mCooldownSelectionAction;
        readonly Action mDoubleClickAction;
        readonly Action mSelectionChangedAction;
        readonly Action mSizeChangedAction;
        readonly ChangesetsViewMenu mMenu;
        readonly List<string> mColumnNames;
    }
}
