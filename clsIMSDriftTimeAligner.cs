using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using FileProcessor;
using UIMFLibrary;
using MessageEventArgs = FileProcessor.MessageEventArgs;

namespace IMSDriftTimeAligner
{
    class DriftTimeAlignmentEngine
    {
        #region "Constants"

        private const string DEBUG_DATA_FILE = "DebugData.txt";

        private const string BASE_FRAME_DESCRIPTION = "Base frame";

        #endregion

        #region "Structs"

        private struct udtFrameRange
        {
            public int Start;
            public int End;

            public override string ToString()
            {
                if (Start == End)
                    return $"Frame {Start}";
                else
                    return $"Frames {Start} to {End}";
            }
        }

        #endregion

        #region "Classwide variables"

        private bool mSmoothedBaseFrameDataWritten;

        private bool mWarnedScanZeroInBaseFrame;

        private int mWarnCountScanZeroDataFrames;

        #endregion

        #region "Properties"

        /// <summary>
        /// List of recent error messages
        /// </summary>
        /// <remarks>Old messages are cleared when ProcessFile is called</remarks>
        // ReSharper disable once CollectionNeverQueried.Global
        public List<string> ErrorMessages { get; }

        /// <summary>
        /// Alignment options
        /// </summary>
        public FrameAlignmentOptions Options { get; }

        private bool ShowDebugMessages => Options.DebugMode;

        /// <summary>
        /// List of recent warning messages
        /// </summary>
        /// <remarks>Old messages are cleared when ProcessFile is called</remarks>
        // ReSharper disable once CollectionNeverQueried.Global
        public List<string> WarningMessages { get; }

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        public DriftTimeAlignmentEngine(FrameAlignmentOptions options)
        {
            Options = options;
            ErrorMessages = new List<string>();
            WarningMessages = new List<string>();
        }

        /// <summary>
        /// Align the data using piecewise linear correlation optimised warping (COW)
        /// </summary>
        /// <param name="frameNum"></param>
        /// <param name="baseFrameScans"></param>
        /// <param name="frameScans"></param>
        /// <returns></returns>
        [Obsolete("Not implemented")]
        private Dictionary<int, int> AlignFrameDataCOW(
            int frameNum,
            IReadOnlyList<ScanInfo> baseFrameScans,
            IReadOnlyList<ScanInfo> frameScans)
        {
            // TODO: Implement the method shown in the Appendix at http://www.sciencedirect.com/science/article/pii/S0021967398000211

            // See also:
            // http://bmcbioinformatics.biomedcentral.com/articles/10.1186/1471-2105-9-375
            // http://www.sciencedirect.com/science/article/pii/S0021967306007059
            // http://onlinelibrary.wiley.com/doi/10.1002/pmic.200700791/epdf
            // http://onlinelibrary.wiley.com/doi/10.1002/cem.859/epdf
            // http://pubs.acs.org/doi/full/10.1021/ac800920h
            // http://bioinformatics.oxfordjournals.org/content/25/6/758.full

            // COW requires two input parameters:
            // 1) section length m
            // 2) flexibility t/m, where t is the allowed deformation of an individual section

            // m can be estimated from the peak width (as a starting point, use half the peak width of the broadest peak)
            // flexibility should be based on how widely the retention times drift between data to be aligned

            //var N = 50;
            //var LT = 50;
            //var m = 10;
            //var delta = 1;
            //var t = 5;

            //var F = new double[10, 10];
            //var U = new double[10, 10];

            //// Perform dynamic programming

            //for (var i = 0; i < N; i++)
            //{
            //    for (var x = 0; x < LT; x++)
            //    {
            //        F[i, x] = double.MaxValue;
            //    }
            //}

            //F[0, 0] = 0;

            //for (var i = N - 1; i >= 0; i--)
            //{
            //    var xstart = Math.Max(i * (m + delta - t), LT - (N - i) * (m + delta + t));

            //    var xend = Math.Min(i * (m + delta + t), LT - (N - i) * (m + delta - t));

            //    for (var x = xstart; x <= xend; x++)
            //    {
            //        for (var u = delta - t; u <= delta + t; u++)
            //        {
            //            var fsum = F[i+1, x+m+u] + unknownFunc(x);

            //            if (fsum > F[i, x])
            //            {
            //                F[i, x] = fsum;
            //                U[i, x] = u;
            //            }
            //        }
            //    }
            //}

            //// Reconstruct optimal solution
            //var xData = new int[10];
            //xData[0] = 0;

            //var uData = new int[10];

            //for (var i = 0; i < N; i++)
            //{
            //    uData[i] = U[i, xData[i]];

            //    xData[i + 1] = xData[i] + m + uData[i];
            //}

            throw new NotImplementedException();
        }

        //private double unknownFunc(double x)
        //{
        //    return x + m + u;
        //}

