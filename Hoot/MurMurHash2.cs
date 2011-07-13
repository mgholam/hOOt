using System;
using System.Collections.Generic;
using System.Text;

namespace hOOt
{
    internal class Hash 
    {
        private int _BucketCount = 10007;
        private int _BucketItems = 200;
        private int _MaxDuplicates = 10;
        private List<int> _bucketPointers = new List<int>();
        MurmurHash2Unsafe _hash = new MurmurHash2Unsafe();
        private SortedList<int, Bucket> _CachedBuckets = new SortedList<int, Bucket>(10007);
        private IndexFile _indexfile;
        private bool _InMemory = false;

        public Hash(string indexfilename, byte maxkeysize, short nodeSize, bool allowDuplicates, int bucketcount)
        {
            _BucketCount = bucketcount;
            _BucketItems = nodeSize;
            // fill buckets with blanks
            for (int i = 0; i < _BucketCount; i++)
                _bucketPointers.Add(-1);

            _indexfile = new IndexFile(indexfilename, maxkeysize, nodeSize, bucketcount);

            _BucketItems = _indexfile._PageNodeCount;

            // load data from index file
            _bucketPointers = _indexfile.GetBucketList();

            _MaxDuplicates = _indexfile._DuplicatesPerPage;
        }

        #region [   I I N D E X   ]

        public bool InMemory
        {
            get
            {
                return _InMemory;
            }
            set
            {
                _InMemory = value;
            }
        }

        public bool Get(byte[] key, out int offset)
        {
            offset = -1;
            Bucket b = FindBucket(key);
            return SearchBucket(b, key, ref offset);
        }

        public void Set(byte[] key, int offset)
        {
            Bucket b = FindBucket(key);
            b = SetBucket(key, offset, b);
        }

        public void Commit()
        {
            if (_InMemory == false)
                SaveIndex();
        }

        public void Shutdown()
        {
            Commit();
        }

        public long Count()
        {
            return _indexfile.CountBuckets();
        }

        public void SaveIndex()
        {
            if (_indexfile.Commit(_CachedBuckets))
            {
                _CachedBuckets = new SortedList<int, Bucket>(_BucketCount);
            }
            _indexfile.SaveBucketList(_bucketPointers);
        }
        #endregion

        #region [   P R I V A T E   M E T H O D S   ]

        private Bucket SetBucket(byte[] key, int offset, Bucket b)
        {
            bool found = false;
            int pos = FindPointerOrLower(b, new bytearr(key), out found);

            if (found)
            {
                KeyPointer p = b.Pointers[pos];
                int v = p.RecordNum;

                // duplicate found     
                if (v != offset)
                {
                    p.RecordNum = offset;
                    DirtyBucket(b);
                }
            }
            else
            {
                if (b.Pointers.Count < _BucketItems)
                {
                    KeyPointer k = new KeyPointer(new bytearr(key), offset);
                    pos++;
                    if (pos < b.Pointers.Count)
                        b.Pointers.Insert(pos, k);
                    else
                        b.Pointers.Add(k);
                    DirtyBucket(b);
                }
                else
                {
                    int p = b.NextPageNumber;
                    if (p != -1)
                    {
                        b = LoadBucket(p);
                        SetBucket(key, offset, b);
                    }
                    else
                    {
                        Bucket newb = new Bucket(_indexfile.GetNewPageNumber());
                        b.NextPageNumber = newb.DiskPageNumber;
                        DirtyBucket(b);
                        SetBucket(key, offset, newb);
                    }
                }
            }
            return b;
        }

        private bool SearchBucket(Bucket b, byte[] key, ref int offset)
        {
            bool found = false;
            int pos = FindPointerOrLower(b, new bytearr(key), out found);
            if (found)
            {
                KeyPointer k = b.Pointers[pos];
                offset = k.RecordNum;
                return true;
            }
            else
            {
                if (b.NextPageNumber != -1)
                {
                    b = LoadBucket(b.NextPageNumber);
                    return SearchBucket(b, key, ref offset);
                }
            }
            return false;
        }

