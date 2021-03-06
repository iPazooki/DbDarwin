﻿using Olive;

namespace DbDarwin.Model.Command
{
    public class Transformation
    {
        public string CurrentDiffFile { get; set; }
        public string TableName { get; set; }
        public string FromName { get; set; }
        public string ToName { get; set; }
        public string MigrateSqlFile { get; set; }

        public bool IsValid => CurrentDiffFile.HasValue() &&
                               FromName.HasValue() &&
                               MigrateSqlFile.HasValue() &&
                               ToName.HasValue();
    }
}
