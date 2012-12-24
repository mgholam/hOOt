using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace hOOt
{
    public class Document
    {
        public Document()
        {
            DocNumber = -1;
        }
        public Document(FileInfo fileinfo, string text)
        {
            FileName = fileinfo.FullName;
            ModifiedDate = fileinfo.LastWriteTime;
            FileSize = fileinfo.Length;
            Text = text;
            DocNumber = -1;
        }
        public int DocNumber { get; set; }
        [XmlIgnore]
        public string Text { get; set; }
        public string FileName { get; set; }
        public DateTime ModifiedDate { get; set; }
        public long FileSize;

        public override string ToString()
        {
            return FileName;
        }
    }

    internal enum OPERATION
    {
        AND,
        OR,
        ANDNOT
    }
}
