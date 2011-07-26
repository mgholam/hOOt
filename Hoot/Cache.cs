using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace hOOt
{
    public class Document
    {
        public Document()
        {
            DocNumber = -1;
        }
        public Document(string filename, string text)
        {
            FileName = filename;
            Text = text;
            DocNumber = -1;
        }
        public int DocNumber { get; set; }
        [XmlIgnore]
        public string Text { get; set; }
        public string FileName { get; set; }

        public override string ToString()
        {
            return FileName;
        }
    }

    internal class Cache
    {
        public enum OPERATION
        {
            AND,
            OR,
            NOT
        }

        public Cache()
        {
        }

        public bool isLoaded = false;
        public bool isDirty = true;
        public long FileOffset = -1;
        public int LastBitSaveLength = 0;
        private WAHBitArray _bits;

        public void SetBit(int index, bool val)
        {
            if (_bits != null)
                _bits.Set(index, val);
            else
            {
                _bits = new WAHBitArray();
                _bits.Set(index, val);
            }
            isDirty = true;
        }

        public uint[] GetCompressedBits()
        {
            if (_bits != null)
                return _bits.GetCompressed();
            else
                return null;
        }

        public void FreeMemory(bool unload)
        {
            if (_bits != null)
                _bits.FreeMemory();

            if (unload)
            {
                _bits = null;
                isLoaded = false;
            }
        }

        public void SetCompressedBits(uint[] bits)
        {
            _bits = new WAHBitArray(true, bits);
            LastBitSaveLength = bits.Length;
            isLoaded = true;
            isDirty = false;
        }

        public WAHBitArray Op(WAHBitArray bits, OPERATION op)
        {
            if (_bits == null)
            {
                // should not be here
            }

            if (op == OPERATION.AND)
                return _bits.And(bits);
            else if (op == OPERATION.OR)
                return _bits.Or(bits);
            else
                return _bits.Not();
        }

        public WAHBitArray GetBitmap()
        {
            return _bits;
        }
    }
}
