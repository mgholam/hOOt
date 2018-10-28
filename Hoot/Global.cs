namespace RaptorDB
{
    public class Global
    {
        /// <summary>
        /// Default maximum string key size for indexes
        /// </summary>
        public static byte DefaultStringKeySize = 60;
        /// <summary>
        /// Free bitmap index memory on save 
        /// </summary>
        public static bool FreeBitmapMemoryOnSave = false;
        /// <summary>
        /// Number of items in each index page (default = 10000) [Expert only, do not change]
        /// </summary>
        public static ushort PageItemCount = 10000;
        /// <summary>
        /// KeyStore save to disk timer
        /// </summary>
        public static int SaveIndexToDiskTimerSeconds = 1800;
        /// <summary>
        /// Flush the StorageFile stream immediately
        /// </summary>
        public static bool FlushStorageFileImmediately = false;
        /// <summary>
        /// Save doc as binary json
        /// </summary>
        public static bool SaveAsBinaryJSON = false;
        /// <summary>
        /// Split the data storage files in MegaBytes (default 0 = off) [500 = 500mb]
        /// <para> - You can set and unset this value anytime and it will operate from that point on.</para>
        /// <para> - If you unset (0) the value previous split files will remain and all the data will go to the last file.</para>
        /// </summary>
        public static ushort SplitStorageFilesMegaBytes = 0;
        /// <summary>
        /// Compress the documents in the storage file if it is over this size (default = 100 Kilobytes) 
        /// <para> - You will be trading CPU for disk IO</para>
        /// </summary>
        public static ushort CompressDocumentOverKiloBytes = 100;
        /// <summary>
        /// Disk block size for high frequency KV storage file (default = 2048)
        /// <para> * Do not use anything under 512 with large string keys</para>
        /// </summary>
        public static ushort HighFrequencyKVDiskBlockSize = 2048;

        public static bool UseLessMemoryStructures = true;

        public static bool CompressBitmapBytes = false;
    }
}