        /// <summary>
        /// Align the TIC data in frameData to baseFrameData using Linear Regression
        /// </summary>
        /// <param name="frameNum">Frame number (for logging purposes)</param>
        /// <param name="baseFrameScans">Scans in the base frame</param>
        /// <param name="frameScans">Scans in the frame that we're aligning</param>
        /// <param name="scanNumsInFrame">Full list of scan numbers in the frame (since frameScans might be filtered)</param>
        /// <param name="statsWriter">Stats file writer</param>
        /// <returns>Dictionary where keys are the old scan number and values are the new scan number</returns>
        private Dictionary<int, int> AlignFrameDataLinearRegression(
            int frameNum,
            IReadOnlyList<ScanInfo> baseFrameScans,
            IReadOnlyList<ScanInfo> frameScans,
            IReadOnlyList<int> scanNumsInFrame,
            TextWriter statsWriter)
        {
            // Keys are the old scan number and values are the new scan number
            var frameScanAlignmentMap = new Dictionary<int, int>();

            try
            {
                // Determine the first and last scan number with non-zero TIC values
                var nonzeroScans1 = (from item in baseFrameScans where item.TIC > 0 orderby item.Scan select item.Scan).ToList();
                var nonzeroScans2 = (from item in frameScans where item.TIC > 0 orderby item.Scan select item.Scan).ToList();

                int bestOffset;
                double bestRSquared;
                double[] baseFrameData;
                double[] frameData;
                int scanStart;

                if (nonzeroScans1.Count == 0 || nonzeroScans2.Count == 0)
                {
                    // Either (or both) of the arrays have all zeroes; nothing to align
                    bestOffset = 0;
                    bestRSquared = 0;

                    scanStart = baseFrameScans.First().Scan;
                    var scanEnd = baseFrameScans.Last().Scan;

                    // Populate the arrays, storing TIC values in the appropriate index of baseFrameData and frameData
                    baseFrameData = GetTICValues(BASE_FRAME_DESCRIPTION, scanStart, scanEnd, baseFrameScans);
                    frameData = GetTICValues("Frame " + frameNum, scanStart, scanEnd, frameScans);

                }
                else
                {

                    scanStart = Math.Min(nonzeroScans1.First(), nonzeroScans2.First());
                    var scanEnd = Math.Max(nonzeroScans1.Last(), nonzeroScans2.Last());

                    // Populate the arrays, storing TIC values in the appropriate index of baseFrameData and frameData
                    baseFrameData = GetTICValues(BASE_FRAME_DESCRIPTION, scanStart, scanEnd, baseFrameScans);
                    frameData = GetTICValues("Frame " + frameNum, scanStart, scanEnd, frameScans);

                    var offset = 0;
                    var correlationByOffset = new Dictionary<int, double>();

                    var shiftPositive = true;

                    while (true)
                    {
                        var frameDataShifted = new double[baseFrameData.Length];
                        var targetIndex = 0;

                        for (var sourceIndex = offset; sourceIndex < frameData.Length; sourceIndex++)
                        {
                            if (sourceIndex >= 0)
                            {
                                frameDataShifted[targetIndex] = frameData[sourceIndex];
                            }
                            targetIndex++;
                            if (targetIndex >= frameData.Length)
                                break;
                        }

                        var coeff = MathNet.Numerics.LinearRegression.SimpleRegression.Fit(frameDataShifted, baseFrameData);

                        var slope = coeff.Item1;
                        var intercept = coeff.Item2;

                        var rSquared = MathNet.Numerics.GoodnessOfFit.RSquared(frameDataShifted.Select(x => intercept + slope * x), baseFrameData);

                        correlationByOffset.Add(offset, rSquared);

                        if (shiftPositive)
                        {
                            offset = Math.Abs(offset) + 1;
                            shiftPositive = false;
                        }
                        else
                        {
                            offset = -offset;
                            shiftPositive = true;
                        }

                        if (Math.Abs(offset) > Options.MaxShiftScans)
                        {
                            // Exit the while loop
                            break;
                        }
                    }

                    var rankedOffsets = (from item in correlationByOffset orderby item.Value descending select item).ToList();

                    if (ShowDebugMessages)
                    {
                        Console.WriteLine();
                        ReportMessage("Top 10 offsets:");
                        ReportMessage(string.Format("{0,-12}  {1}", "Offset_Scans", "R-Squared"));
                        for (var i = 0; i < 10; i++)
                        {
                            if (i >= rankedOffsets.Count)
                                break;

                            ReportMessage(string.Format("{0,4:##0}          {1:n6}", rankedOffsets[i].Key, rankedOffsets[i].Value));
                        }

                    }

                    bestOffset = rankedOffsets.First().Key;
                    bestRSquared = rankedOffsets.First().Value;
                }

                foreach (var scanNumber in scanNumsInFrame)
                {
                    var targetScan = scanNumber - bestOffset;
                    if (targetScan >= 0)
                    {
                        frameScanAlignmentMap.Add(scanNumber, targetScan);
                    }
                }

                statsWriter.WriteLine("{0,-8} {1,-6} {2,-8:F5}", frameNum, bestOffset, bestRSquared);

                if (ShowDebugMessages)
                {
                    SaveFrameForDebug("Frame " + frameNum, baseFrameData, frameData, scanStart, frameScanAlignmentMap);
                }

            }
            catch (Exception ex)
            {
                ReportError("Error in AlignFrameDataLinearRegression: " + ex.Message);
                ReportWarning(PRISM.clsStackTraceFormatter.GetExceptionStackTraceMultiLine(ex));
            }

            return frameScanAlignmentMap;
        }

