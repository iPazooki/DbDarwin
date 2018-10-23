﻿using DbDarwin.Model;
using DbDarwin.Model.Schema;
using KellermanSoftware.CompareNetObjects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using DbDarwin.Model.Command;
using GCop.Core;

namespace DbDarwin.Service
{
    public class CompareSchemaService
    {
        /// <summary>
        /// compare two xml file and create diff xml file
        /// </summary>
        /// <param name="currentFileName">Current XML File</param>
        /// <param name="newSchemaFilePath">New XML File Want To Compare</param>
        /// <param name="output">Output File XML diff</param>
        public static bool StartCompare(GenerateDiffFile model)
        {
            try
            {
                var oldSchema = LoadXMLFile(model.CurrentFile);
                var newSchema = LoadXMLFile(model.NewSchemaFile);

                CompareAndSave(oldSchema, newSchema, model.OutputFile);

                Console.WriteLine("Saving To xml");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ForegroundColor = ConsoleColor.White;
                throw;
            }
            finally
            {
                GC.Collect();
            }
            return true;
        }

        private static void CompareAndSave(IEnumerable<Table> oldSchema, IReadOnlyCollection<Table> newSchema, string output)
        {
            var doc = new XDocument
            {
                Declaration = new XDeclaration("1.0", "UTF-8", "true")
            };
            var arrayOfTable = new XElement("ArrayOfTable");

            foreach (var r1 in oldSchema)
            {
                var foundTable = newSchema.FirstOrDefault(x => x.Name == r1.Name);
                if (foundTable == null)
                {
                    // Must Delete
                }
                else
                {
                    var root = new XElement("Table");
                    root.SetAttributeValue(nameof(r1.Name), r1.Name);

                    var add = new XElement("add");
                    var navigatorAdd = add.CreateWriter();

                    var removeColumn = new XElement("remove");
                    var navigatorRemove = removeColumn.CreateWriter();

                    var updateElement = new XElement("update");
                    var navigatorUpdate = updateElement.CreateWriter();

                    GenerateDifference<Column>(r1.Column, foundTable.Column, navigatorAdd, navigatorRemove, navigatorUpdate);
                    GenerateDifference<Index>(r1.Index, foundTable.Index, navigatorAdd, navigatorRemove, navigatorUpdate);
                    GenerateDifference<ForeignKey>(r1.ForeignKey, foundTable.ForeignKey, navigatorAdd, navigatorRemove, navigatorUpdate);

                    if (!add.IsEmpty)
                        root.Add(add);

                    if (!removeColumn.IsEmpty)
                        root.Add(removeColumn);

                    if (!updateElement.IsEmpty)
                        root.Add(updateElement);

                    arrayOfTable.Add(root);
                }
            }

            doc.Add(arrayOfTable);
            doc.Save(output);
        }

        private static List<Table> LoadXMLFile(string currentFileName)
        {
            var serializer = new XmlSerializer(typeof(List<Table>));
            using (var reader = new StreamReader(currentFileName))
                return (List<Table>)serializer.Deserialize(reader);
        }

        /// <summary>
        /// Operation Set Name to Diff File 
        /// </summary>
        /// <param name="diffFile">Current XML Diff File</param>
        /// <param name="tableName">table name if want change column name</param>
        /// <param name="fromName">First Name</param>
        /// <param name="toName">Replace Name</param>
        /// <param name="diffFileOutput">Output new XML file diff</param>
        public static void TransformationDiffFile(Transformation model)
        {

            var serializer = new XmlSerializer(typeof(List<Table>));
            List<Table> currentDiffSchema = null;
            using (var reader = new StreamReader(model.CurrentDiffFile))
                currentDiffSchema = (List<Table>)serializer.Deserialize(reader);

            if (currentDiffSchema != null)
            {
                if (model.TableName.HasValue())
                {
                    var table = currentDiffSchema.FirstOrDefault(x =>
                        string.Equals(x.Name, model.FromName, StringComparison.CurrentCultureIgnoreCase));
                    if (table != null)
                        table.SetName = model.ToName;
                    else
                    {
                        table = new Table { Name = model.FromName, SetName = model.ToName };
                        currentDiffSchema.Add(table);
                    }
                }
                else
                {
                    var table = currentDiffSchema.FirstOrDefault(x =>
                        string.Equals(x.Name, model.TableName, StringComparison.CurrentCultureIgnoreCase));
                    if (table != null)
                    {
                        if (table.Update == null)
                        {
                            var column = new Column { COLUMN_NAME = model.FromName, SetName = model.ToName };
                            table.Update = new Table();
                            table.Update.Column.Add(column);
                        }
                        else
                        {
                            var column = table.Update.Column.FirstOrDefault(x =>
                                string.Equals(x.COLUMN_NAME, model.FromName,
                                    StringComparison.CurrentCultureIgnoreCase));
                            SetColumnName(column, model);
                        }
                    }
                }
            }


            var sw2 = new StringWriter();
            serializer.Serialize(sw2, currentDiffSchema);
            var xml = sw2.ToString();
            File.WriteAllText(model.MigrateSqlFile, xml);
        }

