using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using hOOt;

namespace hOOt
{
    internal class BoolIndex
    {
        public BoolIndex(string path, string filename)
        {
            // create file
            _filename = filename;
            if (_filename.Contains(".") == false) _filename += ".idx";
            _path = path;
            if (_path.EndsWith(Path.DirectorySeparatorChar.ToString()) == false)
                _path += Path.DirectorySeparatorChar.ToString();

            if (File.Exists(_path + _filename))
                ReadFile();
        }

        private WAHBitArray _bits = new WAHBitArray();
        private string _filename;
        private string _path;
        private object _lock = new object();
        private bool _inMemory = false;

        public WAHBitArray GetBits()
        {
            return _bits.Copy();
        }

        public void Set(object key, int recnum)
        {
            _bits.Set(recnum, (bool)key);
        }

        public void FreeMemory()
        {
            // free memory
            _bits.FreeMemory();
        }

        public void Shutdown()
        {
            // shutdown
            if (_inMemory == false)
                WriteFile();
        }

        public void SaveIndex()
        {
            if (_inMemory == false)
                WriteFile();
        }

        public void InPlaceOR(WAHBitArray left)
        {
            _bits = _bits.Or(left);
        }

        private void WriteFile()
        {
            WAHBitArray.TYPE t;
            uint[] ints = _bits.GetCompressed(out t);
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)t);// write new format with the data type byte
            foreach (var i in ints)
            {
                bw.Write(i);
            }
            File.WriteAllBytes(_path + _filename, ms.ToArray());
        }

        private void ReadFile()
        {
            byte[] b = File.ReadAllBytes(_path + _filename);
            WAHBitArray.TYPE t = WAHBitArray.TYPE.WAH;
            int j = 0;
            if (b.Length % 4 > 0) // new format with the data type byte
            {
                t = (WAHBitArray.TYPE)Enum.ToObject(typeof(WAHBitArray.TYPE), b[0]);
                j = 1;
            }
            List<uint> ints = new List<uint>();
            for (int i = 0; i < b.Length / 4; i++)
            {
                ints.Add((uint)Helper.ToInt32(b, (i * 4) + j));
            }
            _bits = new WAHBitArray(t, ints.ToArray());
        }

        internal void FixSize(int size)
        {
            _bits.Length = size;
        }
    }
}
