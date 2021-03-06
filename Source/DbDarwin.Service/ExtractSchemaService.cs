﻿using DbDarwin.Common;
using DbDarwin.Model;
using DbDarwin.Model.Command;
using DbDarwin.Model.Schema;
using Olive;
using PowerMapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace DbDarwin.Service
{
    public class ExtractSchemaService : IDisposable
    {
        private bool disposedValue;
        private readonly SqlConnection CurrentSqlConnection;
        public List<ConstraintInformationModel> ConstraintInformation { get; set; }
        /// <summary>
        /// All Table Extend Property
        /// </summary>
        public List<ExtendedProperty> ExtendProperties { get; set; }
        public List<IdentityData> IdentityColumns { get; set; }

        /// <summary>
        /// All Key Constraint
        /// </summary>
        public List<KeyConstraint> KeyConstraints { get; set; }
        /// <summary>
        /// All Objects from SQL
        /// </summary>
        public List<SqlObject> ObjectMapped { get; set; }
        /// <summary>
        /// All sys.columns from SQL
        /// </summary>
        public List<SystemColumns> SystemColumnsMapped { get; set; }
        /// <summary>
        /// All Index from SQL
        /// </summary>
        public List<Index> IndexMapped { get; set; }
        /// <summary>
        /// COLUMNS schema
        /// </summary>
        public List<Column> ColumnsMapped { get; set; }
        /// <summary>
        /// All index_columns from SQL
        /// </summary>
        public List<IndexColumns> IndexColumnsMapped { get; set; }
        /// <summary>
        /// All References from SQL
        /// </summary>
        public List<ForeignKey> ReferencesMapped { get; set; }

        public DataTable AllTables { get; set; }
        public ExtractSchema Model { get; set; }

        public XElement RootDatabase { get; set; }
        public XDocument Doc { get; set; }
        public Database Database { get; set; }

        public ExtractSchemaService(ExtractSchema model)
        {
            Model = model;
            CurrentSqlConnection = new System.Data.SqlClient.SqlConnection(model.ConnectionString);
            CurrentSqlConnection.Open();

            AllTables = CurrentSqlConnection.GetSchema("Tables");
            // Fetch All References from SQL
            ReferencesMapped = SqlService.LoadData<ForeignKey>(CurrentSqlConnection, "References", Properties.Resources.REFERENTIAL_CONSTRAINTS);
            // Fetch All index_columns from SQL
            IndexColumnsMapped = SqlService.LoadData<IndexColumns>(CurrentSqlConnection, "index_columns", "SELECT * FROM sys.index_columns");
            // fetch COLUMNS schema
            ColumnsMapped = SqlService.LoadData<Column>(CurrentSqlConnection, "Columns", "select * from INFORMATION_SCHEMA.COLUMNS");
            // Fetch All Index from SQL
            IndexMapped = SqlService.LoadData<Index>(CurrentSqlConnection, "index_columns", "SELECT * FROM sys.indexes");
            // Fetch All sys.columns from SQL
            SystemColumnsMapped = SqlService.LoadData<SystemColumns>(CurrentSqlConnection, "allSysColumns", "SELECT * FROM sys.columns");
            // Fetch All Objects from SQL
            ObjectMapped = SqlService.LoadData<SqlObject>(CurrentSqlConnection, "sys.object", "SELECT o.*, s.name as schemaName FROM sys.objects o join sys.schemas s on s.schema_id = o.schema_id");
            // Get All Key Constraint
            KeyConstraints = SqlService.LoadData<KeyConstraint>(CurrentSqlConnection, "keyConstraints", "SELECT * FROM [sys].[key_constraints]");
            // Get All Table Extend Property
            ExtendProperties = SqlService.LoadData<ExtendedProperty>(CurrentSqlConnection, "extendProperties", "SELECT [major_id] ,[name] ,[value] FROM [sys].[extended_properties] where minor_id = 0 and major_id <> 0");
            // Identity Columns
            IdentityColumns = SqlService.LoadData<IdentityData>(CurrentSqlConnection, "IdentityData", "select OBJECT_NAME(object_id) as TableName,OBJECT_SCHEMA_NAME(object_id) as SchemaName, * FROM sys.identity_columns");

            ConstraintInformation = (from ind in IndexMapped
                                     join ic in IndexColumnsMapped on new { ind.object_id, ind.index_id } equals new { ic.object_id, ic.index_id }
                                     join col in SystemColumnsMapped on new { ic.object_id, ic.column_id } equals new { col.object_id, col.column_id }
                                     select new ConstraintInformationModel { Index = ind, IndexColumn = ic, SystemColumn = col }).ToList();
            Model = model;

            Database = new Database();
            Doc = new XDocument {
                Declaration = new XDeclaration("1.0", "UTF-8", "true")
            };
            RootDatabase = new XElement("Database");
        }

        /// <summary>
        /// extract table schema
        /// </summary>
        /// <param name="model">Contain Connection string and output file</param>
        /// <returns>can be successful it is true</returns>
        public bool ExtractSchema(CompareType compare)
        {
            try
            {
                // Create Table Model
                if(AllTables.Rows.Count > 0)
                    Database.Tables = new List<Table>();

                foreach(var tableSchema in AllTables.Rows.Cast<DataRow>())
                {
                    var schemaTable = tableSchema["TABLE_SCHEMA"].ToString();
                    var tableName = tableSchema["TABLE_NAME"].ToString();
                    Console.WriteLine(schemaTable + @"." + tableName);
                    var tableId = ObjectMapped
                        .Where(x => x.schemaName.Equals(schemaTable, StringComparison.OrdinalIgnoreCase) && x.name.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.object_id)
                        .FirstOrDefault();


                    var newTable = new Table {
                        Name = tableName,
                        Schema = schemaTable,
                        Columns = FetchCulomns(tableName, schemaTable)
                    };



                    if(compare == CompareType.Schema)
                    {
                        newTable.Indexes = FetchIndexes(tableId);
                        newTable.PrimaryKey = FetchPrimary(tableId);
                        newTable.ForeignKeys = ReferencesMapped.Where(x =>
                            x.TABLE_SCHEMA == schemaTable && x.TABLE_NAME == tableName).ToList();
                    }
                    if(compare == CompareType.Data)
                        CheckReferenceData(tableId, newTable.Name, newTable.Schema, newTable.Columns);

                    Database.Tables.Add(newTable);
                }

                Database.Tables = Database.Tables?.OrderBy(x => x.FullName).ToList();

                // Create Serialize Object and save as XML file
                Doc.Add(RootDatabase);
                AddDataToTableAndSaveFile(Database, Doc, Model.OutputFile, compare);

            } catch(Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ForegroundColor = ConsoleColor.White;
                throw ex;
            }

            return true;
        }

        private List<Column> FetchCulomns(string tableName, string schemaTable)
        {
            var result = ColumnsMapped.Where(x => x.TABLE_NAME == tableName && x.TABLE_SCHEMA == schemaTable).ToList();
            foreach(var column in result)
            {
                var identity = IdentityColumns.FirstOrDefault(x =>
                x.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)
                && x.SchemaName.Equals(schemaTable, StringComparison.OrdinalIgnoreCase)
                && x.name.Equals(column.Name, StringComparison.OrdinalIgnoreCase));
                if(identity != null)
                {
                    column.IsIdentity = true;
                    column.SeedValue = identity.seed_value;
                    column.IncrementValue = identity.increment_value;
                }
            }

            return result;
        }

        private bool CheckReferenceData(long tableId, string tableName, string schema, List<Column> columns)
        {
            // If table is deference data
            // For check reference data
            bool existAddOrUpdate = false;
            if(ExtendProperties.Any(x =>
               x.major_id == tableId && x.name.Equals("ReferenceData", StringComparison.OrdinalIgnoreCase) &&
               x.value.Equals("Enum", StringComparison.OrdinalIgnoreCase)))
            {
                var data = SqlService.LoadData(CurrentSqlConnection, tableName, $"SELECT * FROM [{schema}].[{tableName}]");
                if(data.Rows.Count > 0)
                {
                    var tableElement = new XElement("Table");
                    var dataElement = new XElement("Data");
                    tableElement.SetAttributeValue("Name", tableName);
                    if(schema.ToLower() != "dbo")
                        tableElement.SetAttributeValue("Schema", schema);

                    foreach(var rowData in data.Rows.Cast<DataRow>())
                    {
                        var rowElement = new XElement("Row");
                        foreach(var column in data.Columns.Cast<DataColumn>().OrderBy(x => x.ColumnName))
                        {
                            if(column.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)
                                || rowData[column.ColumnName] == System.DBNull.Value)
                                continue;
                            existAddOrUpdate = true;
                            rowElement.SetAttributeValue(XmlConvert.EncodeName(column.ColumnName) ?? column.ColumnName, rowData[column.ColumnName].ToString());
                        }
                        existAddOrUpdate = true;
                        dataElement.Add(rowElement);
                    }

                    var columnType = GetColumnTypes(columns);
                    dataElement.AddFirst(columnType);
                    tableElement.Add(dataElement);
                    RootDatabase.Add(tableElement);
                }
            }
            return existAddOrUpdate;
        }

        public static XElement GetColumnTypes(List<Column> columns)
        {
            var result = new XElement("ColumnTypes");
            columns.ForEach(column => result.SetAttributeValue(XmlConvert.EncodeName(column.Name) ?? column.Name, column.DATA_TYPE));
            return result;
        }

        private List<Index> FetchIndexes(int tableId)
        {
            var indexRows = ConstraintInformation
                .Where(x => x.Index.object_id == tableId && x.Index.is_primary_key.Equals("False", StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.Index.name);
            var existsIndex = new List<Index>();
            foreach(var index in indexRows)
            {
                var resultIndex = index.FirstOrDefault()?.Index;
                if(resultIndex != null)
                    resultIndex.Columns = index.ToList().OrderBy(x => x.IndexColumn.key_ordinal)
                        .Select(x => x.SystemColumn.name)
                        .Aggregate((x, y) => x + "|" + y).Trim('|');
                existsIndex.Add(resultIndex);
            }

            return existsIndex;
        }

        private PrimaryKey FetchPrimary(int tableId)
        {
            var indexRows = ConstraintInformation
                .Where(x => x.Index.object_id == tableId && x.Index.is_primary_key.Equals("True", StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.Index.name);
            var index = indexRows.FirstOrDefault();
            if(index != null)
            {
                var resultIndex = index.FirstOrDefault()?.Index;
                if(resultIndex != null)
                    resultIndex.Columns = index.ToList().OrderBy(x => x.IndexColumn.key_ordinal)
                        .Select(x => x.SystemColumn.name)
                        .Aggregate((x, y) => x + "|" + y).Trim('|');
                var primaryKeys = resultIndex.MapTo<PrimaryKey>();
                var constraint = KeyConstraints.FirstOrDefault(x => x.name == primaryKeys.Name);
                if(constraint != null)
                    primaryKeys.is_system_named = constraint.is_system_named;
                return primaryKeys;
            }

            return null;
        }

        public static void AddDataToTableAndSaveFile(Database database, XDocument data, string fileOutput, CompareType type)
        {
            var doc = new XDocument();
            using(var writer = doc.CreateWriter())
            {
                var serializer = new XmlSerializer(typeof(Database));
                serializer.Serialize(writer, database);
            }

            var dataElements = data.Elements().FirstOrDefault()?.Elements(XName.Get("Table")).ToList();
            var schemaElements = doc.Elements().FirstOrDefault()?.Elements(XName.Get("Table")).ToList();
            if(schemaElements != null)
                foreach(var element in schemaElements)
                {
                    if(element.Name.ToString().ToLower() != "table")
                        continue;

                    var tableNameAttribute = element.Attribute(XName.Get("Name"));
                    var schemaAttribute = element.Attribute(XName.Get("Schema"));

                    var tableName = string.Empty;
                    var schemaName = string.Empty;
                    if(tableNameAttribute != null)
                        tableName = tableNameAttribute.Value;
                    schemaName = schemaAttribute?.Value;
                    if(type == CompareType.Data)
                    {
                        var foundData = (from d in dataElements
                                         where d.Attribute(XName.Get("Name"))?.Value == tableName
                                         && d.Attribute(XName.Get("Schema"))?.Value == schemaName
                                         select d).FirstOrDefault();
                        if(foundData != null)
                            element.Add(foundData.Elements(XName.Get("Data")));
                    }
                }

            var path = ConstantData.WorkingDir + fileOutput;
            doc.Save(path);
            Console.WriteLine("Saving To xml");
        }

        public static void SaveToFile(Database database, string fileOutput)
        {
            var ser = new XmlSerializer(typeof(Database));
            var sw2 = new StringWriter();
            ser.Serialize(sw2, database);

            var xml = sw2.ToString();

            var path = ConstantData.WorkingDir + "\\" + fileOutput;
            File.WriteAllText(path, xml);
            Console.WriteLine("Saving To xml");
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if(disposedValue)
                return;
            if(disposing)
            {
                CurrentSqlConnection.Close();
                CurrentSqlConnection.Dispose();
            }

            GC.Collect();
            disposedValue = true;
        }

        // ~ExtractSchemaService() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