        private static void SetColumnName(Column column, Transformation model)
        {
            if (column != null) column.SetName = model.ToName;
        }

        /// <summary>
        /// compare objects
        /// </summary>
        /// <typeparam name="T">Type can Column , Index , ForeignKey</typeparam>
        /// <param name="doc">must be current XDocument</param>
        /// <param name="root">root xml element</param>
        /// <param name="currentList">Current Diff List Data</param>
        /// <param name="newList">must be compare data</param>
        /// <param name="navigatorAdd">refers to add element XML</param>
        /// <param name="navigatorRemove">refers to remove element XML</param>
        /// <param name="navigatorUpdate">refers to update element XML</param>
        public static void GenerateDifference<T>(List<T> currentList, List<T> newList,
            XmlWriter navigatorAdd, XmlWriter navigatorRemove, XmlWriter navigatorUpdate)
        {
            var emptyNamespaces = new XmlSerializerNamespaces(new[] {
                XmlQualifiedName.Empty,
            });

            // Detect new sql object like as INDEX , Column , REFERENTIAL_CONSTRAINTS 
            var mustAdd = FindNewComponent<T>(currentList, newList);

            // Add new objects to xml
            foreach (T sqlObject in mustAdd)
            {
                using (var writer = navigatorAdd)
                {
                    var serializer1 = new XmlSerializer(sqlObject.GetType());
                    writer.WriteWhitespace("");
                    serializer1.Serialize(writer, sqlObject, emptyNamespaces);
                    writer.Close();
                }
            }

            var compareLogic = new CompareLogic
            {
                Config = { MaxDifferences = int.MaxValue }
            };

            // Detect Sql Objects Changes
            foreach (T c1 in currentList)
            {
                object found = null;
                if (typeof(T) == typeof(Column))
                    found = newList.Cast<Column>().FirstOrDefault(x => x.Name == c1.GetType().GetProperty("Name").GetValue(c1).ToString());
                else if (typeof(T) == typeof(Index))
                    found = newList.Cast<Index>().FirstOrDefault(x => x.Name == c1.GetType().GetProperty("Name").GetValue(c1).ToString());
                else if (typeof(T) == typeof(ForeignKey))
                    found = newList.Cast<ForeignKey>().FirstOrDefault(x => x.Name == c1.GetType().GetProperty("Name").GetValue(c1).ToString());

                var foundObject = (T)Convert.ChangeType(found, typeof(T));
                if (foundObject == null)
                {
                    using (var writer = navigatorRemove)
                    {
                        var serializer1 = new XmlSerializer(c1.GetType());
                        writer.WriteWhitespace("");
                        serializer1.Serialize(writer, c1, emptyNamespaces);
                        writer.Close();
                    }
                }
                else
                {
                    var result = compareLogic.Compare(c1, foundObject);
                    if (!result.AreEqual)
                    {
                        using (var writer = navigatorUpdate)
                        {
                            var serializer1 = new XmlSerializer(foundObject.GetType());
                            writer.WriteWhitespace("");
                            serializer1.Serialize(writer, foundObject, emptyNamespaces);
                            writer.Close();
                        }
                    }
                }
            }
        }

        private static List<T> FindNewComponent<T>(List<T> currentList, List<T> newList)
        {
            object tempAdd = null;
            if (typeof(T) == typeof(Column))
                tempAdd = newList.Cast<Column>()
                    .Except(x => currentList.Cast<Column>().Select(c => c.COLUMN_NAME).ToList().Contains(x.COLUMN_NAME)).ToList();
            else if (typeof(T) == typeof(Index))
                tempAdd = newList.Cast<Index>()
                    .Except(x => currentList.Cast<Index>().Select(c => c.name).ToList().Contains(x.name)).ToList();
            else if (typeof(T) == typeof(ForeignKey))
                tempAdd = newList.Cast<ForeignKey>()
                    .Except(x => currentList.Cast<ForeignKey>().Select(c => c.CONSTRAINT_NAME).ToList().Contains(x.CONSTRAINT_NAME)).ToList();
            return (List<T>)Convert.ChangeType(tempAdd, typeof(List<T>));
        }
    }
}