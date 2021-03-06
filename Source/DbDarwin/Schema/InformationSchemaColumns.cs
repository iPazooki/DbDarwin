﻿using System;
using System.Xml.Serialization;

namespace DbDarwin.Schema
{
    [Serializable]
    public class InformationSchemaColumns
    {
        [XmlIgnore]
        public string TABLE_CATALOG { get; set; }

        [XmlIgnore]
        public string TABLE_SCHEMA { get; set; }

        [XmlIgnore]
        public string TABLE_NAME { get; set; }

        [XmlAttribute]
        public string COLUMN_NAME { get; set; }

        [XmlAttribute]
        public int ORDINAL_POSITION { get; set; }

        [XmlAttribute]
        public string COLUMN_DEFAULT { get; set; }

        [XmlAttribute]
        public string IS_NULLABLE { get; set; }

        [XmlAttribute]
        public string DATA_TYPE { get; set; }

        [XmlAttribute]
        public string CHARACTER_MAXIMUM_LENGTH { get; set; }

        [XmlAttribute]
        public string CHARACTER_OCTET_LENGTH { get; set; }

        [XmlAttribute]
        public string NUMERIC_PRECISION { get; set; }

        [XmlAttribute]
        public string NUMERIC_PRECISION_RADIX { get; set; }

        [XmlAttribute]
        public string NUMERIC_SCALE { get; set; }

        [XmlAttribute]
        public string DATETIME_PRECISION { get; set; }

        [XmlAttribute]
        public string CHARACTER_SET_CATALOG { get; set; }

        [XmlAttribute]
        public string CHARACTER_SET_SCHEMA { get; set; }

        [XmlAttribute]
        public string CHARACTER_SET_NAME { get; set; }

        [XmlAttribute]
        public string COLLATION_CATALOG { get; set; }

        [XmlAttribute]
        public string COLLATION_SCHEMA { get; set; }

        [XmlAttribute]
        public string COLLATION_NAME { get; set; }

        [XmlAttribute]
        public string DOMAIN_CATALOG { get; set; }

        [XmlAttribute]
        public string DOMAIN_SCHEMA { get; set; }

        [XmlAttribute]
        public string DOMAIN_NAME { get; set; }
    }
}
