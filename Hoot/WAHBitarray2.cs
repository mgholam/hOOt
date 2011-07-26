using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace hOOt
{
    internal class WAHBitArray
    {
        public WAHBitArray()
        {
        }

        public WAHBitArray(bool compressed, uint[] ints)
        {
            if (compressed)
                _compressed = new List<uint>(ints);
            else
                _uncompressed = new List<uint>(ints);
        }

        private Guid g = Guid.NewGuid();
        private List<uint> _compressed;
        private List<uint> _uncompressed;

        public bool Get(int index)
        {
            CheckBitArray();

            ResizeAsNeeded(_uncompressed, index);

            return internalGet(index);
        }

        public void Set(int index, bool val)
        {
            CheckBitArray();

            ResizeAsNeeded(_uncompressed, index);

            internalSet(index, val);
        }

        public int Length
        {
            set { CheckBitArray(); ResizeAsNeeded(_uncompressed, value); }
            get { CheckBitArray(); return _uncompressed.Count << 5; }
        }

        public WAHBitArray And(WAHBitArray op)
        {
            this.CheckBitArray();

            uint[] ints = op.GetUncompressed();

            FixSizes(ints, _uncompressed);

            for (int i = 0; i < ints.Length; i++)
                ints[i] &= _uncompressed[i];

            return new WAHBitArray(false, ints);
        }

        public WAHBitArray Or(WAHBitArray op)
        {
            this.CheckBitArray();

            uint[] ints = op.GetUncompressed();
            
            FixSizes(ints, _uncompressed);

            for (int i = 0; i < ints.Length; i++)
                ints[i] |= _uncompressed[i];

            return new WAHBitArray(false, ints);
        }

        public WAHBitArray Not()
        {
            this.CheckBitArray();

            uint[] ints = _uncompressed.ToArray();

            for (int i = 0; i < ints.Length; i++)
                ints[i] = ~ints[i];

            return new WAHBitArray(false, ints);
        }

        public WAHBitArray Xor(WAHBitArray op)
        {
            this.CheckBitArray();

            uint[] ints = op.GetUncompressed();

            FixSizes(ints, _uncompressed);

            for (int i = 0; i < ints.Length; i++)
                ints[i] ^= _uncompressed[i];

            return new WAHBitArray(false, ints);
        }

        public long CountOnes()
        {
            long c = 0;
            CheckBitArray();
            int count = _uncompressed.Count << 5;

            for (int i = 0; i < count; i++)
            {
                if (internalGet(i))
                    c++;
            }
            return c;
        }

        public long CountZeros()
        {
            long c = 0;
            CheckBitArray();
            int count = _uncompressed.Count << 5;

            for (int i = 0; i < count; i++)
            {
                if (internalGet(i) == false)
                    c++;
            }
            return c;
        }

        public void FreeMemory()
        {
            Compress();
            _uncompressed = null;
        }

        public uint[] GetCompressed()
        {
            if (_uncompressed == null)
                return new uint[] { 0 };

            Compress();
            return _compressed.ToArray();
        }

        public IEnumerable<int> GetBitIndexes(bool ones)
        {
            CheckBitArray();
            int count = _uncompressed.Count << 5;

            for (int i = 0; i < count; i++)
            {
                bool b = internalGet(i);
                if (b == ones)
                    yield return i;
            }
        }

        public string DebugPrint()
        {
            CheckBitArray();
            StringBuilder sb = new StringBuilder();
            int count = _uncompressed.Count << 5;

            for (int i = 0; i < count; i++)
            {
                bool b = internalGet(i);
                sb.Append(b ? "1" : "0");
            }

            return sb.ToString();
        }

        protected uint[] GetUncompressed()
        {
            this.CheckBitArray();

            return _uncompressed.ToArray();
        }

        #region [  P R I V A T E  ]
        private void FixSizes(uint[] ints, List<uint> _uncompressed)
        {
            int il = ints.Length;
            int ul = _uncompressed.Count;

            if (il < ul)
            {
                // TODO : if needed
            }
            if (il > ul)
            {
                while (_uncompressed.Count < il)
                    _uncompressed.Add(0);
            }
        }

        private void ResizeAsNeeded(List<uint> list, int index)
        {
            int count = index >> 5;
            count++;

            while (list.Count < count)
                list.Add(0);
        }

        private void internalSet(int index, bool val)
        {
            int pointer = index >> 5;
            uint mask = (uint)1 << (index % 32);

            if (val)
                _uncompressed[pointer] |= mask;
            else
                _uncompressed[pointer] &= ~mask;
        }

        private bool internalGet(int index)
        {
            int pointer = index >> 5;
            uint mask = (uint)1 << (index % 32);

            return (_uncompressed[pointer] & mask) != 0;
        }

        private void CheckBitArray()
        {
            if (_compressed == null && _uncompressed == null)
            {
                _uncompressed = new List<uint>();
                return;
            }
            if (_compressed == null)
                return;
            if (_uncompressed == null)
                Uncompress();
        }

        private uint Take31Bits(int index)
        {
            long l1 = 0;
            long l2 = 0;
            long l = 0;
            long ret = 0;
            int off = (index % 32);
            int pointer = index >> 5;

            l1 = _uncompressed[pointer];
            pointer++;
            if (pointer < _uncompressed.Count)
                l2 = _uncompressed[pointer];

            l = (l1 << 32) + l2;
            ret = (l >> (32 - off)) & 0x07fffffff;

            return (uint)ret;
        }

        private void Compress()
        {
            _compressed = new List<uint>();
            uint zeros = 0;
            uint ones = 0;
            int count = _uncompressed.Count << 5;
            for (int i = 0; i < count; )
            {
                uint num = Take31Bits(i);
                i += 31;
                if (num == 0)
                {
                    zeros += 31;
                    FlushOnes(ref ones);
                }
                else if (num == 0x7fffffff)
                {
                    ones += 31;
                    FlushZeros(ref zeros);
                }
                else
                {
                    FlushOnes(ref ones);
                    FlushZeros(ref zeros);
                    _compressed.Add(num);
                }
            }
            FlushOnes(ref ones);
            FlushZeros(ref zeros);
        }

        private void FlushOnes(ref uint ones)
        {
            if (ones > 0)
            {
                uint n = 0xc0000000 + ones;
                ones = 0;
                _compressed.Add(n);
            }
        }

        private void FlushZeros(ref uint zeros)
        {
            if (zeros > 0)
            {
                uint n = 0x80000000 + zeros;
                zeros = 0;
                _compressed.Add(n);
            }
        }

        private void Write31Bits(List<uint> list, int index, uint val)
        {
            this.ResizeAsNeeded(list, index + 32);

            long l = 0;
            int off = (index % 32);
            int pointer = index >> 5;

            l = ((long)val << (32 - off));
            list[pointer] |= (uint)(l >> 32);
            if (pointer < list.Count-1)
                list[pointer + 1] |= (uint)(l & 0xffffffff);
        }

        private void WriteBits(List<uint> list, int index, uint count)
        {
            this.ResizeAsNeeded(list, index);

            int bit = index % 32;
            int pointer = index >> 5;
            int cc = (int)count;

            list[pointer] |= (uint)(~(0x0ffffffff >> bit));
            cc -= (bit);
            while (cc > 32)//full ints
            {
                list.Add(0xffffffff);
                cc -= 32;
            }
            if (cc > 0) //remaining
                list.Add(~(0xffffffff >> (32 - cc)));
        }

        private void Uncompress()
        {
            int index = 0;
            List<uint> list = new List<uint>();
            if (_compressed == null)
                return;

            foreach (uint ci in _compressed)
            {
                if ((ci & 0x80000000) == 0) // literal
                {
                    this.Write31Bits(list, index, ci & 0x7fffffff);
                    index += 31;
                }
                else
                {
                    uint c = ci & 0x3ffffff;
                    if ((ci & 0x40000000) > 0) // ones count
                        this.WriteBits(list, index, c);

                    index += (int)c;
                }
            }
            this.ResizeAsNeeded(list, index);
            _uncompressed = list;
        }
        #endregion
    }
}
