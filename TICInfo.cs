﻿
namespace IMSDriftTimeAligner
{
    internal class ScanStats
    {
        public double BPI { get; set; }

        public double BPI_MZ { get; set; }

        public int NonZeroCount { get; set; }

        public double TIC { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ScanStats()
        {
            BPI = 0;
            BPI_MZ = 0;

            NonZeroCount = 0;
            TIC = 0;
        }
    }
}
