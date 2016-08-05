namespace RaptorDB
{
    /// <summary>
    /// High frequency mode Key/Value store with recycled storage file.
    /// <para>Use for rapid saves of the same key.</para>
    /// <para>Views are not effected by saves in this storage.</para>
    /// <para>NOTE : You do not have history of changes in this storage.</para>
    /// </summary>
    public interface IKeyStoreHF
    {
        object GetObjectHF(string key);
        bool SetObjectHF(string key, object obj);
        bool DeleteKeyHF(string key);
        int CountHF();
        bool ContainsHF(string key);
        string[] GetKeysHF();
        void CompactStorageHF();
        int Increment(string key, int amount);
        decimal Increment(string key, decimal amount);
        int Decrement(string key, int amount);
        decimal Decrement(string key, decimal amount);
        //T Increment<T>(string key, T amount);
        //T Decrement<T>(string key, T amount);
        //IEnumerable<object> EnumerateObjects();
        //string[] SearchKeys(string contains); // FIX : implement 
    }

    internal enum RDBExpression
    {
        Equal,
        Greater,
        GreaterEqual,
        Less,
        LessEqual,
        NotEqual,
        Between,
        Contains
    }

    internal interface IIndex
    {
        void Set(object key, int recnum);
        //WAHBitArray Query(object fromkey, object tokey, int maxsize);
        WAHBitArray Query(RDBExpression ex, object from, int maxsize);
        void FreeMemory();
        void Shutdown();
        void SaveIndex();
        object[] GetKeys();
    }
}