        /// <summary>
        /// Align the TIC data in frameData to baseFrameData
        /// </summary>
        /// <param name="frameNum">Frame number (for logging purposes)</param>
        /// <param name="baseFrameScans">Scans in the base frame</param>
        /// <param name="frameScans">Scans in the frame that we're aligning</param>
        /// <param name="scanNumsInFrame">Full list of scan numbers in the frame (since frameScans might be filtered)</param>
        /// <param name="statsWriter">Stats file writer</param>
        /// <returns>Dictionary where keys are the old scan number and values are the new scan number</returns>
        public Dictionary<int, int> AlignFrameTICToBase(
            int frameNum,
            IReadOnlyList<ScanInfo> baseFrameScans,
            IReadOnlyList<ScanInfo> frameScans,
            IReadOnlyList<int> scanNumsInFrame,
            TextWriter statsWriter)
        {

            Dictionary<int, int> frameScanAlignmentMap;

            switch (Options.AlignmentMethod)
            {
                case FrameAlignmentOptions.AlignmentMethods.LinearRegression:
                    frameScanAlignmentMap = AlignFrameDataLinearRegression(frameNum, baseFrameScans, frameScans, scanNumsInFrame, statsWriter);
                    break;
                case FrameAlignmentOptions.AlignmentMethods.COW:
                    ReportError("Alignment method COW is not implemented");
                    // frameScanAlignmentMap = AlignFrameDataCOW(frameNum, baseFrameScans, frameScans, alignmentOptions);
                    return null;
                default:
                    throw new InvalidEnumArgumentException(
                        "AlignmentMethod");
            }


            return frameScanAlignmentMap;
        }


        /// <summary>
        /// Append a new frame using merged frame data
        /// </summary>
        /// <param name="reader">UIMF Reader</param>
        /// <param name="writer">UIMF Writer</param>
        /// <param name="referenceFrameNum">Frame number to use as reference for frame parameters</param>
        /// <param name="mergedFrameNum">Frame number to use for the merged frame data</param>
        /// <param name="mergedFrameScans">
        /// Single frame of data where scan intensities are accumulated (summed) as each frame is processed
        /// Keys are the aligned scan number, values are intensities by bin
        /// </param>
        private void AppendMergedFrame(
            DataReader reader,
            DataWriter writer,
            int referenceFrameNum,
            int mergedFrameNum,
            Dictionary<int, int[]> mergedFrameScans)
        {
            Console.WriteLine();
            ReportMessage("Appending the merged frame data");

            var frameParams = reader.GetFrameParams(referenceFrameNum);

            writer.InsertFrame(mergedFrameNum, frameParams);

            var binWidth = reader.GetGlobalParams().BinWidth;
            var scansProcessed = 0;

            foreach (var scanItem in mergedFrameScans)
            {
                if (scansProcessed % 250 == 0)
                    ReportMessage($"  storing scan {scanItem.Key}");

                var scanNumNew = scanItem.Key;
                var intensities = scanItem.Value;
                writer.InsertScan(mergedFrameNum, frameParams, scanNumNew, intensities, binWidth);

                scansProcessed++;
            }

        }

        private void ComputeTIC(
            ScanInfo sourceScanInfo,
            out int nonZeroCount, out double totalIntensity)
        {
            nonZeroCount = sourceScanInfo.NonZeroCount;
            totalIntensity = sourceScanInfo.TIC;
        }

        /// <summary>
        /// Compute the IMS-based TIC value across scans
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="baseFrameRange"></param>
        /// <param name="scanNumsInFrame">Full list of scan numbers (unfiltered)</param>
        /// <param name="frameScansSummed">Scan info from the first frame, but with NonZeroCount and TIC values summed across the frames</param>
        /// <remarks>frameScansSummed will be a subset of scans if DriftScanFilterMin or DriftScanFilterMax are greater than 0</remarks>
        private void GetSummedFrameScans(
            DataReader reader,
            udtFrameRange baseFrameRange,
            out List<int> scanNumsInFrame,
            out List<ScanInfo> frameScansSummed)
        {

            var scanMin = Options.DriftScanFilterMin;
            var scanMax = Options.DriftScanFilterMax;
            var scanFilterEnabled = scanMin > 0 || scanMax > 0;

            scanNumsInFrame = new List<int>();
            frameScansSummed = new List<ScanInfo>();

            var baseFrameNum = baseFrameRange.Start;
            var frameScansStart = reader.GetFrameScans(baseFrameNum);

            foreach (var sourceScanInfo in frameScansStart)
            {

                var scanNumber = sourceScanInfo.Scan;
                scanNumsInFrame.Add(scanNumber);

                if (scanFilterEnabled && (scanNumber < scanMin || scanNumber > scanMax))
                    continue;

                frameScansSummed.Add(sourceScanInfo);

            }

            if (baseFrameRange.Start == baseFrameRange.End)
            {
                return;
            }

            // Summing multiple frames
            // Frames can have different scans, so use a dictionary to keep track of data on a per-scan basis
            // Keys are scan number, values are the ScanInfo for that scan
            var scanData = new Dictionary<int, ScanInfo>(frameScansSummed.Count);
            foreach (var scanItem in frameScansSummed)
            {
                scanData.Add(scanItem.Scan, scanItem);
            }

            LookupValidFrameRange(reader, out var frameMin, out var frameMax);

            // Sum the TIC values by IMS frame
            for (var frameNum = baseFrameRange.Start + 1; frameNum <= baseFrameRange.End; frameNum++)
            {
                if (frameNum < frameMin || frameNum > frameMax)
                    continue;

                var frameScans = reader.GetFrameScans(frameNum);

                foreach (var sourceScanInfo in frameScans)
                {

                    var scanNumber = sourceScanInfo.Scan;

                    if (scanFilterEnabled && (scanNumber < scanMin || scanNumber > scanMax))
                        continue;

                    if (scanData.TryGetValue(scanNumber, out var targetScanInfo))
                    {
                        targetScanInfo.NonZeroCount += sourceScanInfo.NonZeroCount;
                        targetScanInfo.TIC += sourceScanInfo.TIC;
                    }
                    else
                    {
                        scanData.Add(scanNumber, sourceScanInfo);
                    }

                }

            }

            frameScansSummed.Clear();
            frameScansSummed.AddRange(from item in scanData orderby item.Key select item.Value);

        }

