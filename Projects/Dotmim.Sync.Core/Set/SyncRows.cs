﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;

namespace Dotmim.Sync
{
    public class SyncRows : ICollection<SyncRow>, IList<SyncRow>
    {
        public SyncTable Table { get; set; }
        private List<SyncRow> rows = new List<SyncRow>();

        public SyncRows(SyncTable table) => this.Table = table;

        /// Since we don't serializer the reference to the schema, this method will reaffect the correct schema
        /// </summary>
        public void EnsureRows(SyncTable table)
        {
            this.Table = table;
            foreach (SyncRow row in this)
                row.Table = table;
        }

        /// <summary>
        /// Add a new buffer row
        /// </summary>
        public void Add(object[] row, DataRowState state = DataRowState.Unchanged)
        {
            var schemaRow = new SyncRow(this.Table, row, state);
            rows.Add(schemaRow);
        }

        /// <summary>
        /// Add a rows
        /// </summary>
        public void AddRange(IEnumerable<object[]> rows, DataRowState state = DataRowState.Unchanged)
        {
            foreach (var row in rows)
            {
                var schemaRow = new SyncRow(this.Table, row, state);
                this.rows.Add(schemaRow);
            }
        }

        public void AddRange(List<SyncRow> rows)
        {
            foreach(var item in rows)
            {
                TryEnsureData(item);
                item.Table = this.Table;
                this.rows.Add(item);

            }
        }
        /// <summary>
        /// Add a new row to the collection
        /// </summary>
        public void Add(SyncRow item)
        {
            TryEnsureData(item);
            item.Table = this.Table;
            this.rows.Add(item);
        }

        /// <summary>
        /// Gets the inner rows for serialization
        /// </summary>
        internal IEnumerable<object[]> ToEnumerable()
        {
            foreach (var row in this.rows)
                yield return row.ToArray();
        }

        /// <summary>
        /// Make a filter on primarykeys
        /// </summary>
        public IEnumerable<SyncRow> GetRowsByPrimaryKeys(SyncRow criteria)
        {
            // Get the primarykeys to get the ordinal
            var primaryKeysColumn = Table.GetPrimaryKeysColumns().ToList();
            var criteriaKeysColumn = criteria.Table.GetPrimaryKeysColumns().ToList();

            if (primaryKeysColumn.Count != criteriaKeysColumn.Count)
                throw new ArgumentOutOfRangeException($"Can't make a query on primary keys since number of primary keys columns in criterias is not matching the number of primary keys columns in this table");


            var filteredRows = this.rows.Where(itemRow =>
            {
                for (int i = 0; i < primaryKeysColumn.Count; i++)
                {
                    var syncColumn = primaryKeysColumn[i];

                    if (!criteria[syncColumn.ColumnName].Equals(itemRow[syncColumn.ColumnName]))
                        return false;
                }
                return true;
            });

            return filteredRows;
        }



        /// <summary>
        /// Ensure schema and data are correctly related
        /// </summary>
        private void TryEnsureData(SyncRow row)
        {
            if (row.Length != this.Table.Columns.Count)
                throw new Exception("The row length does not fit with the DataTable columns count");

            for (int i = 0; i < row.Length; i++)
            {
                var cell = row[i];

                // we can't check the value with the column type, so that's life, go next
                if (cell == null)
                    continue;

                var column = this.Table.Columns[i];
                var columnType = column.GetDataType();
                var cellType = cell.GetType();

                if (columnType.IsEquivalentTo(cellType))
                    continue;

                // if object, no need to verify anything on this column
                if (columnType == typeof(object))
                    continue;

                // everything can be convert to string, I guess :D
                if (columnType == typeof(string))
                    continue;

                var converter = GetConverter(columnType);

                if (converter != null && converter.CanConvertFrom(cellType))
                    continue;

                throw new Exception($"The type of column {columnType.Name} is not compatible with type of row index cell {cellType.Name}");
            }

        }



        /// <summary>
        /// Get type converter
        /// </summary>
        public static TypeConverter GetConverter(Type type)
        {
            var converter = TypeDescriptor.GetConverter(type);

            // Every object could use a TypeConverter, so we exclude it
            if (converter != null && converter.GetType() != typeof(TypeConverter) && converter.CanConvertTo(typeof(string)))
                return converter;

            return null;
        }

        /// <summary>
        /// Clear all rows
        /// </summary>
        public void Clear()
        {
            foreach (var row in rows)
            {
                row.Clear();
                row.Table = null;
            }

            rows.Clear();
        }


        public SyncRow this[int index] => rows[index];
        public int Count => rows.Count;
        public bool IsReadOnly => false;
        SyncRow IList<SyncRow>.this[int index]
        {
            get => this.rows[index];
            set => this.rows[index] = value;
        }
        public bool Remove(SyncRow item) => rows.Remove(item);
        public bool Contains(SyncRow item) => rows.Contains(item);
        public void CopyTo(SyncRow[] array, int arrayIndex) => rows.CopyTo(array, arrayIndex);
        public int IndexOf(SyncRow item) => rows.IndexOf(item);
        public void RemoveAt(int index) => rows.RemoveAt(index);
        public override string ToString() => this.rows.Count.ToString();
        public void Insert(int index, SyncRow item)
        {
            item.Table = this.Table;
            this.rows.Insert(index, item);
        }
        public IEnumerator<SyncRow> GetEnumerator() => rows.GetEnumerator();
        IEnumerator<SyncRow> IEnumerable<SyncRow>.GetEnumerator() => this.rows.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.rows.GetEnumerator();

      
    }
}