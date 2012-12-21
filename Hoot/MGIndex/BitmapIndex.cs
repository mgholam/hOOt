using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

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

    internal class BitmapIndex
    {
        public BitmapIndex(string path, string filename)
        {
            _FileName = Path.GetFileNameWithoutExtension(filename);
            _Path = path;
            if (_Path.EndsWith(Path.DirectorySeparatorChar.ToString()) == false)
                _Path += Path.DirectorySeparatorChar.ToString();

            _recordFileRead = new FileStream(_Path + _FileName + _recExt, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            _recordFileWriteOrg = new FileStream(_Path + _FileName + _recExt, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            _bitmapFileWriteOrg = new FileStream(_Path + _FileName + _bmpExt, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            _bitmapFileRead = new FileStream(_Path + _FileName + _bmpExt, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            _bitmapFileWrite = new BufferedStream(_bitmapFileWriteOrg);
            _recordFileWrite = new BufferedStream(_recordFileWriteOrg);
            _bitmapFileWrite.Seek(0L, SeekOrigin.End);
            _lastBitmapOffset = _bitmapFileWrite.Length;
            _lastRecordNumber = (int)(_recordFileRead.Length / 8);
        }

        private string _recExt = ".mgbmr";
        private string _bmpExt = ".mgbmp";
        private string _FileName = "";
        private string _Path = "";
        private FileStream _bitmapFileWriteOrg;
        private BufferedStream _bitmapFileWrite;
        private FileStream _bitmapFileRead;
        private FileStream _recordFileRead;
        private FileStream _recordFileWriteOrg;
        private BufferedStream _recordFileWrite;
        private long _lastBitmapOffset = 0;
        private int _lastRecordNumber = 0;
        private object _lock = new object();
        private SafeDictionary<int, WAHBitArray> _cache = new SafeDictionary<int, WAHBitArray>();
        private SafeDictionary<int, long> _offsetCache = new SafeDictionary<int, long>();
        ILog log = LogManager.GetLogger(typeof(BitmapIndex));

        #region [  P U B L I C  ]
        public void Shutdown()
        {
            log.Debug("Shutdown BitmapIndex");
            bool d1 = false;
            bool d2 = false;
            Flush();
            if (_recordFileRead != null)
            {
                if (_recordFileWrite.Length == 0) d1 = true;
                if (_bitmapFileWrite.Length == 0) d2 = true;
                _recordFileRead.Close();
                _recordFileWrite.Close();
                _bitmapFileWrite.Close();
                _bitmapFileRead.Close();
                _bitmapFileWriteOrg.Close();
                _recordFileWriteOrg.Close();
                if (d1)
                    File.Delete(_Path + _FileName + _recExt);
                if (d2)
                    File.Delete(_Path + _FileName + _bmpExt);
                _recordFileRead = null;
                _recordFileWrite = null;
                _bitmapFileRead = null;
                _bitmapFileWrite = null;
                _recordFileWriteOrg = null;
                _bitmapFileWriteOrg = null;
            }
        }

        public void Flush()
        {
            if (_recordFileWrite != null)
                _recordFileWrite.Flush();
            if (_bitmapFileWrite != null)
                _bitmapFileWrite.Flush();
            if (_recordFileRead != null)
                _recordFileRead.Flush();
            if (_bitmapFileRead != null)
                _bitmapFileRead.Flush();
            if (_bitmapFileWriteOrg != null)
                _bitmapFileWriteOrg.Flush();
            if (_recordFileWriteOrg != null)
                _recordFileWriteOrg.Flush();
        }

        public int GetFreeRecordNumber()
        {
            int i = _lastRecordNumber++;

            _cache.Add(i, new WAHBitArray());
            return i;
        }

        public void Commit(bool freeMemory)
        {
            int[] keys = _cache.Keys();
            Array.Sort(keys);

            foreach (int k in keys)
            {
                var bmp = _cache[k];
                if (bmp.isDirty)
                {
                    SaveBitmap(k, bmp);
                    bmp.FreeMemory();
                    bmp.isDirty = false;
                }
            }
            Flush();
            if (freeMemory)
            {
                _cache = new SafeDictionary<int, WAHBitArray>();
            }
        }

        public void SetDuplicate(int bitmaprecno, int record)
        {
            WAHBitArray ba = null;

            ba = GetBitmap(bitmaprecno);

            ba.Set(record, true);
        }

        public WAHBitArray GetBitmap(int recno)
        {
            return internalGetBitmap(recno);
        }

        #endregion


        #region [  P R I V A T E  ]
        private object _readlock = new object();
        private WAHBitArray internalGetBitmap(int recno)
        {
            lock (_readlock)
            {
                WAHBitArray ba = new WAHBitArray();
                if (recno == -1)
                    return ba;

                if (_cache.TryGetValue(recno, out ba))
                {
                    return ba;
                }
                else
                {
                    long offset = 0;
                    if (_offsetCache.TryGetValue(recno, out offset) == false)
                    {
                        byte[] b = new byte[8];
                        long off = ((long)recno) * 8;
                        _recordFileRead.Seek(off, SeekOrigin.Begin);
                        _recordFileRead.Read(b, 0, 8);
                        offset = Helper.ToInt64(b, 0);
                        _offsetCache.Add(recno, offset);
                    }
                    ba = LoadBitmap(offset);
              
                    _cache.Add(recno, ba);

                    return ba;
                }
            }
        }

        private object _writelock = new object();
        private void SaveBitmap(int recno, WAHBitArray bmp)
        {
            lock (_writelock)
            {
                long offset = SaveBitmapToFile(bmp);
                long v;
                if (_offsetCache.TryGetValue(recno, out v))
                    _offsetCache[recno] = offset;
                else
                    _offsetCache.Add(recno, offset);

                long pointer = ((long)recno) * 8;
                _recordFileWrite.Seek(pointer, SeekOrigin.Begin);
                byte[] b = new byte[8];
                b = Helper.GetBytes(offset, false);
                _recordFileWrite.Write(b, 0, 8);
            }
        }

        //-----------------------------------------------------------------
        // BITMAP FILE FORMAT
        //    0  'B','M'
        //    2  uint count = 4 bytes
        //    6  Bitmap type :
        //                0 = int record list   
        //                1 = uint bitmap
        //                2 = rec# indexes
        //    7  '0'
        //    8  uint data
        //-----------------------------------------------------------------
        private long SaveBitmapToFile(WAHBitArray bmp)
        {
            long off = _lastBitmapOffset;
            WAHBitArray.TYPE t;
            uint[] bits = bmp.GetCompressed(out t);

            byte[] b = new byte[bits.Length * 4 + 8];
            // write header data
            b[0] = ((byte)'B');
            b[1] = ((byte)'M');
            Buffer.BlockCopy(Helper.GetBytes(bits.Length, false), 0, b, 2, 4);

            b[6] = (byte)t;
            b[7] = (byte)(0);

            for (int i = 0; i < bits.Length; i++)
            {
                byte[] u = Helper.GetBytes((int)bits[i], false);
                Buffer.BlockCopy(u, 0, b, i * 4 + 8, 4);
            }
            _bitmapFileWrite.Write(b, 0, b.Length);
            _lastBitmapOffset += b.Length;
            return off;
        }

        private WAHBitArray LoadBitmap(long offset)
        {
            WAHBitArray bc = new WAHBitArray();
            if (offset == -1)
                return bc;

            List<uint> ar = new List<uint>();
            WAHBitArray.TYPE type = WAHBitArray.TYPE.WAH;
            FileStream bmp = _bitmapFileRead;
            {
                bmp.Seek(offset, SeekOrigin.Begin);

                byte[] b = new byte[8];

                bmp.Read(b, 0, 8);
                if (b[0] == (byte)'B' && b[1] == (byte)'M' && b[7] == 0)
                {
                    type = (WAHBitArray.TYPE)Enum.ToObject(typeof(WAHBitArray.TYPE), b[6]);
                    int c = Helper.ToInt32(b, 2);
                    byte[] buf = new byte[c * 4];
                    bmp.Read(buf, 0, c * 4);
                    for (int i = 0; i < c; i++)
                    {
                        ar.Add((uint)Helper.ToInt32(buf, i * 4));
                    }
                }
            }
            bc = new WAHBitArray(type, ar.ToArray());

            return bc;
        }
        #endregion
    }
}
