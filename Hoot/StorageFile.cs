using System;
using System.Diagnostics;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace hOOt
{
    internal class StorageFile
    {
        System.IO.Stream _writefile;
        System.IO.Stream _recordfile;
        private int _maxKeyLen;
        string _filename = "";
        string _recfilename = "";
        int _lastRecordNum = 0;
        long _lastWriteOffset = 0;

        public static byte[] _fileheader = { (byte)'M', (byte)'G', (byte)'D', (byte)'B',
                                              0, // -- [flags] = [shutdownOK:1],
                                              0  // -- [maxkeylen] 
                                           };

        public static byte[] _rowheader = { (byte)'M', (byte)'G', (byte)'R' ,
                                           0,               // 4     [keylen]
                                           0,0,0,0,0,0,0,0, // 5-12  [datetime] 8 bytes = insert time
                                           0,0,0,0,         // 13-16 [data length] 4 bytes
                                           0,               // 17 -- [flags] = isCommited:1 
                                                            //                 isRollback:1
                                                            //                 isCompressed:1
                                                            //                 isDeleted:1
                                                            //                 isVersioned:1 
                                           0                // 18 -- [crc] = header crc check
                                       };
        private enum HDR_POS
        {
            KeyLen = 3,
            DateTime = 4,
            DataLength = 12,
            Flags = 16,
            CRC = 17
        }

        public bool SkipDateTime = false;

        public StorageFile(string filename, int maxkeylen)
        {
            _filename = filename;
            if (File.Exists(filename) == false)
                _writefile = new FileStream(filename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
            else
                _writefile = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            // load rec pointers
            _recfilename = filename.Substring(0, filename.LastIndexOf('.')) + ".rec";
            if (File.Exists(_recfilename) == false)
                _recordfile = new FileStream(_recfilename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
            else
                _recordfile = new FileStream(_recfilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            _maxKeyLen = maxkeylen;
            if (_writefile.Length == 0)
            {
                // new file
                byte b = (byte)maxkeylen;
                _fileheader[5] = b;
                _writefile.Write(_fileheader, 0, _fileheader.Length);
                _writefile.Flush();
            }
            else
            {
                // TODO : check file header exists
                // TODO : check file flags ok
            }
            bw = new BinaryWriter(ms, Encoding.UTF8);

            _lastRecordNum = (int)(_recordfile.Length / 8);
            _recordfile.Seek(0L, SeekOrigin.End);
            _lastWriteOffset = _writefile.Seek(0L, SeekOrigin.End);
        }

        public IEnumerable<KeyValuePair<byte[], byte[]>> Traverse()
        {
            long offset = 0;
            offset = _fileheader.Length;

            while (offset < _writefile.Length)
            {
                long pointer = offset;
                byte[] key;
                offset = NextOffset(offset, out key);
                KeyValuePair<byte[], byte[]> kv = new KeyValuePair<byte[], byte[]>(key, internalReadData(pointer));

                yield return kv;
            }
        }

        private long NextOffset(long curroffset, out byte[] key)
        {
            using (Stream _read = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long next = _read.Length;
                // seek offset in file
                byte[] hdr = new byte[_rowheader.Length];
                _read.Seek(curroffset, System.IO.SeekOrigin.Begin);
                // read header
                _read.Read(hdr, 0, _rowheader.Length);
                key = new byte[hdr[(int)HDR_POS.KeyLen]];
                _read.Read(key, 0, hdr[(int)HDR_POS.KeyLen]);
                // check header
                if (CheckHeader(hdr))
                {
                    next = curroffset + hdr.Length + Helper.ToInt32(hdr, (int)HDR_POS.DataLength) + hdr[(int)HDR_POS.KeyLen];
                }

                return next;
            }
        }

        public int WriteData(byte[] key, byte[] data)
        {
            byte[] k = key;
            int kl = k.Length;

            // seek end of file
            long offset = _lastWriteOffset;
            byte[] hdr = CreateRowHeader(kl, data.Length);
            // write header info
            _writefile.Write(hdr, 0, hdr.Length);
            // write key
            _writefile.Write(k, 0, kl);
            // write data block
            _writefile.Write(data, 0, data.Length);
            _writefile.Flush();
            // update pointer
            _lastWriteOffset += hdr.Length;
            _lastWriteOffset += kl;
            _lastWriteOffset += data.Length;
            // return starting offset -> recno
            int recno = _lastRecordNum++;
            _recordfile.Write(Helper.GetBytes(offset, false), 0, 8);
            _recordfile.Flush();

            return recno;
        }

        MemoryStream ms = new MemoryStream();
        BinaryWriter bw;
        private byte[] CreateRowHeader(int keylen, int datalen)
        {
            ms.Seek(0L, SeekOrigin.Begin);
            bw.Write(_rowheader, 0, 3);
            bw.Write((byte)keylen);
            if (SkipDateTime == false)
                bw.Write(FastDateTime.Now.Ticks);
            else
                bw.Write(0L);
            bw.Write(datalen);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Flush();
            return ms.ToArray();
        }

        public byte[] ReadData(int recnum)
        {
            long off = recnum * 8;
            using (Stream _read = new FileStream(_recfilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] b = new byte[8];

                _read.Seek(off, SeekOrigin.Begin);
                _read.Read(b, 0, 8);
                off = Helper.ToInt64(b, 0);
            }

            return internalReadData(off);
        }

        private byte[] internalReadData(long offset)
        {
            using (Stream _read = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // seek offset in file
                byte[] hdr = new byte[_rowheader.Length];
                _read.Seek(offset, System.IO.SeekOrigin.Begin);
                // read header
                _read.Read(hdr, 0, _rowheader.Length);
                // check header
                if (CheckHeader(hdr))
                {
                    // skip key bytes
                    _read.Seek(hdr[(int)HDR_POS.KeyLen], System.IO.SeekOrigin.Current);
                    int dl = Helper.ToInt32(hdr, (int)HDR_POS.DataLength);
                    byte[] data = new byte[dl];
                    // read data block
                    _read.Read(data, 0, dl);
                    return data;
                }
                else
                    throw new Exception("data header error");
            }
        }

        private bool CheckHeader(byte[] hdr)
        {
            if (hdr[0] == (byte)'M' && hdr[1] == (byte)'G' && hdr[2] == (byte)'R' && hdr[(int)HDR_POS.CRC] == (byte)0)
                return true;
            return false;
        }

        public void Shutdown()
        {
            this._writefile.Flush();
            this._writefile.Close();
        }
    }
}
