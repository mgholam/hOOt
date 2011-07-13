using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace hOOt
{
    internal class WAHBitArray 
    {
        public WAHBitArray(int size)
        {
            _ba = new BitArray(size);
            _size = size;
        }

        public WAHBitArray(BitArray bitarray)
        {
            _ba = bitarray;
            _size = bitarray.Length;
        }

        public WAHBitArray(uint[] CompressedInts)
        {
            _compressed.AddRange(CompressedInts);
            //_ba = new BitArray(size);
            _size = 32;// size;
        }

        private List<uint> _compressed = new List<uint>();
        private BitArray _ba;
        private int _size;

        public bool Get(int index)
        {
            CheckBitArray();
            if (index > _size)
            {
                _ba.Length = index+1;
                _size = index+1;
            }
            return _ba.Get(index);
        }

        public void Set(int index, bool val)
        {
            CheckBitArray();
            if (index >= _size)
            {
                _ba.Length = index+1;
                _size = index+1;
            }
            _ba.Set(index, val);
            //FreeMemory();
        }

        public int Length
        {
            set { _size = value; if (_ba != null) _ba.Length = value; }
            get { return _size; }
        }

        public WAHBitArray And(BitArray op)
        {
            CheckBitArray(op);

            return new WAHBitArray(_ba.And(op));
        }

        public WAHBitArray And(WAHBitArray op)
        {
            CheckBitArray(op);

            return op.And(_ba);
        }

        public WAHBitArray Or(BitArray op)
        {
            CheckBitArray(op);
           
            return new WAHBitArray(_ba.Or(op));
        }

        public WAHBitArray Or(WAHBitArray op)
        {
            CheckBitArray(op);

            return op.Or(_ba);
        }

        public WAHBitArray Not()
        {
            CheckBitArray();

            return new WAHBitArray(_ba.Not());
        }

        public WAHBitArray Xor(BitArray op)
        {
            CheckBitArray(op);

            return new WAHBitArray(_ba.Xor(op));
        }

        public WAHBitArray Xor(WAHBitArray op)
        {
            CheckBitArray(op);

            return op.Xor(_ba);
        }

        public long CountOnes()
        {
            long c = 0;
            CheckBitArray();

            for (int i = 0; i < _ba.Count; i++)
            {
                if (_ba[i])
                    c++;
            }
            return c;
        }

        public long CountZeros()
        {
            long c = 0;
            CheckBitArray();

            for (int i = 0; i < _ba.Count; i++)
            {
                if (_ba[i]==false)
                    c++;
            }
            return c;
        }

        public void FreeMemory()
        {
            Compress();
            _ba = null;
        }

        public uint[] GetCompressed()
        {
            Compress();
            return _compressed.ToArray();
        }

        public IEnumerable<int> GetBitIndexes(bool ones)
        {
            CheckBitArray();

            for (int i = 0; i < _ba.Length; i++)
            {
                bool b = _ba[i];
                if(b==ones)
                    yield return i;
            }
        }

        #region [  P R I V A T E  ]
        private void CheckBitArray()
        {
            if (_ba == null)
                Uncompress();
        }

        private void CheckBitArray(BitArray op)
        {
            CheckBitArray();

            if (op != null)
            {
                int L1 = _ba.Length;
                int L2 = op.Length;
                if (L1 != L2)
                {
                    if (L1 > L2)
                        op.Length = L1;
                    else
                        _ba.Length = L2;
                }
                _size = _ba.Length;
            }
        }

        private void CheckBitArray(WAHBitArray op)
        {
            CheckBitArray();

            if (op != null)
            {
                int L1 = _ba.Length;
                int L2 = op.Length;
                if (L1 != L2)
                {
                    if (L1 > L2)
                        op.Length = L1;
                    else
                        _ba.Length = L2;
                }
                _size = _ba.Length;
            }
        }

        private void Compress()
        {
            if (_ba == null)
                return;
            _compressed = new List<uint>();
            uint zeros = 0;
            uint ones = 0;
            int mc = _ba.Count;
            for (int i = 0; i < _ba.Count; )
            {
                uint num = 0;
                for (int k = 0; k < 31; k++)
                {
                    num <<= 1;
                    if (i + k >= mc)
                        break;
                    if (_ba.Get(i + k))
                        num++;
                }
                i += 31;
                if (num == 0)
                {
                    zeros += 31;
                    if (ones > 0)
                    {
                        uint n = 0xc0000000 + ones;
                        ones = 0;
                        _compressed.Add(n);
                    }
                }
                else if (num == 0x7fffffff)
                {
                    ones += 31;
                    if (zeros > 0)
                    {
                        uint n = 0x80000000 + zeros;
                        zeros = 0;
                        _compressed.Add(n);
                    }
                }
                else
                {
                    if (ones > 0)
                    {
                        uint n = 0xc0000000 + ones;
                        ones = 0;
                        _compressed.Add(n);
                    }
                    if (zeros > 0)
                    {
                        uint n = 0x80000000 + zeros;
                        zeros = 0;
                        _compressed.Add(n);
                    }
                    _compressed.Add(num);
                }
            }
            if (ones > 0)
            {
                uint n = 0xc0000000 + ones;
                ones = 0;
                _compressed.Add(n);
            }
            if (zeros > 0)
            {
                uint n = 0x80000000 + zeros;
                zeros = 0;
                _compressed.Add(n);
            }
        }

        private void Uncompress()
        {
            int bit = 0;
            _ba = new BitArray(_size);
            int mc = _size;
            foreach (uint ci in _compressed)
            {
                if ((ci & 0x80000000) == 0)
                {
                    for (int j = 30; j >= 0; j--)
                    {
                        uint mask = (uint)1 << j;

                        if ((ci & mask) > 0)
                            Set(bit, true);// _ba[bit] = true;
                        bit++;
                        if (bit >= mc)
                            break;

                    }
                }
                else
                {
                    uint c = ci & 0x3ffffff;
                    if ((ci & 0x40000000) > 0)
                    {
                        for (int j = (int)c; j >= 0; j--)
                        {
                            Set(bit, true);// _ba.Set(bit, true);
                            bit++;
                            if (bit >= mc)
                                break;
                        }
                    }
                    else
                        bit += (int)c;
                }
            }
        }
#endregion
    }
}
