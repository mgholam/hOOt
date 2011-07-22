using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.IO;
using System.Xml.Serialization;
using System.Threading;

namespace hOOt
{
    public class Hoot
    {
        private int MAX_STRING_LENGTH_IGNORE = 60;

        public Hoot(string IndexPath, string FileName)
        {
            _Path = IndexPath;
            _FileName = FileName;
            if (_Path.EndsWith("\\") == false) _Path += "\\";
            Directory.CreateDirectory(IndexPath);
            LogManager.Configure(_Path + _FileName + ".txt", 200, false);
            _log.Debug("\r\n\r\n");
            _log.Debug("Starting hOOt....");
            _log.Debug("Storage Folder = " + _Path);

            _docs = new StorageFile(_Path + _FileName + ".docs", 4);
            // read hash index file
            _hash = new Hash(_Path + _FileName + ".idx", 255, 10, false, 1009);
            _hash.InMemory = true;
            // read doc count
            _lastDocNum = (int)_hash.Count();
            // read words
            LoadWords();
            // read deleted
            ReadDeleted();
            // open bitmap index
            _bitmapFile = new FileStream(_Path + _FileName + ".bitmap", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            _lastBitmapOffset = _bitmapFile.Seek(0L, SeekOrigin.End);

            //writewordstodisk();
        }

        //private void writewordstodisk()
        //{
        //    StringBuilder sb = new StringBuilder();
        //    foreach (var c in _index.Keys)
        //        sb.AppendLine(c);

        //    File.WriteAllText("words.txt", sb.ToString());
        //}

        private ILog _log = LogManager.GetLogger(typeof(Hoot));
        private Hash _hash;
        int _lastDocNum = 0;
        private string _FileName = "words";
        private string _Path = "";
        private Dictionary<string, Cache> _index = new Dictionary<string, Cache>(100000);
        private WAHBitArray _deleted = new WAHBitArray();
        private StorageFile _docs;
        private bool _internalOP = false;
        private object _lock = new object();
        private FileStream _bitmapFile;
        private long _lastBitmapOffset = 0;

        public int WordCount()
        {
            return _index.Count;
        }

        public int DocumentCount
        {
            get
            {
                return _lastDocNum;
            }
        }

        public void FreeMemory(bool freecache)
        {
            lock (_lock)
            {
                _internalOP = true;
                _log.Debug("freeing memory");
                // free deleted
                _deleted.FreeMemory();

                // clear hash
                _hash.SaveIndex();

                // free bitmap memory
                foreach (var v in _index.Values)
                {
                    if (freecache)
                    {
                        long off = SaveBitmap(v.GetCompressedBits(), v.LastBitSaveLength, v.FileOffset);
                        v.isDirty = false;
                        v.FileOffset = off;
                        v.FreeMemory(true);
                    }
                    else
                        v.FreeMemory(false);
                }
                _internalOP = false;
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                _internalOP = true;
                InternalSave();
                _internalOP = false;
            }
        }

        public void Index(int recordnumber, string text)
        {
            while (_internalOP) Thread.Sleep(50);

            AddtoIndex(recordnumber, text);
        }

        public int Index(Document doc, bool deleteold)
        {
            while (_internalOP) Thread.Sleep(50);

            _log.Debug("indexing doc : " + doc.FileName);
            DateTime dt = FastDateTime.Now;

            if (deleteold && doc.DocNumber > -1)
                _deleted.Set(doc.DocNumber, true);

            if (deleteold == true || doc.DocNumber == -1)
                doc.DocNumber = _lastDocNum++;

            // save doc to disk
            string dstr = fastJSON.JSON.Instance.ToJSON(doc);
            _docs.WriteData(Helper.GetBytes(doc.DocNumber, false), Helper.GetBytes(dstr));

            // hash filename
            _hash.Set(Helper.GetBytes(doc.FileName), doc.DocNumber);
            _log.Debug("writing doc to disk (ms) = " + FastDateTime.Now.Subtract(dt).TotalMilliseconds);

            dt = FastDateTime.Now;
            // index doc
            AddtoIndex(doc.DocNumber, doc.Text);
            _log.Debug("indexing time (ms) = " + FastDateTime.Now.Subtract(dt).TotalMilliseconds);

            return _lastDocNum;
        }

        public IEnumerable<int> FindRows(string filter)
        {
            while (_internalOP) Thread.Sleep(50);

            WAHBitArray bits = ExecutionPlan(filter);
            // enumerate records
            return bits.GetBitIndexes(true);
        }

        public IEnumerable<Document> FindDocuments(string filter)
        {
            while (_internalOP) Thread.Sleep(50);

            WAHBitArray bits = ExecutionPlan(filter);
            // enumerate documents
            foreach (int i in bits.GetBitIndexes(true))
            {
                if (i > _lastDocNum - 1)
                    break;
                byte[] b = _docs.ReadData(i);
                Document d = (Document)fastJSON.JSON.Instance.ToObject(Helper.GetString(b));

                yield return d;
            }
        }

        public IEnumerable<string> FindDocumentFileNames(string filter)
        {
            while (_internalOP) Thread.Sleep(50);

            WAHBitArray bits = ExecutionPlan(filter);
            // enumerate documents
            foreach (int i in bits.GetBitIndexes(true))
            {
                if (i > _lastDocNum - 1)
                    break;
                byte[] b = _docs.ReadData(i);
                Dictionary<string, object> d = (Dictionary<string, object>)fastJSON.JSON.Instance.Parse(Helper.GetString(b));

                yield return d["FileName"].ToString();
            }
        }

        public void RemoveDocument(int number)
        {
            while (_internalOP) Thread.Sleep(50);
            // add number to deleted bitmap
            _deleted.Set(number, true);
        }

        public void OptimizeIndex()
        {
            lock (_lock)
            {
                _internalOP = true;
                InternalSave();
                _log.Debug("optimizing index..");
                DateTime dt = FastDateTime.Now;
                _lastBitmapOffset = 0;
                _bitmapFile.Flush();
                _bitmapFile.Close();
                // compact bitmap index file to new file
                _bitmapFile = new FileStream(_Path + _FileName + ".bitmap$", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                MemoryStream ms = new MemoryStream();
                BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8);
                // save words and bitmaps
                using (FileStream words = new FileStream(_Path + _FileName + ".words", FileMode.Create))
                {
                    foreach (KeyValuePair<string, Cache> kv in _index)
                    {
                        bw.Write(kv.Key);
                        uint[] ar = LoadBitmap(kv.Value.FileOffset);
                        long offset = SaveBitmap(ar, ar.Length, 0);
                        kv.Value.FileOffset = offset;
                        bw.Write(kv.Value.FileOffset);
                    }
                    // save words
                    byte[] b = ms.ToArray();
                    words.Write(b, 0, b.Length);
                    words.Flush();
                    words.Close();
                }
                // rename files
                _bitmapFile.Flush();
                _bitmapFile.Close();
                File.Delete(_Path + _FileName + ".bitmap");
                File.Move(_Path + _FileName + ".bitmap$", _Path + _FileName + ".bitmap");
                // reload everything
                _bitmapFile = new FileStream(_Path + _FileName + ".bitmap", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                _lastBitmapOffset = _bitmapFile.Seek(0L, SeekOrigin.End);
                _log.Debug("optimizing index done = " + DateTime.Now.Subtract(dt).TotalSeconds + " sec");
                _internalOP = false;
            }
        }

        #region [  P R I V A T E   M E T H O D S  ]

        private WAHBitArray ExecutionPlan(string filter)
        {
            _log.Debug("query : " + filter);
            DateTime dt = FastDateTime.Now;
            // query indexes
            string[] words = filter.Split(' ');

            WAHBitArray bits = null;

            foreach (string s in words)
            {
                Cache c;
                if (s == "") continue;
                if (_index.TryGetValue(s.ToLowerInvariant(), out c))
                {
                    // bits logic
                    if (c.isLoaded == false)
                        LoadCache(c);
                    if (bits != null)
                        bits = c.Op(bits, Cache.OPERATION.AND);
                    else
                        bits = c.GetBitmap();
                }
            }
            if (bits == null)
                return new WAHBitArray();

            // remove deleted docs
            if (bits.Length > _deleted.Length)
                _deleted.Length = bits.Length;
            else if (bits.Length < _deleted.Length)
                bits.Length = _deleted.Length;

            WAHBitArray ret = bits.And(_deleted.Not());
            _log.Debug("query time (ms) = " + FastDateTime.Now.Subtract(dt).TotalMilliseconds);
            return ret;
        }

        private void LoadCache(Cache c)
        {
            if (c.FileOffset != -1)
            {
                uint[] bits = LoadBitmap(c.FileOffset);
                c.SetCompressedBits(bits);
            }
            else
            {
                c.SetCompressedBits(new uint[] { 0 });
            }
        }

        private void InternalSave()
        {
            _log.Debug("saving index...");
            DateTime dt = FastDateTime.Now;
            // save deleted
            WriteDeleted();
            // save hash index
            _hash.SaveIndex();

            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8);

            // save words and bitmaps
            using (FileStream words = new FileStream(_Path + _FileName + ".words", FileMode.Create))
            {
                foreach (KeyValuePair<string, Cache> kv in _index)
                {
                    bw.Write(kv.Key);
                    if (kv.Value.isDirty)
                    {
                        // write bit index
                        uint[] ar = kv.Value.GetCompressedBits();
                        if (ar != null)
                        {
                            // save bitmap data to disk
                            long off = SaveBitmap(ar, kv.Value.LastBitSaveLength, kv.Value.FileOffset);
                            // set the saved info in cache
                            kv.Value.FileOffset = off;
                            kv.Value.LastBitSaveLength = ar.Length;
                            // set the word bitmap offset
                            bw.Write(kv.Value.FileOffset);
                        }
                        else
                            bw.Write(kv.Value.FileOffset);
                    }
                    else
                        bw.Write(kv.Value.FileOffset);

                    kv.Value.isDirty = false;
                }
                byte[] b = ms.ToArray();
                words.Write(b, 0, b.Length);
                words.Flush();
                words.Close();
            }
            _log.Debug("save time (ms) = " + FastDateTime.Now.Subtract(dt).TotalMilliseconds);
        }

        private void ReadDeleted()
        {
            if (File.Exists(_Path + _FileName + ".deleted") == false)
            {
                _deleted = new WAHBitArray();
                return;
            }
            using (FileStream del = new FileStream(_Path + _FileName + ".deleted",
                                                   FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                List<uint> ar = new List<uint>();
                byte[] b = new byte[4];
                while (del.Read(b, 0, 4) > 0)
                {
                    ar.Add((uint)Helper.ToInt32(b, 0));
                }
                _deleted = new WAHBitArray(ar.ToArray());

                del.Close();
            }
        }

        private void WriteDeleted()
        {
            using (FileStream del = new FileStream(_Path + _FileName + ".deleted", FileMode.Create,
                                                   FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                uint[] b = _deleted.GetCompressed();

                foreach (uint i in b)
                {
                    del.Write(Helper.GetBytes((int)i, false), 0, 4);
                }
                del.Flush();
                del.Close();
            }
        }

        private void LoadWords()
        {
            if (File.Exists(_Path + _FileName + ".words") == false)
                return;
            // load words
            byte[] b = File.ReadAllBytes(_Path + _FileName + ".words");
            MemoryStream ms = new MemoryStream(b);
            BinaryReader br = new BinaryReader(ms, Encoding.UTF8);
            string s = br.ReadString();
            while (s != "")
            {
                long off = br.ReadInt64();
                Cache c = new Cache();
                c.isLoaded = false;
                c.isDirty = false;
                c.FileOffset = off;
                _index.Add(s, c);
                try
                {
                    s = br.ReadString();
                }
                catch { s = ""; }
            }
            _log.Debug("Word Count = " + _index.Count);
        }

        //-----------------------------------------------------------------
        // BITMAP FILE FORMAT
        //    0  'B','M'
        //    2  uint count = 4 bytes
        //    6  '0'
        //    7  uint data
        //-----------------------------------------------------------------
        private long SaveBitmap(uint[] bits, int lassize, long offset)
        {
            long off = _lastBitmapOffset;

            byte[] b = new byte[bits.Length * 4 + 7];
            // write header data
            b[0] = ((byte)'B');
            b[1] = ((byte)'M');
            Buffer.BlockCopy(Helper.GetBytes(bits.Length, false), 0, b, 2, 4);
            b[6] = (0);

            for (int i = 0; i < bits.Length; i++)
            {
                byte[] u = Helper.GetBytes((int)bits[i], false);
                Buffer.BlockCopy(u, 0, b, i * 4 + 7, 4);
            }
            _bitmapFile.Write(b, 0, b.Length);
            _lastBitmapOffset += b.Length;
            _bitmapFile.Flush();
            return off;
        }

        private uint[] LoadBitmap(long offset)
        {
            if (offset == -1)
                return null;

            List<uint> ar = new List<uint>();

            using (FileStream bmp = new FileStream(_Path + _FileName + ".bitmap", FileMode.Open,
                                                   FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                bmp.Seek(offset, SeekOrigin.Begin);

                byte[] b = new byte[7];
                bmp.Read(b, 0, 7);
                if (b[0] == (byte)'B' && b[1] == (byte)'M' && b[6] == 0)
                {
                    int c = Helper.ToInt32(b, 2);
                    for (int i = 0; i < c; i++)
                    {
                        bmp.Read(b, 0, 4);
                        ar.Add((uint)Helper.ToInt32(b, 0));
                    }
                }

                bmp.Flush();
                bmp.Close();
            }
            return ar.ToArray();
        }

        private void AddtoIndex(int recnum, string text)
        {
            _log.Debug("text size = " + text.Length);
            Dictionary<string, int> wordfreq = GenerateWordFreq(text);
            _log.Debug("word count = " + wordfreq.Count);

            foreach (string key in wordfreq.Keys)
            {
                Cache cache;
                if (_index.TryGetValue(key, out cache))
                {
                    cache.SetBit(recnum, true);
                }
                else
                {
                    cache = new Cache();
                    cache.isLoaded = true;
                    cache.SetBit(recnum, true);
                    _index.Add(key, cache);
                }
            }
        }

        private Dictionary<string, int> GenerateWordFreq(string text)
        {
            Dictionary<string, int> dic = new Dictionary<string, int>(50000);

            char[] chars = text.ToCharArray();
            int index = 0;
            int run = -1;
            int count = chars.Length;
            while (index < count)
            {
                char c = chars[index++];
                if (!char.IsLetter(c))
                {
                    if (run != -1)
                    {
                        ParseString(dic, chars, index, run);
                        run = -1;
                    }
                }
                else
                    if (run == -1)
                        run = index - 1;
            }

            if (run != -1)
            {
                ParseString(dic, chars, index, run);
                run = -1;
            }

            return dic;
        }

        private void ParseString(Dictionary<string, int> dic, char[] chars, int end, int start)
        {
            // check if upper lower case mix -> extract words
            int uppers = 0;
            bool found = false;
            for (int i = start; i < end; i++)
            {
                if (char.IsUpper(chars[i]))
                    uppers++;
            }
            // not all uppercase
            if (uppers != end - start - 1)
            {
                int lastUpper = start;

                string word = "";
                for (int i = start + 1; i < end; i++)
                {
                    char c = chars[i];
                    if (char.IsUpper(c))
                    {
                        found = true;
                        word = new string(chars, lastUpper, i - lastUpper).ToLowerInvariant().Trim();
                        AddDictionary(dic, word);
                        lastUpper = i;
                    }
                }
                if (lastUpper > start)
                {
                    string last = new string(chars, lastUpper, end - lastUpper).ToLowerInvariant().Trim();
                    if (word != last)
                        AddDictionary(dic, last);
                }
            }
            if (found == false)
            {
                string s = new string(chars, start, end - start - 1).ToLowerInvariant().Trim();
                AddDictionary(dic, s);
            }
        }

        private void AddDictionary(Dictionary<string, int> dic, string word)
        {
            int l = word.Length;
            if (l > MAX_STRING_LENGTH_IGNORE)
                return;
            if (l < 2)
                return;
            if (char.IsLetter(word[l - 1]) == false)
                word = new string(word.ToCharArray(), 0, l - 2);
            if (word.Length < 2)
                return;
            int cc = 0;
            if (dic.TryGetValue(word, out cc))
                dic[word] = ++cc;
            else
                dic.Add(word, 1);
        }
        #endregion
    }
}
