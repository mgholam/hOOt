using hOOt;
using System;
using System.IO;

namespace SampleApp
{
    /// <summary>
    /// Sample doc override 
    /// </summary>
    public class myDoc : Document
    {
        public myDoc(FileInfo fileinfo, string text) : base(fileinfo, text)
        {
            now = DateTime.Now;
        }

        // other data I want to save
        public DateTime now;
    }
}
