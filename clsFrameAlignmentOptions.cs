
using System;
using System.Reflection;
using PRISM;

namespace IMSDriftTimeAligner
{
    public class FrameAlignmentOptions
    {
        public const string PROGRAM_DATE = "August 31, 2017";

        public const BaseFrameSelectionModes DEFAULT_FRAME_SELECTION_MODE = BaseFrameSelectionModes.SumMidNFrames;
        public const int DEFAULT_FRAME_SUM_COUNT = 15;
        public const int DEFAULT_MAX_SHIFT_SCANS = 150;
        public const int DEFAULT_SMOOTH_COUNT = 7;

        #region "Enums"

        public enum BaseFrameSelectionModes
        {
            FirstFrame = 0,
            MidpointFrame = 1,
            MaxTICFrame = 2,
            UserSpecifiedFrameRange = 3,
            SumFirstNFrames = 4,
            SumMidNFrames = 5,
            SumFirstNPercent = 6,
            SumMidNPercent = 7,
            SumAll = 8
        }

        public enum AlignmentMethods
        {
            LinearRegression = 0,
            COW = 1
        }

        #endregion

        #region "Properties"

        [Option("Align", HelpText = "Method for aligning the data for each frame to the base frame")]
        public AlignmentMethods AlignmentMethod { get; set; }

        [Option("BaseFrame", HelpText =
            "Method for selecting the base frame to align all the other frames to. Default is /BaseFrame:5")]
        public BaseFrameSelectionModes BaseFrameSelectionMode { get; set; }

        [Option("BaseCount", HelpText =
            "Number of frames to use, or percentage of frames to use, when the BaseFrameSelection mode is NFrames or NPercent. " +
            "When specifying a percentage, must be a value between 1 and 100. Default is 15 frames")]
        public int BaseFrameSumCount { get; set; }

        [Option("BaseStart", HelpText = "First frame to use when the BaseFrameSelection mode is 3, aka UserSpecifiedFrameRange")]
        public int BaseFrameStart { get; set; }

        [Option("BaseEnd", HelpText = "Last frame to use when the BaseFrameSelection mode is 3, aka UserSpecifiedFrameRange")]
        public int BaseFrameEnd { get; set; }

        [Option("Debug", HelpText = "True to show additional debug messages at the console")]
        public bool DebugMode { get; set; }

        [Option("Start", HelpText = "Frame to start processing at (0 to start at the first frame)")]
        public int FrameStart { get; set; }

        [Option("End", HelpText = "Frame to stop processing at (Set FrameStart and FrameEnd to 0 to process all frames)")]
        public int FrameEnd { get; set; }

        [Option("i", "input", ArgPosition = 1, HelpText = "Input file path (UIMF File)")]
        public string InputFilePath { get; set; }

        [Option("o", "output", ArgPosition = 2, HelpText = "Output file path")]
        public string OutputFilePath { get; set; }

        [Option("MaxShift", HelpText = "Maximum number of scans that data in a frame is allowed to be shifted when aligning to the base frame data")]
        public int MaxShiftScans { get; set; }

        [Option("ITF", HelpText = "Value to multiply the maximum TIC value by to determine an intensity threshold, below which intensity values will be set to 0")]
        public double MinimumIntensityThresholdFraction { get; set; }

        [Option("MzMin", HelpText = "Optional minimum m/z to filter data by when obtaining data to align")]
        [Obsolete("Filtering by m/z results in not enough data to align")]
        public double MzFilterMin { get; set; }

        [Option("MzMax", HelpText = "Optional maximum m/z to filter data by when obtaining data to align")]
        [Obsolete("Filtering by m/z results in not enough data to align")]
        public double MzFilterMax { get; set; }

        [Option("ScanMin", HelpText = "Optional minimum drift scan number to filter data by when obtaining data to align")]
        public int DriftScanFilterMin { get; set; }

        [Option("ScanMax", HelpText = "Optional maximum drift scan number to filter data by when obtaining data to align")]
        public int DriftScanFilterMax { get; set; }

        [Option("Smooth", HelpText = "Number of points to use when smoothing TICs before aligning.  If 0 or 1; no smoothing is applied")]
        public int ScanSmoothCount { get; set; }

        [Option("Merge", HelpText =
            "When true, the output file will have a single, merged frame. " +
            "When false, the output file will have all of the original frames, with their IMS drift times aligned")]
        public bool MergeFrames { get; set; }