        /// <summary>
        /// Determine the base frame range
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>Pair of integers representing the start frame and end frame to use for the base frame</returns>
        /// <remarks>Uses Options.BaseFrameSelectionMode</remarks>
        private udtFrameRange GetBaseFrameRange(DataReader reader)
        {
            return GetBaseFrameRange(reader, Options);
        }

        /// <summary>
        /// Determine the base frame range
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="frameAlignmentOptions">Base frame alignment options</param>
        /// <returns>Pair of integers representing the start frame and end frame to use for the base frame</returns>
        private udtFrameRange GetBaseFrameRange(DataReader reader, FrameAlignmentOptions frameAlignmentOptions)
        {
            int baseFrameStart;
            int baseFrameEnd;
            LookupValidFrameRange(reader, out var frameMin, out var frameMax);

            var baseFrameSumCount = frameAlignmentOptions.BaseFrameSumCount;
            if (baseFrameSumCount < 1)
                baseFrameSumCount = 1;

            var frameCountFromPercent = (int)Math.Floor(baseFrameSumCount / 100.0 * (frameMax - frameMin));
            if (frameCountFromPercent < 1)
                frameCountFromPercent = 1;

            switch (frameAlignmentOptions.BaseFrameSelectionMode)
            {
                case FrameAlignmentOptions.BaseFrameSelectionModes.FirstFrame:
                    baseFrameStart = frameMin;
                    baseFrameEnd = frameMin;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.MidpointFrame:
                    baseFrameStart = frameMin + (frameMax - frameMin) / 2;
                    if (baseFrameStart > frameMax)
                    {
                        baseFrameStart = frameMax;
                    }
                    baseFrameEnd = baseFrameStart;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.MaxTICFrame:
                    var ticByFrame = reader.GetTICByFrame(frameMin, frameMax, 0, 0);
                    var frameMaxTIC = (from item in ticByFrame orderby item.Value descending select item).First();
                    baseFrameStart = frameMaxTIC.Key;
                    baseFrameEnd = frameMaxTIC.Key;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.UserSpecifiedFrameRange:
                    baseFrameStart = frameAlignmentOptions.BaseFrameStart;
                    baseFrameEnd = frameAlignmentOptions.BaseFrameEnd;
                    if (baseFrameStart > 0 && (baseFrameEnd <= 0 || baseFrameEnd < baseFrameStart))
                    {
                        baseFrameEnd = baseFrameStart;
                    }

                    if (baseFrameStart <= 0 && baseFrameEnd <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(frameAlignmentOptions.BaseFrameEnd),
                            "BaseFrameEnd must be non-zero when the BaseFrameSelectionMode is UserSpecifiedFrameRange");
                    }
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumFirstNFrames:
                    baseFrameStart = frameMin;
                    baseFrameEnd = frameMin + baseFrameSumCount - 1;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumFirstNPercent:
                    baseFrameStart = frameMin;
                    baseFrameEnd = frameMin + frameCountFromPercent - 1;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumMidNFrames:
                case FrameAlignmentOptions.BaseFrameSelectionModes.SumMidNPercent:

                    // First call this function to determine the midpoint frame number
                    // (Start and End in midpointFrameRange will be identical)
                    var tempOptions = frameAlignmentOptions.ShallowCopy();
                    tempOptions.BaseFrameSelectionMode = FrameAlignmentOptions.BaseFrameSelectionModes.MidpointFrame;
                    var midpointFrameRange = GetBaseFrameRange(reader, tempOptions);

                    int leftCount;
                    int rightCount;

                    if (frameAlignmentOptions.BaseFrameSelectionMode == FrameAlignmentOptions.BaseFrameSelectionModes.SumMidNFrames)
                    {
                        // Mid frames, by frame count
                        leftCount = (int)Math.Floor((baseFrameSumCount - 1) / 2.0);
                        rightCount = baseFrameSumCount - leftCount - 1;
                    }
                    else
                    {
                        // Mid frames, by percentage
                        leftCount = (int)Math.Floor((frameCountFromPercent - 1) / 2.0);
                        rightCount = frameCountFromPercent - leftCount - 1;
                    }

                    baseFrameStart = midpointFrameRange.Start - leftCount;
                    baseFrameEnd = midpointFrameRange.Start + rightCount;
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumAll:
                    baseFrameStart = frameMin;
                    baseFrameEnd = frameMax;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(frameAlignmentOptions.BaseFrameSelectionMode),
                        "Unrecognized value for BaseFrameSelectionMode");
            }

