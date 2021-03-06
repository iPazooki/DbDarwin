﻿using DbDarwin.Model.Schema;
using Olive;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace DbDarwin.Service
{
    public static class XmlExtention
    {
        public static XElement ToElement(this IDictionary<string, object> rows, string node)
        {
            var result = new XElement(node);
            foreach (var column in rows)
                result.SetAttributeValue(XmlConvert.EncodeName(column.Key) ?? column.Key, column.Value.ToString());
            return result;
        }

        public static void Serialize(this XmlWriter writer, object element)
        {
            var emptyNamespaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
            var serializer1 = new XmlSerializer(element.GetType());
            writer.WriteWhitespace("");
            serializer1.Serialize(writer, element, emptyNamespaces);
        }

        public static List<IDictionary<string, object>> ToDictionaryList(this TableData data)
        {
            var dictionary = new List<IDictionary<string, object>>();
            if (data == null)
                return dictionary;
            return data.Rows.ToDictionaryList();
        }

        public static List<IDictionary<string, object>> ToDictionaryList(this List<dynamic> data)
        {
            var result = new List<IDictionary<string, object>>();
            foreach (var o in data.Cast<XmlNode[]>())
                result.Add(o.ToDictionary());
            return result;
        }

        public static IDictionary<string, object> ToDictionary(this XmlNode[] data)
        {
            var result = new ExpandoObject();
            foreach (var node in data)
                AddProperty(result, XmlConvert.DecodeName(node.Name), node.InnerText);
            return result;
        }
        public static IDictionary<string, object> ToDictionary(dynamic data)
        {
            var result = new ExpandoObject();
            foreach (var node in data)
                AddProperty(result, XmlConvert.DecodeName(node.Name), node.InnerText);
            return result;
        }
        //https://www.oreilly.com/learning/building-c-objects-dynamically
        public static void AddProperty(ExpandoObject expando, string propertyName, object propertyValue)
        {
            // ExpandoObject supports IDictionary so we can extend it like this
            var expandoDict = expando as IDictionary<string, object>;
            if (expandoDict.ContainsKey(propertyName))
                expandoDict[propertyName] = propertyValue;
            else
                expandoDict.Add(propertyName, propertyValue);
        }
    }
}