        private void DirtyBucket(Bucket b)
        {
            if (b.isDirty)
                return;

            b.isDirty = true;
            if (_CachedBuckets.ContainsKey(b.DiskPageNumber) == false)
                _CachedBuckets.Add(b.DiskPageNumber, b);
        }

        private int FindPointerOrLower(Bucket b, bytearr key, out bool found)
        {
            found = false;
            if (b.Pointers.Count == 0)
                return 0;
            // binary search
            int lastlower = -1;
            int first = 0;
            int last = b.Pointers.Count - 1;
            int mid = 0;
            while (first <= last)
            {
                mid = (first + last) >> 1;
                KeyPointer k = b.Pointers[mid];
                int compare = Helper.Compare(k.Key, key);
                if (compare < 0)
                {
                    lastlower = mid;
                    first = mid + 1;
                }
                if (compare == 0)
                {
                    found = true;
                    return mid;
                }
                if (compare > 0)
                {
                    last = mid - 1;
                }
            }

            return lastlower;
        }

        private Bucket FindBucket(byte[] key)
        {
            Bucket b;
            uint h = _hash.Hash(key);

            int bucketNumber = (int)(h % _BucketCount);

            int pointer = _bucketPointers[bucketNumber];
            if (pointer == -1)
            {
                // new bucket
                b = CreateBucket(bucketNumber);
                _bucketPointers[bucketNumber] = b.DiskPageNumber;
                _CachedBuckets.Add(b.DiskPageNumber, b);
            }
            else
                b = LoadBucket(pointer);

            return b;
        }

        private Bucket LoadBucket(int pagenumber)
        {
            Bucket b;
            // try cache first
            if (_CachedBuckets.TryGetValue(pagenumber, out b))
                return b;
            // else load from disk //and put in cache
            b = _indexfile.LoadBucketFromPage(pagenumber);

            return b;
        }

        private Bucket CreateBucket(int bucketNumber)
        {
            Bucket b = new Bucket(_indexfile.GetNewPageNumber());
            b.BucketNumber = bucketNumber;
            // get next free indexfile pointer offset

            return b;
        }
        #endregion
    }

    public class MurmurHash2Unsafe
    {
        public UInt32 Hash(Byte[] data)
        {
            return Hash(data, 0xc58f1a7b);
        }
        const UInt32 m = 0x5bd1e995;
        const Int32 r = 24;

        public unsafe UInt32 Hash(Byte[] data, UInt32 seed)
        {
            Int32 length = data.Length;
            if (length == 0)
                return 0;
            UInt32 h = seed ^ (UInt32)length;
            Int32 remainingBytes = length & 3; // mod 4
            Int32 numberOfLoops = length >> 2; // div 4
            fixed (byte* firstByte = &(data[0]))
            {
                UInt32* realData = (UInt32*)firstByte;
                while (numberOfLoops != 0)
                {
                    UInt32 k = *realData;
                    k *= m;
                    k ^= k >> r;
                    k *= m;

                    h *= m;
                    h ^= k;
                    numberOfLoops--;
                    realData++;
                }
                switch (remainingBytes)
                {
                    case 3:
                        h ^= (UInt16)(*realData);
                        h ^= ((UInt32)(*(((Byte*)(realData)) + 2))) << 16;
                        h *= m;
                        break;
                    case 2:
                        h ^= (UInt16)(*realData);
                        h *= m;
                        break;
                    case 1:
                        h ^= *((Byte*)realData);
                        h *= m;
                        break;
                    default:
                        break;
                }
            }

            // Do a few final mixes of the hash to ensure the last few
            // bytes are well-incorporated.

            h ^= h >> 13;
            h *= m;
            h ^= h >> 15;

            return h;
        }
    }
}