            if (baseFrameEnd < baseFrameStart)
                baseFrameEnd = baseFrameStart;

            if (baseFrameStart < frameMin)
                baseFrameStart = frameMin;

            if (baseFrameEnd > frameMax)
                baseFrameEnd = frameMax;

            var frameRange = new udtFrameRange
            {
                Start = baseFrameStart,
                End = baseFrameEnd
            };

            return frameRange;
        }

        private bool CloneUimf(DataReader reader, FileSystemInfo sourceFile, FileSystemInfo outputFile)
        {
            var tablesToSkip = new List<string> {
                        "Frame_Parameters",
                        "Frame_Params",
                        "Frame_Scans" };

            var frameTypesToAlwaysCopy = new List<DataReader.FrameType>();

            var success = reader.CloneUIMF(outputFile.FullName, tablesToSkip, frameTypesToAlwaysCopy);

            if (success)
                return true;

            ReportError("Error cloning the source UIMF file " + sourceFile.Name);
            return false;
        }

        private void GetFrameRangeToProcess(DataReader reader, out int frameStart, out int frameEnd)
        {
            LookupValidFrameRange(reader, out var frameMin, out var frameMax);

            frameStart = frameMin;
            frameEnd = frameMax;

            if (Options.FrameStart > 0 && Options.FrameStart >= frameMin)
                frameStart = Options.FrameStart;

            if (Options.FrameEnd > 0 && Options.FrameEnd <= frameMax)
                frameEnd = Options.FrameEnd;
        }

        /// <summary>
        /// Populate an array of doubles using the TIC values from the scans in frameScans
        /// </summary>
        /// <param name="frameDescription">Description of this frame</param>
        /// <param name="scanStart">Start scan number for the TIC values to return</param>
        /// <param name="scanEnd">End scan number for the TIC values to return</param>
        /// <param name="frameScans">Scan data for this frame</param>
        /// <returns>TIC values for this frame (possibly smoothed)</returns>
        /// <remarks>
        /// The data in frameScans may have missing scans, but the data in the returned array
        /// will have one point for every scan (0 for the scans that were missing)
        /// </remarks>
        private double[] GetTICValues(
            string frameDescription,
            int scanStart,
            int scanEnd,
            IEnumerable<ScanInfo> frameScans)
        {
            var scanCount = scanEnd - scanStart + 1;
            var frameData = new double[scanCount];

            var warningCountEarly = 0;
            var warningCountLater = 0;

            foreach (var scan in frameScans)
            {
                if (scan.Scan == 0)
                {
                    if (frameDescription == BASE_FRAME_DESCRIPTION)
                    {
                        if (!mWarnedScanZeroInBaseFrame)
                            ReportWarning("Skipping scan 0 in " + BASE_FRAME_DESCRIPTION);

                        mWarnedScanZeroInBaseFrame = true;
                        continue;
                    }

                    mWarnCountScanZeroDataFrames++;
                    if (mWarnCountScanZeroDataFrames <= 5)
                    {
                        var msg = "Skipping scan 0 in " + frameDescription;

                        if (mWarnCountScanZeroDataFrames == 5)
                            msg += "; Suppressing additional warnings";

                        ReportWarning(msg);
                    }

                    continue;
                }

                if (scan.Scan < scanStart)
                {
                    warningCountEarly++;
                    if (warningCountEarly < 5)
                        ReportWarning(string.Format("Scan {0} is less than {1} in {2}; this represents a programming bug", scan.Scan, scanStart, frameDescription));
                    continue;
                }

                if (scan.Scan > scanEnd)
                {
                    warningCountLater++;
                    if (warningCountLater < 5)
                        ReportWarning(string.Format("Scan {0} is greater than {1} in {2}; this represents a programming bug", scan.Scan, scanEnd, frameDescription));
                    continue;
                }

                frameData[scan.Scan - scanStart] = scan.TIC;
            }

            if (Options.ScanSmoothCount <= 1)
            {
                // Not using smoothing, but we may need to zero-out values below a threshold
                ZeroValuesBelowThreshold(frameData);
                return frameData;
            }

            // Apply a moving average smooth
            var frameDataSmoothed = MathNet.Numerics.Statistics.Statistics.MovingAverage(frameData, Options.ScanSmoothCount).ToArray();

            // The smoothing algorithm results in some negative values very close to 0 (like -4.07E-12)
            // Change these to 0
            for (var i = 0; i < frameDataSmoothed.Length; i++)
            {
                if (frameDataSmoothed[i] < 0)
                    frameDataSmoothed[i] = 0;
            }

            ZeroValuesBelowThreshold(frameDataSmoothed);

            if (!ShowDebugMessages)
                return frameDataSmoothed;

            var writeData = true;
            if (frameDescription == BASE_FRAME_DESCRIPTION)
            {
                if (mSmoothedBaseFrameDataWritten)
                {
                    writeData = false;
                }
                else
                {
                    mSmoothedBaseFrameDataWritten = true;
                }
            }

            if (writeData)
            {
                SaveSmoothedDataForDebug(frameDescription, scanStart, frameData, frameDataSmoothed);
            }

            return frameDataSmoothed;
        }

        private FileInfo InitializeOutputFile(FileInfo sourceFile, string outputFilePath)
        {

            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                var outputFileName = Path.GetFileNameWithoutExtension(sourceFile.Name) + "_new" + Path.GetExtension(sourceFile.Name);
                if (sourceFile.DirectoryName == null)
                {
                    outputFilePath = Path.Combine(outputFileName);
                }
                else
                {
                    outputFilePath = Path.Combine(sourceFile.DirectoryName, outputFileName);
                }
            }

            var outputFile = new FileInfo(outputFilePath);
            if (outputFile.Directory != null && !outputFile.Directory.Exists)
            {
                outputFile.Directory.Create();
            }

            if (outputFile.Exists)
            {
                outputFile.Delete();
            }

            return outputFile;
        }

        private void LookupValidFrameRange(DataReader reader, out int frameMin, out int frameMax)
        {
            var masterFrameList = reader.GetMasterFrameList();

            frameMin = masterFrameList.Keys.Min();
            frameMax = masterFrameList.Keys.Max();
        }

        public bool ProcessFile(string inputFilePath, string outputFilePath)
        {

            try
            {
                var sourceFile = new FileInfo(inputFilePath);
                if (!sourceFile.Exists)
                {
                    ReportError("Source file not found: " + inputFilePath);
                    return false;
                }

                mSmoothedBaseFrameDataWritten = false;
                if (ShowDebugMessages)
                {
                    var debugDataFile = new FileInfo(DEBUG_DATA_FILE);
                    if (debugDataFile.Exists)
                        debugDataFile.Delete();
                }

                ReportMessage(string.Format("Opening {0}\n in folder {1}", sourceFile.Name, sourceFile.Directory));
                var outputFile = InitializeOutputFile(sourceFile, outputFilePath);

                if (outputFile.DirectoryName == null)
                    throw new DirectoryNotFoundException("Cannot determine the parent directory of " + outputFile.FullName);

                var statsFilePath = Path.Combine(outputFile.DirectoryName, Path.GetFileNameWithoutExtension(outputFile.Name) + "_stats.txt");

                using (var reader = new DataReader(sourceFile.FullName))
                {
                    reader.ErrorEvent += UIMFReader_ErrorEvent;

                    ReportMessage("Cloning the .UIMF file");
                    var success = CloneUimf(reader, sourceFile, outputFile);
                    if (!success)
                    {
                        // The error message has already been shown
                        return false;
                    }

                    GetFrameRangeToProcess(reader, out var frameStart, out var frameEnd);

                    var baseFrameRange = GetBaseFrameRange(reader);

                    GetSummedFrameScans(reader, baseFrameRange, out _, out var baseFrameScans);

                    if (baseFrameScans.Count == 0)
                    {
                        if (Options.DriftScanFilterMin > 0 || Options.DriftScanFilterMax > 0)
                        {
                            ReportError(string.Format(
                                            "Unable to define the base frame scans. Perhaps the drift scan range is invalid (currently {0} to {1})",
                                            Options.DriftScanFilterMin, Options.DriftScanFilterMax));
                        }
                        else
                        {
                            if (Options.BaseFrameStart > 0 || Options.BaseFrameEnd > 0)
                                ReportError(string.Format(
                                                "Unable to define the base frame scans. Perhaps the base frame range is invalid (currently {0} to {1})",
                                                Options.BaseFrameStart, Options.BaseFrameEnd));
                            else
                                ReportError("Unable to define the base frame scans; check the parameters");

                        }

                        return false;
                    }
                    var mergedFrameScans = new Dictionary<int, int[]>();

                    using (var statsWriter = new StreamWriter(new FileStream(statsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                    using (var writer = new DataWriter(outputFile.FullName))
                    {
                        statsWriter.AutoFlush = true;
                        statsWriter.WriteLine("{0,-8} {1,-6} {2,-8}", "Frame", "Shift", "Best RSquared");

                        if (writer.HasLegacyParameterTables)
                            writer.ValidateLegacyHPFColumnsExist();

                        var nextFrameNumOutfile = 1;
                        bool insertEachFrame;

                        if (Options.MergeFrames && !Options.AppendMergedFrame)
                        {
                            // The output file will only have a single, merged frame
                            insertEachFrame = false;
                        }
                        else
                        {
                            insertEachFrame = true;
                        }


                        for (var frameNum = frameStart; frameNum <= frameEnd; frameNum++)
                        {
                            ProcessFrame(reader, writer, frameNum, baseFrameScans, mergedFrameScans, statsWriter, insertEachFrame, nextFrameNumOutfile);
                            if (insertEachFrame)
                                nextFrameNumOutfile++;
                        }

                        if ((Options.AppendMergedFrame || Options.MergeFrames) && mergedFrameScans.Count > 0)
                        {
                            const int referenceFrameNum = 1;

                            AppendMergedFrame(reader, writer, referenceFrameNum, nextFrameNumOutfile, mergedFrameScans);
                        }

                        // Make sure the frame count in Global_Params is up-to-date
                        writer.UpdateGlobalFrameCount();

                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Error in ProcessFile: " + ex.Message);
                ReportMessage(ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Read the frame data from the source file, align it to baseFramedata, and write to the output file
        /// </summary>
        /// <param name="reader">UIMF Reader</param>
        /// <param name="writer">UIMF Writer</param>
        /// <param name="frameNum">Frame Number</param>
        /// <param name="baseFrameScans">Base frame scan data (most importantly, TIC by scan number)</param>
        /// <param name="mergedFrameScans">
        /// Single frame of data where scan intensities are accumulated (summed) as each frame is processed
        /// Keys are the aligned scan number, values are intensities by bin
        /// </param>
        /// <param name="statsWriter">Stats file writer</param>
        /// <param name="insertFrame">True to insert a new frame</param>
        /// <param name="nextFrameNumOutfile">Next frame number for the writer to use</param>
        private void ProcessFrame(
            DataReader reader,
            DataWriter writer,
            int frameNum,
            IReadOnlyList<ScanInfo> baseFrameScans,
            IDictionary<int, int[]> mergedFrameScans,
            TextWriter statsWriter,
            bool insertFrame,
            int nextFrameNumOutfile
            )
        {
            Console.WriteLine();
            ReportMessage($"Process frame {frameNum}");

            try
            {
                var udtCurrentFrameRange = new udtFrameRange
                {
                    Start = nextFrameNumOutfile,
                    End = nextFrameNumOutfile
                };

                GetSummedFrameScans(reader, udtCurrentFrameRange, out var scanNumsInFrame, out var frameScans);

                // Dictionary where keys are the old scan number and values are the new scan number
                var frameScanAlignmentMap = AlignFrameTICToBase(frameNum, baseFrameScans, frameScans, scanNumsInFrame, statsWriter);
                if (frameScanAlignmentMap == null)
                    return;

                var frameParams = reader.GetFrameParams(frameNum);

                // Assure that the frame type is not 0
                var frameType = frameParams.GetValueInt32(FrameParamKeyType.FrameType, 0);
                if (frameType == 0)
                {
                    frameParams.AddUpdateValue(FrameParamKeyType.FrameType, (int)UIMFData.FrameType.MS1);
                }

                if (insertFrame)
                {
                    writer.InsertFrame(nextFrameNumOutfile, frameParams);
                }

                var scanFilterEnabled = Options.DriftScanFilterMin > 0 || Options.DriftScanFilterMax > 0;

                var binWidth = reader.GetGlobalParams().BinWidth;
                var scansProcessed = 0;

                foreach (var scanNumber in scanNumsInFrame)
                {
                    if (scansProcessed % 250 == 0)
                        ReportMessage($"  storing scan {scanNumber}");

                    var scanNumOld = scanNumber;

                    if (!frameScanAlignmentMap.TryGetValue(scanNumOld, out var scanNumNew))
                        continue;

                    int[] intensities;
                    try
                    {
                        intensities = reader.GetSpectrumAsBins(frameNum, frameParams.FrameType, scanNumOld);
                    }
                    catch (Exception ex)
                    {
                        ReportError($"Error retrieving data for frame {frameNum}, scan {scanNumOld}: {ex.Message}");
                        continue;
                    }

                    if (insertFrame)
                    {
                        writer.InsertScan(nextFrameNumOutfile, frameParams, scanNumNew, intensities, binWidth);
                    }

                    scansProcessed++;

                    if (!Options.AppendMergedFrame && !Options.MergeFrames)
                        continue;

                    if (scanFilterEnabled)
                    {
                        if (scanNumNew < Options.DriftScanFilterMin || scanNumNew > Options.DriftScanFilterMax)
                            continue;
                    }

                    // Dictionary where Keys are the aligned scan number and values are intensities by bin
                    if (mergedFrameScans.TryGetValue(scanNumNew, out var summedIntensities))
                    {
                        for (var i = 0; i < summedIntensities.Length; i++)
                        {
                            if (i >= intensities.Length)
                                break;

                            if (summedIntensities[i] + (long)intensities[i] > int.MaxValue)
                                summedIntensities[i] = int.MaxValue;
                            else
                                summedIntensities[i] += intensities[i];
                        }
                    }
                    else
                    {
                        mergedFrameScans.Add(scanNumNew, intensities);
                    }

                }

            }
            catch (Exception ex)
            {
                ReportError("Error in ProcessFrame: " + ex.Message);
            }
        }

        private void ReportError(string message)
        {
            OnErrorMessage(new MessageEventArgs(message));
            ErrorMessages.Add(message);
        }

        private void ReportMessage(string message)
        {
            OnMessage(new MessageEventArgs(message));
        }

        private void ReportWarning(string message)
        {
            OnWarningMessage(new MessageEventArgs(message));
            WarningMessages.Add(message);
        }

        private void SaveFrameForDebug(
            string frameDescription,
            IReadOnlyList<double> baseFrameData,
            IReadOnlyList<double> frameData,
            int scanStart,
            IReadOnlyDictionary<int, int> frameScanAlignmentMap)
        {
            try
            {
                var debugDataFile = new FileInfo(DEBUG_DATA_FILE);

                using (var writer = new StreamWriter(new FileStream(debugDataFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    writer.WriteLine(frameDescription);

                    // Construct a mapping of the existing indices in frameData to where the TIC value for that index would be shifted to using frameScanAlignmentMap
                    var targetIndex = new int[frameData.Count];

                    var scanShiftStats = new Dictionary<int, int>();

                    for (var i = 0; i < frameData.Count; i++)
                    {
                        var scanNumOld = scanStart + i;

                        if (!frameScanAlignmentMap.TryGetValue(scanNumOld, out var scanNumNew))
                        {
                            targetIndex[i] = -1;
                            continue;
                        }

                        var scanShift = scanNumNew - scanStart;
                        if (scanShiftStats.TryGetValue(scanShift, out var scanShiftCount))
                        {
                            scanShiftStats[scanShift] = scanShiftCount + 1;
                        }
                        else
                        {
                            scanShiftStats.Add(scanShift, 1);
                        }

                        targetIndex[i] = scanNumNew - scanStart;

                    }

                    var highestScanShiftCount = scanShiftStats.Values.Max();
                    var scanShiftApplied = (from item in scanShiftStats where item.Value == highestScanShiftCount select item.Key).First();

                    switch (scanShiftApplied)
                    {
                        case 0:
                            ReportMessage($"Data in {frameDescription} will not be shifted");
                            break;
                        case 1:
                            ReportMessage($"Data in {frameDescription} will be shifted by 1 scan");
                            break;
                        default:
                            ReportMessage($"Data in {frameDescription} will be shifted by {scanShiftApplied} scans");
                            break;
                    }


                    writer.WriteLine("ScanShift\t{0}\tscans", scanShiftApplied);

                    writer.WriteLine("{0}\t{1}\t{2}\t{3}", "Scan", "TIC_Base", "TIC_Compare_Offset", "TIC_Compare_Original");

                    // Use the mapping in targetIndex to populate frameDataOffset
                    var frameDataOffset = new double[frameData.Count];
                    for (var i = 0; i < frameData.Count; i++)
                    {
                        if (targetIndex[i] < 0 || targetIndex[i] >= frameData.Count)
                            continue;

                        frameDataOffset[targetIndex[i]] = frameData[i];
                    }

                    for (var i = 0; i < frameData.Count; i++)
                    {
                        writer.WriteLine("{0}\t{1}\t{2}\t{3}", scanStart + i, baseFrameData[i], frameDataOffset[i], frameData[i]);
                    }
                    writer.WriteLine();
                }

            }
            catch (Exception ex)
            {
                ReportError("Error in SaveFrameForDebug: " + ex.Message);
            }
        }

        private void SaveSmoothedDataForDebug(
            string frameDescription,
            int scanStart,
            IReadOnlyList<double> frameData,
            IReadOnlyList<double> frameDataSmoothed)
        {
            try
            {
                var debugDataFile = new FileInfo(DEBUG_DATA_FILE);

                using (var writer = new StreamWriter(new FileStream(debugDataFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    writer.WriteLine("Data smoothing comparison for " + frameDescription);
                    writer.WriteLine("{0}\t{1}\t{2}", "Scan", "TIC_Original", "TIC_Smoothed");

                    for (var i = 0; i < frameData.Count; i++)
                    {
                        writer.WriteLine("{0}\t{1}\t{2}", scanStart + i, frameData[i], frameDataSmoothed[i]);
                    }
                    writer.WriteLine();
                }

            }
            catch (Exception ex)
            {
                ReportError("Error in SaveSmoothedDataForDebug: " + ex.Message);
            }
        }

        private void ZeroValuesBelowThreshold(IList<double> frameData)
        {
            var thresholdFraction = Options.MinimumIntensityThresholdFraction;
            if (thresholdFraction <= 0)
                return;

            var maxIntensity = frameData.Max();
            var intensityThreshold = maxIntensity * thresholdFraction;

            for (var i = 0; i < frameData.Count; i++)
            {
                if (frameData[i] < intensityThreshold)
                    frameData[i] = 0;
            }

        }

        #endregion

        #region "Events"

        public event MessageEventHandler ErrorEvent;
        public event MessageEventHandler MessageEvent;
        public event MessageEventHandler WarningEvent;

        #endregion

        #region "Event Handlers"

        private void OnErrorMessage(MessageEventArgs e)
        {
            ErrorEvent?.Invoke(this, e);
        }

        private void OnMessage(MessageEventArgs e)
        {
            MessageEvent?.Invoke(this, e);
        }

        private void OnWarningMessage(MessageEventArgs e)
        {
            WarningEvent?.Invoke(this, e);
        }

        private void UIMFReader_ErrorEvent(object sender, UIMFLibrary.MessageEventArgs e)
        {
            ReportError("UIMFReader error: " + e.Message);
        }
        #endregion
    }
}