        [Option("Append", HelpText =
            "When true, a merged frame of data will be appended to the output file as a new frame " +
            "(ignored if option AppendMergedFrame is true)")]
        public bool AppendMergedFrame { get; set; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public FrameAlignmentOptions()
        {
            InputFilePath = string.Empty;
            OutputFilePath = string.Empty;

            AlignmentMethod = AlignmentMethods.LinearRegression;

            BaseFrameSelectionMode = DEFAULT_FRAME_SELECTION_MODE;

            BaseFrameSumCount = DEFAULT_FRAME_SUM_COUNT;
            BaseFrameStart = 0;
            BaseFrameEnd = 0;

            FrameStart = 0;
            FrameEnd = 0;

            MaxShiftScans = DEFAULT_MAX_SHIFT_SCANS;

            MinimumIntensityThresholdFraction = 0.1;

            // MzFilterMin = 0;
            // MzFilterMax = 0;

            DriftScanFilterMin = 0;
            DriftScanFilterMax = 0;

            ScanSmoothCount = DEFAULT_SMOOTH_COUNT;

            MergeFrames = false;

            AppendMergedFrame = false;
        }

        public static string GetAppVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";

            return version;
        }

        public void OutputSetOptions()
        {
            Console.WriteLine("Options:");

            Console.WriteLine(" Reading data from: {0}", InputFilePath);

            if (!string.IsNullOrWhiteSpace(OutputFilePath))
                Console.WriteLine(" Creating file: {0}", OutputFilePath);

            Console.WriteLine();
            Console.WriteLine(" Alignment Method: {0}", AlignmentMethod);
            Console.WriteLine(" Base Frame Selection Mode: {0}", BaseFrameSelectionMode);
            Console.WriteLine(" Base Frames to Sum: {0}", BaseFrameSumCount);

            if (BaseFrameStart > 0 || BaseFrameEnd > 0)
            {
                Console.WriteLine(" Base Frame Start: {0}", BaseFrameStart);
                Console.WriteLine(" Base Frame End: {0}", BaseFrameEnd);
            }

            Console.WriteLine();
            if (FrameStart > 0 || FrameEnd > 0)
            {
                Console.WriteLine(" First frame to process {0}", FrameStart);
                Console.WriteLine(" Last frame to process: {0}", FrameEnd);
            }

            Console.WriteLine(" Maximum shift: {0} scans", MaxShiftScans);
            Console.WriteLine(" Minimum Intensity hreshold Fraction: {0}", MinimumIntensityThresholdFraction);

            //if (MzFilterMin > 0 || MzFilterMax > 0 || DriftScanFilterMin > 0 || DriftScanFilterMax > 0)
            if (DriftScanFilterMin > 0 || DriftScanFilterMax > 0)
            {
                Console.WriteLine();
                Console.WriteLine(" Data selection restrictions for generating the drift time TIC");

                //if (MzFilterMin > 0 || MzFilterMax > 0)
                //{
                //    Console.WriteLine(" Minimum m/z: {0}", MzFilterMin);
                //    Console.WriteLine(" Maximum m/z: {0}", MzFilterMax);
                //}

                if (DriftScanFilterMin > 0 || DriftScanFilterMax > 0)
                {
                    Console.WriteLine(" Minimum drift time scan: {0}", DriftScanFilterMin);
                    Console.WriteLine(" Maximum drift time scan: {0}", DriftScanFilterMax);
                }
            }

            Console.WriteLine(" Scans to smooth: {0}", ScanSmoothCount);
            if (DebugMode)
                Console.WriteLine(" Showing debug messages");

            Console.WriteLine();
            if (MergeFrames)
            {
                Console.WriteLine(" The output file will have a single smoothed frame");
            }
            else
            {
                if (AppendMergedFrame)
                {
                    Console.WriteLine(" The output file will have aligned frames, plus a merged frame appended to the end");
                }
                else
                {
                    Console.WriteLine(" The output file will have aligned frames");
                }
            }

            Console.WriteLine();

        }

        public FrameAlignmentOptions ShallowCopy()
        {
            return (FrameAlignmentOptions)this.MemberwiseClone();
        }

        public bool ValidateArgs()
        {
            if (string.IsNullOrWhiteSpace(InputFilePath))
            {
                Console.WriteLine("You must specify an input file");
                return false;
            }

            return true;
        }
    }
}
