﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using NDtw;
using NDtw.Preprocessing;
using OxyPlot;
using OxyPlot.Wpf;
using PRISM;
using UIMFLibrary;
using DataPointSeries = OxyPlot.DataPointSeries;
using LinearAxis = OxyPlot.LinearAxis;
using LineSeries = OxyPlot.LineSeries;

namespace IMSDriftTimeAligner
{
    /// <summary>
    /// Drift time alignment engine
    /// </summary>
    public class DriftTimeAlignmentEngine : EventNotifier
    {
        // Ignore Spelling: bak, crosstab, Func, Nums, Prescan, Sakoe

        #region "Constants"

        private const string DEBUG_DATA_FILE = "DebugData.txt";

        private const string DTW_DEBUG_DATA_FILE = "DebugDataDTW.txt";

        private const string BASE_FRAME_DESCRIPTION = "Base frame";

        /// <summary>
        /// Suffix to add to output files
        /// </summary>
        public const string OUTPUT_FILENAME_SUFFIX = "_new";

        /// <summary>
        /// Suffix to use when backing up a file by renaming it
        /// </summary>
        public const string BACKUP_FILENAME_SUFFIX = "_bak";

        #endregion

        #region "Class wide variables"

        private bool mSmoothedBaseFrameDataWritten;

        private bool mWarnedScanZeroInBaseFrame;

        private int mWarnCountScanZeroDataFrames;

        /// <summary>
        /// Cached ScanStats
        /// Keys in this dictionary are based on the frame number and scan number, for example: Frame20_Scan400
        /// Values are filtered TIC values
        /// </summary>
        /// <remarks>This dictionary is used by ComputeFilteredTICAndBPI when filtering by m/z</remarks>
        private readonly Dictionary<string, ScanStats> mFrameScanStats;

        #endregion

        #region "Properties"

        /// <summary>
        /// Original command line passed to the entry class
        /// </summary>
        public string CommandLine { get; }

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
        public DriftTimeAlignmentEngine(FrameAlignmentOptions options, string commandLine = "")
        {
            CommandLine = commandLine;
            Options = options;
            ErrorMessages = new List<string>();
            WarningMessages = new List<string>();
            mFrameScanStats = new Dictionary<string, ScanStats>();
        }

        /// <summary>
        /// Align the data using piecewise linear correlation optimized warping (COW)
        /// </summary>
        /// <param name="frameNum"></param>
        /// <param name="baseFrameScans"></param>
        /// <param name="frameScans"></param>
        /// <exception cref="NotImplementedException"></exception>
        [Obsolete("Not implemented")]
        private Dictionary<int, int> AlignFrameDataCOW(
            int frameNum,
            IReadOnlyList<ScanInfo> baseFrameScans,
            IReadOnlyList<ScanInfo> frameScans)
        {
            // Possibly future task: Implement the method shown in the Appendix at http://www.sciencedirect.com/science/article/pii/S0021967398000211

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
            //    var xStart = Math.Max(i * (m + delta - t), LT - (N - i) * (m + delta + t));

            //    var xEnd = Math.Min(i * (m + delta + t), LT - (N - i) * (m + delta - t));

            //    for (var x = xStart; x <= xEnd; x++)
            //    {
            //        for (var u = delta - t; u <= delta + t; u++)
            //        {
            //            var fSum = F[i+1, x+m+u] + unknownFunc(x);

            //            if (fSum > F[i, x])
            //            {
            //                F[i, x] = fSum;
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
        /// <param name="comparisonFrameNum">Frame number (for logging purposes)</param>
        /// <param name="baseFrameScans">Scans in the base frame</param>
        /// <param name="frameScans">Scans in the frame that we're aligning</param>
        /// <param name="scanNumsInFrame">Full list of scan numbers in the frame (since frameScans might be filtered)</param>
        /// <param name="statsWriter">Stats file writer</param>
        /// <param name="datasetName"></param>
        /// <param name="outputDirectory">Output directory</param>
        /// <returns>Dictionary where keys are the old scan number and values are the new scan number</returns>
        private Dictionary<int, int> AlignFrameData(
            int comparisonFrameNum,
            IReadOnlyList<ScanInfo> baseFrameScans,
            IReadOnlyList<ScanInfo> frameScans,
            IEnumerable<int> scanNumsInFrame,
            StatsWriter statsWriter,
            string datasetName,
            FileSystemInfo outputDirectory)
        {
            try
            {
                // Determine the first and last scan number with non-zero TIC values
                var nonzeroScans1 = (from item in baseFrameScans where item.TIC > 0 orderby item.Scan select item.Scan).ToList();
                var nonzeroScans2 = (from item in frameScans where item.TIC > 0 orderby item.Scan select item.Scan).ToList();

                List<double> baseFrameData;
                List<double> comparisonFrameData;
                int scanStart;
                int scanEnd;

                // Keys are the old scan number and values are the new scan number
                Dictionary<int, int> frameScanAlignmentMap;

                if (nonzeroScans1.Count == 0 || nonzeroScans2.Count == 0)
                {
                    // Either (or both) of the arrays have all zeros; nothing to align
                    frameScanAlignmentMap = new Dictionary<int, int>();

                    scanStart = baseFrameScans.First().Scan;
                    scanEnd = baseFrameScans.Last().Scan;

                    // Populate the arrays, storing TIC values in the appropriate index of baseFrameData and comparisonFrameData
                    baseFrameData = GetTICValues(outputDirectory, BASE_FRAME_DESCRIPTION, scanStart, scanEnd, baseFrameScans);
                    comparisonFrameData = GetTICValues(outputDirectory, "Frame " + comparisonFrameNum, scanStart, scanEnd, frameScans);
                }
                else
                {
                    scanStart = Math.Min(nonzeroScans1.First(), nonzeroScans2.First());
                    scanEnd = Math.Max(nonzeroScans1.Last(), nonzeroScans2.Last());

                    var dataSourceDescription = "Frame " + comparisonFrameNum;

                    // Populate the arrays, storing TIC values in the appropriate index of baseFrameData and comparisonFrameData
                    baseFrameData = GetTICValues(outputDirectory, BASE_FRAME_DESCRIPTION, scanStart, scanEnd, baseFrameScans);
                    comparisonFrameData = GetTICValues(outputDirectory, dataSourceDescription, scanStart, scanEnd, frameScans);

                    if (Options.AlignmentMethod == FrameAlignmentOptions.AlignmentMethods.LinearRegression)
                    {
                        frameScanAlignmentMap = AlignFrameDataLinearRegression(
                            comparisonFrameNum, comparisonFrameData,
                            baseFrameData, scanNumsInFrame,
                            statsWriter);
                    }
                    else if (Options.AlignmentMethod == FrameAlignmentOptions.AlignmentMethods.DynamicTimeWarping)
                    {
                        var pngFileName = string.Format("{0}_Frame{1}.png", datasetName, comparisonFrameNum);
                        var pngFileInfo = new FileInfo(Path.Combine(outputDirectory.FullName, pngFileName));

                        frameScanAlignmentMap = AlignFrameDataDTW(
                            "Frame",
                            comparisonFrameNum, comparisonFrameData,
                            baseFrameData, scanNumsInFrame,
                            statsWriter, scanStart, scanEnd,
                            outputDirectory,
                            pngFileInfo);
                    }
                    else
                    {
                        frameScanAlignmentMap = new Dictionary<int, int>();
                    }
                }

                if (ShowDebugMessages)
                {
                    SaveAlignedData("Frame " + comparisonFrameNum,
                                      baseFrameData, comparisonFrameData, scanStart,
                                      frameScanAlignmentMap, outputDirectory, null);
                }

                return frameScanAlignmentMap;
            }
            catch (Exception ex)
            {
                ReportError("Error in AlignFrameData", ex);
                return new Dictionary<int, int>();
            }
        }

        /// <summary>
        /// Align the TIC values in comparisonFrameData to baseFrameData
        /// </summary>
        /// <param name="dataSourceDescription">Frame number or column number</param>
        /// <param name="comparisonFrameNum">Frame number (for logging purposes)</param>
        /// <param name="comparisonFrameData">TIC values from the comparison frame</param>
        /// <param name="baseFrameData">TIC values from the base frame</param>
        /// <param name="scanNumsInFrame">Full list of scan numbers in the frame (since frameScans might be filtered)</param>
        /// <param name="statsWriter">Stats file writer</param>
        /// <param name="scanStart">First scan of the data in comparisonFrameData (and also baseFrameData)</param>
        /// <param name="scanEnd">Last scan of the data in comparisonFrameData (and also baseFrameData)</param>
        /// <param name="outputDirectory">Output directory</param>
        /// <param name="pngFileInfo">File info for the PNG file to create if plot file saving is enabled</param>
        /// <returns>Dictionary where keys are the old scan number and values are the new scan number</returns>
        public Dictionary<int, int> AlignFrameDataDTW(
            string dataSourceDescription,
            int comparisonFrameNum,
            List<double> comparisonFrameData,
            List<double> baseFrameData,
            IEnumerable<int> scanNumsInFrame,
            StatsWriter statsWriter,
            int scanStart,
            int scanEnd,
            FileSystemInfo outputDirectory,
            FileSystemInfo pngFileInfo)
        {
            // Keys are the old scan number and values are the new scan number (in the base frame)
            var frameScanAlignmentMap = new Dictionary<int, int>();

            try
            {
                double[] baseDataToUse;
                double[] comparisonDataToUse;
                bool dataIsCompressed;
                int sampleLength;

                if (baseFrameData.Count > Options.DTWMaxPoints)
                {
                    // Compress the data to limit the size of the matrices used by the Dynamic Time Warping algorithm

                    sampleLength = (int)Math.Ceiling(baseFrameData.Count / (double)Options.DTWMaxPoints);

                    baseDataToUse = CompressArrayBySumming(baseFrameData, sampleLength);
                    comparisonDataToUse = CompressArrayBySumming(comparisonFrameData, sampleLength);
                    dataIsCompressed = true;
                }
                else
                {
                    baseDataToUse = baseFrameData.ToArray();
                    comparisonDataToUse = comparisonFrameData.ToArray();
                    dataIsCompressed = false;
                    sampleLength = 1;
                }

                var normalizer = new NormalizationPreprocessor();

                var xSeries = new[] { new SeriesVariable(comparisonDataToUse, baseDataToUse, "IntensityVsDriftTime", normalizer) };

                var sakoeChibaMaxShift = Math.Max(2, (int)Math.Round(baseDataToUse.Length * Options.DTWSakoeChibaMaxShiftPercent / 100.0));

                // Map the comparison data TIC values onto the base data TIC values
                var dtwAligner = new Dtw(xSeries, DistanceMeasure.Euclidean, sakoeChibaMaxShift: sakoeChibaMaxShift);

                var cost = dtwAligner.GetCost();

                // The alignment path will range from 0 to baseDataToUse.Length - 1
                var alignmentPath = dtwAligner.GetPath();

                // Populate frameScanAlignmentMap

                // Keys in this dictionary are source scan number
                // Values are the list of target scan numbers (ideally just one, but dynamic time warping will sometimes duplicate the X values)
                var scanInfoFromDTW = new Dictionary<int, List<int>>();

                // Mapping from source scan to target scan
                var alignmentPathAllScans = new List<Tuple<int, int>>();

                if (dataIsCompressed)
                {
                    // Data was compressed
                    // Need to transform the indices in alignmentPath to actual scan numbers

                    for (var i = 0; i < alignmentPath.Length - 1; i++)
                    {
                        var currentPoint = alignmentPath[i];
                        var nextPoint = alignmentPath[i + 1];

                        if (currentPoint.Item1 == nextPoint.Item1)
                        {
                            // Data like:
                            // ComparisonData  BaseFrameData
                            // 23              14
                            // 23              15
                            // 23              16

                            // Insert items to alignmentPathAllScans using 23 * 5 for Item1 of each entry, and incrementing values for Item2, starting at 14 * 5
                            // Example inserted data:
                            //
                            // ComparisonData  BaseFrameData
                            // 115             65
                            // 115             66
                            // 115             67
                            // 115             68
                            // 115             69

                            var rescaledSourceScan = currentPoint.Item1 * sampleLength;
                            var rescaledTargetScanStart = currentPoint.Item2 * sampleLength;
                            for (var j = 0; j < sampleLength; j++)
                            {
                                alignmentPathAllScans.Add(new Tuple<int, int>(rescaledSourceScan, rescaledTargetScanStart + j));
                            }
                        }
                        else if (currentPoint.Item2 == nextPoint.Item2)
                        {
                            // Data like:
                            // ComparisonData  BaseFrameData
                            // 60              72
                            // 61              72
                            // 62              72

                            // Insert items to alignmentPathAllScans using 72 * 5 for Item2 of each entry, and incrementing values for Item1, starting at 60 * 5
                            // Example inserted data:
                            //
                            // ComparisonData  BaseFrameData
                            // 300             360
                            // 301             360
                            // 302             360
                            // 303             360
                            // 304             360

                            var rescaledSourceScan = currentPoint.Item1 * sampleLength;
                            var rescaledTargetScanStart = currentPoint.Item2 * sampleLength;
                            for (var j = 0; j < sampleLength; j++)
                            {
                                alignmentPathAllScans.Add(new Tuple<int, int>(rescaledSourceScan + j, rescaledTargetScanStart));
                            }
                        }
                        else
                        {
                            // Data like:
                            // ComparisonData  BaseFrameData
                            // 285             236
                            // 286             237
                            // 287             238

                            // Insert items to alignmentPathAllScans using incrementing values for Item1 and Item2 for each entry
                            // Example inserted data:
                            //
                            // ComparisonData  BaseFrameData
                            // 1425            1180
                            // 1426            1181
                            // 1427            1182
                            // 1428            1183
                            // 1429            1184

                            var rescaledSourceScan = currentPoint.Item1 * sampleLength;
                            var rescaledTargetScanStart = currentPoint.Item2 * sampleLength;
                            for (var j = 0; j < sampleLength; j++)
                            {
                                alignmentPathAllScans.Add(new Tuple<int, int>(rescaledSourceScan + j, rescaledTargetScanStart + j));
                            }
                        }
                    }

                    // Add the final mapping point
                    var finalPoint = alignmentPath[alignmentPath.Length - 1];
                    alignmentPathAllScans.Add(new Tuple<int, int>(finalPoint.Item1 * sampleLength, finalPoint.Item2 * sampleLength));
                }
                else
                {
                    // Data was not compressed
                    // Can use the indices in alignmentPath as-is
                    foreach (var alignedPoint in alignmentPath)
                    {
                        alignmentPathAllScans.Add(alignedPoint);
                    }
                }

                foreach (var alignedPoint in alignmentPathAllScans)
                {
                    var comparisonFrameScan = alignedPoint.Item1 + scanStart;
                    var baseFrameScan = alignedPoint.Item2 + scanStart;

                    if (scanInfoFromDTW.TryGetValue(comparisonFrameScan, out var mappedValues))
                    {
                        mappedValues.Add(baseFrameScan);
                    }
                    else
                    {
                        scanInfoFromDTW.Add(comparisonFrameScan, new List<int> { baseFrameScan });
                    }
                }

                // For each source scan, compute the average target scan value (base frame scan) in scanInfoFromDTW
                var consolidatedScanInfoFromDTW = new Dictionary<int, int>();

                foreach (var comparisonFrameScan in scanInfoFromDTW.Keys.ToList())
                {
                    var mappedValues = scanInfoFromDTW[comparisonFrameScan];
                    if (mappedValues.Count == 1)
                    {
                        consolidatedScanInfoFromDTW.Add(comparisonFrameScan, mappedValues.First());
                        continue;
                    }

                    var average = mappedValues.Average();
                    consolidatedScanInfoFromDTW.Add(comparisonFrameScan, (int)Math.Round(average));
                }

                // Populate a dictionary listing source scan number, and the offset to apply to obtain the target scan number

                var offsetsBySourceScan = new Dictionary<int, int>();

                foreach (var item in consolidatedScanInfoFromDTW)
                {
                    var offset = item.Value - item.Key;
                    offsetsBySourceScan.Add(item.Key, offset);
                }

                // Compute a moving average of the offsets in offsetsBySourceScan
                // For the smooth length, use the total number of scans divided by 100
                var pointsForSmooth = Math.Max(1, (int)(offsetsBySourceScan.Count / 100.0));

                // Keys in this dictionary are source scan; values are the offset to apply to obtain the target scan
                var offsetsBySourceScanSmoothed = SmoothViaMovingAverage(offsetsBySourceScan, pointsForSmooth);

                var searcher = new clsBinarySearchFindNearest();
                searcher.AddData(offsetsBySourceScanSmoothed);

                var optimizedOffsetsBySourceScan = OptimizeOffsetsUsingPeaks(comparisonFrameData, scanStart, offsetsBySourceScanSmoothed, searcher);

                // Initialize a dictionary for keeping track of average scan shift by percentile
                // Keys are percentile, values are the list of offsets for the scans in that percentile
                var offsetsByPercentile = new Dictionary<int, List<int>>();
                var scanCountInFrame = Math.Max(1, scanEnd - scanStart);

                // Populate frameScanAlignmentMap and offsetsByPercentile
                foreach (var scanNumber in scanNumsInFrame)
                {
                    var offset = GetOffsetForSourceScan(scanNumber, optimizedOffsetsBySourceScan, searcher);
                    var targetScan = scanNumber + offset;

                    frameScanAlignmentMap.Add(scanNumber, targetScan);

                    var percentile = 10 * (int)Math.Round((scanNumber - scanStart) / (double)scanCountInFrame * 10, 0);

                    if (offsetsByPercentile.TryGetValue(percentile, out var offsetList))
                    {
                        offsetList.Add(offset);
                    }
                    else
                    {
                        offsetsByPercentile.Add(percentile, new List<int> { offset });
                    }
                }

                statsWriter.AppendStats(comparisonFrameNum, cost, offsetsByPercentile);

                if (Options.VisualizeDTW || Options.SaveDTWPlots || Options.SavePlotData)
                {
                    VisualizeDTWAlignment(
                        dataSourceDescription,
                        comparisonFrameNum,
                        comparisonFrameData,
                        scanStart,
                        offsetsBySourceScan,
                        offsetsBySourceScanSmoothed,
                        optimizedOffsetsBySourceScan,
                        dtwAligner,
                        baseDataToUse,
                        sakoeChibaMaxShift,
                        pngFileInfo);
                }

                if (ShowDebugMessages)
                {
                    SaveDynamicTimeWarpingDataForDebug(outputDirectory,
                        "Frame " + comparisonFrameNum,
                        cost, alignmentPath,
                        consolidatedScanInfoFromDTW,
                        offsetsBySourceScanSmoothed,
                        frameScanAlignmentMap);
                }
            }
            catch (Exception ex)
            {
                ReportError("Error in AlignFrameDataDTW", ex);
            }

            PRISM.ProgRunner.GarbageCollectNow();

            return frameScanAlignmentMap;
        }

        /// <summary>
        /// Align the TIC data in comparisonFrameData to baseFrameData using Linear Regression
        /// </summary>
        /// <param name="comparisonFrameNum">Frame number (for logging purposes)</param>
        /// <param name="comparisonFrameData">TIC values from the comparison frame</param>
        /// <param name="baseFrameData">TIC values from the base frame</param>
        /// <param name="scanNumsInFrame">Full list of scan numbers in the frame (since frameScans might be filtered)</param>
        /// <param name="statsWriter">Stats file writer</param>
        /// <returns>Dictionary where keys are the old scan number and values are the new scan number</returns>
        public Dictionary<int, int> AlignFrameDataLinearRegression(
            int comparisonFrameNum,
            IReadOnlyList<double> comparisonFrameData,
            List<double> baseFrameData,
            IEnumerable<int> scanNumsInFrame,
            StatsWriter statsWriter)
        {
            // Keys are the old scan number and values are the new scan number
            var frameScanAlignmentMap = new Dictionary<int, int>();

            try
            {
                var offset = 0;

                // Keys in this dictionary are offset values, values are R-squared
                var correlationByOffset = new Dictionary<int, double>();

                var shiftPositive = true;

                do
                {
                    var frameDataShifted = new double[baseFrameData.Count];
                    var targetIndex = 0;

                    for (var sourceIndex = offset; sourceIndex < comparisonFrameData.Count; sourceIndex++)
                    {
                        if (sourceIndex >= 0)
                        {
                            frameDataShifted[targetIndex] = comparisonFrameData[sourceIndex];
                        }

                        targetIndex++;
                        if (targetIndex >= comparisonFrameData.Count)
                            break;
                    }

                    var coeff = MathNet.Numerics.LinearRegression.SimpleRegression.Fit(frameDataShifted, baseFrameData.ToArray());

                    var intercept = coeff.Item1;
                    var slope = coeff.Item2;

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
                }
                while (Math.Abs(offset) <= Options.MaxShiftScans);

                var rankedOffsets = (from item in correlationByOffset orderby item.Value descending select item).ToList();

                if (ShowDebugMessages)
                {
                    Console.WriteLine();
                    OnStatusEvent("Top 10 offsets:");
                    OnStatusEvent(string.Format("{0,-12}  {1}", "Offset_Scans", "R-Squared"));
                    for (var i = 0; i < 10; i++)
                    {
                        if (i >= rankedOffsets.Count)
                            break;

                        OnStatusEvent(string.Format("{0,4:##0}          {1:n6}", rankedOffsets[i].Key, rankedOffsets[i].Value));
                    }
                }

                var bestOffset = rankedOffsets.First().Key;
                var bestRSquared = rankedOffsets.First().Value;

                foreach (var scanNumber in scanNumsInFrame)
                {
                    var targetScan = scanNumber - bestOffset;
                    if (targetScan >= 0)
                    {
                        frameScanAlignmentMap.Add(scanNumber, targetScan);
                    }
                }

                statsWriter.AppendStats(comparisonFrameNum, bestOffset, bestRSquared);

                OnStatusEvent(string.Format("  R-squared {0:F3}, shift {1} scans", bestRSquared, bestOffset));
            }
            catch (Exception ex)
            {
                ReportError("Error in AlignFrameDataLinearRegression", ex);
            }

            return frameScanAlignmentMap;
        }

        /// <summary>
        /// Align the TIC data in frameData to baseFrameData
        /// </summary>
        /// <param name="comparisonFrameNum">Frame number (for logging purposes)</param>
        /// <param name="baseFrameScans">Scans in the base frame</param>
        /// <param name="frameScans">Scans in the frame that we're aligning</param>
        /// <param name="scanNumsInFrame">Full list of scan numbers in the frame (since frameScans might be filtered)</param>
        /// <param name="statsWriter">Stats file writer</param>
        /// <param name="datasetName"></param>
        /// <param name="outputDirectory">Output directory</param>
        /// <returns>Dictionary where keys are the old scan number and values are the new scan number</returns>
        public Dictionary<int, int> AlignFrameTICToBase(
            int comparisonFrameNum,
            IReadOnlyList<ScanInfo> baseFrameScans,
            IReadOnlyList<ScanInfo> frameScans,
            IReadOnlyList<int> scanNumsInFrame,
            StatsWriter statsWriter,
            string datasetName,
            DirectoryInfo outputDirectory)
        {
            if (!outputDirectory.Exists)
                outputDirectory.Create();

            Dictionary<int, int> frameScanAlignmentMap;

            switch (Options.AlignmentMethod)
            {
                case FrameAlignmentOptions.AlignmentMethods.LinearRegression:
                case FrameAlignmentOptions.AlignmentMethods.DynamicTimeWarping:
                    frameScanAlignmentMap = AlignFrameData(comparisonFrameNum, baseFrameScans, frameScans,
                                                           scanNumsInFrame, statsWriter, datasetName, outputDirectory);
                    break;

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
            OnStatusEvent("Appending the merged frame data");

            var frameParams = reader.GetFrameParams(referenceFrameNum);

            // Determine the minimum and maximum scan numbers in the merged frame
            if (GetScanRange(mergedFrameScans.Keys, out var scanMin, out var scanMax))
            {
                UpdateScanRange(frameParams, scanMin, scanMax);
            }

            writer.InsertFrame(mergedFrameNum, frameParams);

            var binWidth = reader.GetGlobalParams().BinWidth;
            var lastProgressTime = DateTime.UtcNow;

            var sortedKeys = mergedFrameScans.Keys.ToList();
            sortedKeys.Sort();

            var scansProcessed = 0;
            var scanCountToStore = sortedKeys.Count;

            foreach (var scanNumNew in sortedKeys)
            {
                if (scanNumNew % 10 == 0 && DateTime.UtcNow.Subtract(lastProgressTime).TotalMilliseconds >= 1000)
                {
                    lastProgressTime = DateTime.UtcNow;
                    var percentComplete = scansProcessed / (double)scanCountToStore * 100;
                    OnStatusEvent($"  storing scan {scanNumNew:#,##0} ({percentComplete:0.0}% complete)");
                }

                var intensities = mergedFrameScans[scanNumNew];
                writer.InsertScan(mergedFrameNum, frameParams, scanNumNew, intensities, binWidth);
                scansProcessed++;
            }
        }

        private ScanInfo CloneScanInfo(ScanInfo sourceScanInfo)
        {
            var clonedScanInfo = new ScanInfo(sourceScanInfo.Frame, sourceScanInfo.Scan)
            {
                BPI = sourceScanInfo.BPI,
                BPI_MZ = sourceScanInfo.BPI_MZ,
                DriftTime = sourceScanInfo.DriftTime,
                DriftTimeUnnormalized = sourceScanInfo.DriftTimeUnnormalized,
                NonZeroCount = sourceScanInfo.NonZeroCount,
                TIC = sourceScanInfo.TIC
            };

            return clonedScanInfo;
        }

        /// <summary>
        /// Compress data in dataValues by summing adjacent data points in groups of sampleLength
        /// </summary>
        /// <param name="dataValues">Data to compress</param>
        /// <param name="sampleLength">Number of adjacent data points to combine</param>
        /// <returns>Compressed data</returns>
        private double[] CompressArrayBySumming(IReadOnlyList<double> dataValues, int sampleLength)
        {
            var dataCount = dataValues.Count;

            var compressedDataLength = (int)Math.Ceiling(dataCount / (double)sampleLength);

            var compressedData = new double[compressedDataLength];

            var targetIndex = 0;
            for (var i = 0; i < dataCount; i += sampleLength)
            {
                var endIndex = Math.Min(i + sampleLength, dataCount);
                var sum = 0.0;

                for (var j = i; j < endIndex; j++)
                {
                    sum += dataValues[j];
                }

                compressedData[targetIndex] = sum;

                targetIndex++;

                if (targetIndex == compressedDataLength)
                {
                    break;
                }
            }

            return compressedData;
        }

        /// <summary>
        /// Compute updated TIC and BPI values for the scan, using only the data between mzMin and mzMax
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="sourceScanInfo"></param>
        /// <param name="mzMin"></param>
        /// <param name="mzMax"></param>
        private void ComputeFilteredTICAndBPI(
            DataReader reader,
            ScanInfo sourceScanInfo,
            double mzMin,
            double mzMax)
        {
            var key = "Frame" + sourceScanInfo.Frame + "_Scan" + sourceScanInfo.Scan;

            if (mFrameScanStats.TryGetValue(key, out var cachedScanStats))
            {
                UpdateScanStats(sourceScanInfo, cachedScanStats);
                return;
            }

            reader.GetSpectrum(sourceScanInfo.Frame, sourceScanInfo.Scan, out var mzArray, out var intensityArray);

            var scanStats = new ScanStats();
            var highestIntensity = 0;

            for (var i = 0; i < mzArray.Length; i++)
            {
                if (intensityArray[i] == 0)
                    continue;

                if (mzArray[i] < mzMin || mzMax > 0 && mzArray[i] > mzMax)
                    continue;

                scanStats.TIC += intensityArray[i];
                scanStats.NonZeroCount++;

                if (intensityArray[i] <= highestIntensity)
                    continue;

                highestIntensity = intensityArray[i];
                scanStats.BPI = highestIntensity;
                scanStats.BPI_MZ = mzArray[i];
            }

            mFrameScanStats.Add(key, scanStats);

            UpdateScanStats(sourceScanInfo, scanStats);
        }

        /// <summary>
        /// Compute the IMS-based TIC value across scans
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="framesToSum">Frame numbers to sum while populating frameScansSummed</param>
        /// <param name="scanNumsInFrame">Full list of scan numbers (unfiltered)</param>
        /// <param name="frameScansSummed">Scan info from the first frame, but with NonZeroCount and TIC values summed across the frames</param>
        /// <remarks>frameScansSummed will be a subset of scans if DriftScanFilterMin, DriftScanFilterMax, MzFilterMin, or MzFilterMax are non-zero</remarks>
        private void GetSummedFrameScans(
            DataReader reader,
            IReadOnlyCollection<int> framesToSum,
            out List<int> scanNumsInFrame,
            out List<ScanInfo> frameScansSummed)
        {
            var scanMin = Options.DriftScanFilterMin;
            var scanMax = Options.DriftScanFilterMax;
            var scanFilterEnabled = scanMin > 0 || scanMax > 0;

            var mzMin = Options.MzFilterMin;
            var mzMax = Options.MzFilterMax;
            var mzFilterEnabled = mzMin > 0 || mzMax > 0;

            scanNumsInFrame = new List<int>();
            frameScansSummed = new List<ScanInfo>();

            if (framesToSum.Count == 0)
                throw new ArgumentException("frame list cannot be empty", nameof(framesToSum));

            var firstFrameNum = framesToSum.First();
            var frameScansStart = reader.GetFrameScans(firstFrameNum);

            foreach (var sourceScanInfo in frameScansStart)
            {
                var scanNumber = sourceScanInfo.Scan;
                scanNumsInFrame.Add(scanNumber);

                if (scanFilterEnabled && (scanNumber < scanMin || scanNumber > scanMax))
                    continue;

                ScanInfo scanInfoToStore;

                if (mzFilterEnabled)
                {
                    scanInfoToStore = CloneScanInfo(sourceScanInfo);

                    ComputeFilteredTICAndBPI(reader, scanInfoToStore, mzMin, mzMax);
                }
                else
                {
                    scanInfoToStore = sourceScanInfo;
                }

                frameScansSummed.Add(scanInfoToStore);
            }

            if (framesToSum.Count == 1)
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
            // (skipping the first frame since it has already been processed)
            foreach (var frameNum in framesToSum.Skip(1))
            {
                if (frameNum < frameMin || frameNum > frameMax)
                    continue;

                var frameScans = reader.GetFrameScans(frameNum);

                foreach (var sourceScanInfo in frameScans)
                {
                    var scanNumber = sourceScanInfo.Scan;

                    if (scanFilterEnabled && (scanNumber < scanMin || scanNumber > scanMax))
                        continue;

                    ScanInfo scanInfoToStore;

                    if (mzFilterEnabled)
                    {
                        scanInfoToStore = CloneScanInfo(sourceScanInfo);

                        ComputeFilteredTICAndBPI(reader, scanInfoToStore, mzMin, mzMax);
                    }
                    else
                    {
                        scanInfoToStore = sourceScanInfo;
                    }

                    if (scanData.TryGetValue(scanNumber, out var targetScanInfo))
                    {
                        targetScanInfo.NonZeroCount += scanInfoToStore.NonZeroCount;
                        targetScanInfo.TIC += scanInfoToStore.TIC;

                        if (scanInfoToStore.BPI > targetScanInfo.BPI)
                        {
                            targetScanInfo.BPI = scanInfoToStore.BPI;
                            targetScanInfo.BPI_MZ = scanInfoToStore.BPI_MZ;
                        }
                    }
                    else
                    {
                        scanData.Add(scanNumber, scanInfoToStore);
                    }
                }
            }

            frameScansSummed.Clear();
            frameScansSummed.AddRange(from item in scanData orderby item.Key select item.Value);
        }

        /// <summary>
        /// Determine the frames to use when constructing the base frame
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>List of frame numbers</returns>
        /// <remarks>Uses Options.BaseFrameSelectionMode</remarks>
        private List<int> GetBaseFrames(DataReader reader)
        {
            return GetBaseFrames(reader, Options);
        }

        /// <summary>
        /// Determine the frames to use when constructing the base frame
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="frameAlignmentOptions">Base frame alignment options</param>
        /// <returns>List of frame numbers</returns>
        private List<int> GetBaseFrames(DataReader reader, FrameAlignmentOptions frameAlignmentOptions)
        {
            List<int> baseFrameList;

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
                    baseFrameList = new List<int> { frameMin };
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.MidpointFrame:
                    var midpointFrameNum = frameMin + (frameMax - frameMin) / 2;
                    if (midpointFrameNum > frameMax)
                    {
                        midpointFrameNum = frameMax;
                    }
                    baseFrameList = new List<int> { midpointFrameNum };
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.MaxTICFrame:
                    var ticByFrame = reader.GetTICByFrame(frameMin, frameMax, 0, 0);
                    var frameMaxTIC = (from item in ticByFrame orderby item.Value descending select item).First();
                    baseFrameList = new List<int> { frameMaxTIC.Key };
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.UserSpecifiedFrameRange:
                    if (string.IsNullOrWhiteSpace(frameAlignmentOptions.BaseFrameList))
                    {
                        var baseFrameStart = frameAlignmentOptions.BaseFrameStart;
                        var baseFrameEnd = frameAlignmentOptions.BaseFrameEnd;
                        if (baseFrameStart > 0 && (baseFrameEnd <= 0 || baseFrameEnd < baseFrameStart))
                        {
                            baseFrameEnd = baseFrameStart;
                        }

                        if (baseFrameStart <= 0 && baseFrameEnd <= 0)
                        {
                            ReportWarning(string.Format(
                                              "BaseFrameEnd must be non-zero when the BaseFrameSelectionMode is UserSpecifiedFrameRange; alternatively, use /BaseFrameList.\n" +
                                              "Valid frame numbers are {0} to {1}", frameMin, frameMax));

                            throw new ArgumentOutOfRangeException(
                                nameof(frameAlignmentOptions.BaseFrameEnd),
                                "BaseFrameEnd must be non-zero when the BaseFrameSelectionMode is UserSpecifiedFrameRange");
                        }

                        baseFrameList = new List<int>();
                        var ignoredFrameNums = new List<int>();

                        for (var frameNum = baseFrameStart; frameNum <= baseFrameEnd; frameNum++)
                        {
                            if (frameNum < frameMin)
                            {
                                ignoredFrameNums.Add(frameNum);
                                continue;
                            }

                            if (frameNum > frameMax)
                            {
                                ignoredFrameNums.Add(frameNum);
                                continue;
                            }

                            baseFrameList.Add(frameNum);
                        }

                        if (baseFrameList.Count == 0)
                        {
                            ReportWarning(string.Format(
                                              "Invalid base frame range; valid frame numbers are {0} to {1}; " +
                                              "will use the first frame as the base frame",
                                              frameMin, frameMax));

                            baseFrameList.Add(frameMin);
                        }
                        else if (ignoredFrameNums.Count == 1)
                        {
                            ReportWarning(string.Format(
                                              "Valid frame numbers are {0} to {1}; " +
                                              "ignoring frame {2}",
                                              frameMin, frameMax, ignoredFrameNums.First()));
                        }
                        else if (ignoredFrameNums.Count > 1)
                        {
                            ReportWarning(string.Format(
                                              "Valid frame numbers are {0} to {1}; " +
                                              "ignoring {2}",
                                              frameMin, frameMax, string.Join(", ", ignoredFrameNums)));
                        }
                    }
                    else
                    {
                        // Split the frame list on commas
                        var baseFrames = frameAlignmentOptions.BaseFrameList.Split(',');
                        baseFrameList = new List<int>();

                        var integerCount = 0;
                        foreach (var baseFrame in baseFrames)
                        {
                            if (int.TryParse(baseFrame, out var frameNum))
                            {
                                integerCount++;

                                if (frameNum < frameMin)
                                    continue;
                                if (frameNum > frameMax)
                                    continue;

                                baseFrameList.Add(frameNum);
                            }
                            else
                            {
                                ReportWarning("Ignoring invalid base frame number: " + baseFrame);
                            }
                        }

                        if (baseFrameList.Count == 0)
                        {
                            if (integerCount == 0)
                            {
                                throw new ArgumentException(
                                    "BaseFrameList must contain a comma-separated list of integers",
                                    nameof(frameAlignmentOptions.BaseFrameList));
                            }

                            ReportWarning(string.Format(
                                              "Invalid base frame numbers; frame numbers must be between {0} and {1}; " +
                                              "will use the first frame as the base frame",
                                              frameMin, frameMax));

                            baseFrameList.Add(frameMin);
                        }
                    }
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumFirstNFrames:
                    baseFrameList = GetFrameRange(frameMin, frameMin + baseFrameSumCount - 1, frameMin, frameMax);
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumFirstNPercent:
                    baseFrameList = GetFrameRange(frameMin, frameMin + frameCountFromPercent - 1, frameMin, frameMax);
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumMidNFrames:
                case FrameAlignmentOptions.BaseFrameSelectionModes.SumMidNPercent:

                    // First call this function to determine the midpoint frame number
                    var tempOptions = frameAlignmentOptions.ShallowCopy();
                    tempOptions.BaseFrameSelectionMode = FrameAlignmentOptions.BaseFrameSelectionModes.MidpointFrame;
                    var midpointFrame = GetBaseFrames(reader, tempOptions).First();

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

                    var midpointBasedStart = midpointFrame - leftCount;
                    var midpointBasedEnd = midpointFrame + rightCount;
                    baseFrameList = GetFrameRange(midpointBasedStart, midpointBasedEnd, frameMin, frameMax);
                    break;

                case FrameAlignmentOptions.BaseFrameSelectionModes.SumAll:
                    baseFrameList = GetFrameRange(frameMin, frameMax, frameMin, frameMax);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(frameAlignmentOptions.BaseFrameSelectionMode),
                        "Unrecognized value for BaseFrameSelectionMode");
            }

            return baseFrameList;
        }

        private static List<int> GetFrameRange(int frameStart, int frameEnd, int frameMin, int frameMax)
        {
            if (frameEnd < frameStart)
                frameEnd = frameStart;

            if (frameStart < frameMin)
                frameStart = frameMin;

            if (frameEnd > frameMax)
                frameEnd = frameMax;

            var frameList = new List<int>();
            for (var frameNum = frameStart; frameNum <= frameEnd; frameNum++)
            {
                frameList.Add(frameNum);
            }

            return frameList;
        }

        private bool CloneUimf(DataReader reader, FileSystemInfo sourceFile, FileSystemInfo outputFile)
        {
            var tablesToSkip = new List<string> {
                        "Frame_Parameters",
                        "Frame_Params",
                        "Frame_Scans" };

            var frameTypesToAlwaysCopy = new List<UIMFData.FrameType>();

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
        /// Get the appropriate offset for the given scan, interpolating if not found in offsetsBySourceScan
        /// </summary>
        /// <param name="sourceScan">Scan number</param>
        /// <param name="offsetsBySourceScan">Dictionary where keys are scan numbers and values are the offset to use for that scan</param>
        /// <param name="searcher">Binary search helper</param>
        private int GetOffsetForSourceScan(
            int sourceScan,
            IReadOnlyDictionary<int, int> offsetsBySourceScan,
            clsBinarySearchFindNearest searcher)
        {
            var offsetToUse = GetOffsetForSourceScan(sourceScan, offsetsBySourceScan, searcher, true);

            if (!offsetToUse.HasValue)
            {
                throw new Exception("GetOffsetForSourceScan returned a null value; this code should not be reached");
            }

            return offsetToUse.GetValueOrDefault();
        }

        /// <summary>
        /// Get the appropriate offset for the given scan
        /// </summary>
        /// <param name="sourceScan">Scan number</param>
        /// <param name="offsetsBySourceScan">Dictionary where keys are scan numbers and values are the offset to use for that scan</param>
        /// <param name="searcher">Binary search helper</param>
        /// <param name="interpolateIfNotFound">
        /// When true, if a match is not found, return an interpolated value.
        /// When false, if a match is not found, return null
        /// </param>
        private int? GetOffsetForSourceScan(
            int sourceScan,
            IReadOnlyDictionary<int, int> offsetsBySourceScan,
            clsBinarySearchFindNearest searcher,
            bool interpolateIfNotFound)
        {
            if (offsetsBySourceScan.TryGetValue(sourceScan, out var offset))
            {
                return offset;
            }

            if (!interpolateIfNotFound)
                return null;

            var interpolatedOffset = searcher.GetYForX(sourceScan);
            return (int)Math.Round(interpolatedOffset);
        }

        /// <summary>
        /// Determine the minimum and maximum scan numbers
        /// </summary>
        /// <param name="scanNumbers"></param>
        /// <param name="scanMin"></param>
        /// <param name="scanMax"></param>
        private bool GetScanRange(IReadOnlyCollection<int> scanNumbers, out int scanMin, out int scanMax)
        {
            scanMin = scanNumbers.Min();
            scanMax = scanNumbers.Max();

            return scanMax > 0;
        }

        /// <summary>
        /// Populate an array of doubles using the TIC values from the scans in frameScans
        /// </summary>
        /// <param name="outputDirectory"></param>
        /// <param name="frameDescription">Description of this frame</param>
        /// <param name="scanStart">Start scan number for the TIC values to return</param>
        /// <param name="scanEnd">End scan number for the TIC values to return</param>
        /// <param name="frameScans">Scan data for this frame</param>
        /// <returns>TIC values for this frame (possibly smoothed)</returns>
        /// <remarks>
        /// The data in frameScans may have missing scans, but the data in the returned array
        /// will have one point for every scan (0 for the scans that were missing)
        /// </remarks>
        private List<double> GetTICValues(
            FileSystemInfo outputDirectory,
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
                    if (Math.Abs(scan.TIC) < float.Epsilon)
                        continue;

                    warningCountEarly++;
                    if (warningCountEarly < 5)
                        ReportWarning(string.Format("Scan {0} is less than {1} in {2}; this represents a programming bug", scan.Scan, scanStart, frameDescription));
                    continue;
                }

                if (scan.Scan > scanEnd)
                {
                    if (Math.Abs(scan.TIC) < float.Epsilon)
                        continue;

                    warningCountLater++;
                    if (warningCountLater < 5)
                        ReportWarning(string.Format("Scan {0} is greater than {1} in {2}; this represents a programming bug", scan.Scan, scanEnd, frameDescription));
                    continue;
                }

                frameData[scan.Scan - scanStart] = scan.TIC;
            }

            var frameDataToReturn = SmoothAndFilterData(frameData.ToList(), outputDirectory, frameDescription, scanStart);
            return frameDataToReturn;
        }

        private FileInfo InitializeOutputFile(FileInfo sourceFile, string outputFilePath)
        {
            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                var outputFileName = Path.GetFileNameWithoutExtension(sourceFile.Name) + OUTPUT_FILENAME_SUFFIX + Path.GetExtension(sourceFile.Name);
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

            if (outputFile.Directory?.Exists == false)
            {
                outputFile.Directory.Create();
            }

            if (!outputFile.Exists)
            {
                return outputFile;
            }

            try
            {
                if (outputFile.Directory != null)
                {
                    var backupFilePath = Path.Combine(
                        outputFile.Directory.FullName,
                        Path.GetFileNameWithoutExtension(outputFile.Name) + BACKUP_FILENAME_SUFFIX + Path.GetExtension(outputFile.Name));

                    var backupFile = new FileInfo(backupFilePath);

                    if (backupFile.Exists)
                    {
                        if (Options.PreviewMode)
                        {
                            OnWarningEvent("Preview delete: " + PathUtils.CompactPathString(backupFile.FullName, 100));
                        }
                        else
                        {
                            backupFile.Delete();
                        }
                    }

                    if (Options.PreviewMode)
                    {
                        Console.WriteLine();
                        OnStatusEvent(string.Format(
                            "Preview rename, from: \n{0} to \n{1}",
                            PathUtils.CompactPathString(outputFile.FullName, 100),
                            PathUtils.CompactPathString(backupFile.FullName, 100)));
                    }
                    else
                    {
                        File.Move(outputFile.FullName, backupFile.FullName);
                        OnStatusEvent("Existing output file found; renamed to: " + backupFile.Name);
                    }
                }
                else
                {
                    if (Options.PreviewMode)
                    {
                        OnWarningEvent("Preview delete: " + PathUtils.CompactPathString(outputFile.FullName, 100));
                    }
                    else
                    {
                        outputFile.Delete();
                    }
                }

                return outputFile;
            }
            catch (Exception ex)
            {
                ReportError("Error in InitializeOutputFile for " + outputFile.FullName, ex);
                return null;
            }
        }

        private void LookupValidFrameRange(DataReader reader, out int frameMin, out int frameMax)
        {
            var masterFrameList = reader.GetMasterFrameList();

            frameMin = masterFrameList.Keys.Min();
            frameMax = masterFrameList.Keys.Max();
        }

        /// <summary>
        /// Look for peaks (aka islands) in comparisonFrameData
        /// Assure that all points in each peak get the same offset applied
        /// </summary>
        /// <param name="comparisonFrameData"></param>
        /// <param name="scanStart"></param>
        /// <param name="offsetsBySourceScanSmoothed"></param>
        /// <param name="searcher"></param>
        /// <returns>Dictionary where keys are source scan numbers and values are the offset to apply</returns>
        private Dictionary<int, int> OptimizeOffsetsUsingPeaks(
            IReadOnlyList<double> comparisonFrameData,
            int scanStart,
            IReadOnlyDictionary<int, int> offsetsBySourceScanSmoothed,
            clsBinarySearchFindNearest searcher)
        {
            // Dictionary where keys are source scan numbers and values are the offset to apply
            var optimizedOffsetsBySourceScan = new Dictionary<int, int>();

            try
            {
                // Set the noise threshold at 0.1% of the maximum intensity in comparisonFrameData
                var noiseThreshold = comparisonFrameData.Max() * 0.001;

                var insidePeak = false;
                var lastAverageOffset = 0;

                // Keys in this dictionary are source scan number, values are the offset to apply
                var sourceScanOffsetsInPeak = new Dictionary<int, int?>();

                if (comparisonFrameData[0] >= noiseThreshold)
                {
                    // The first value in comparisonFrameData is above the noise threshold
                    insidePeak = true;
                }

                for (var i = 0; i < comparisonFrameData.Count - 1; i++)
                {
                    var sourceScan = i + scanStart;

                    if (comparisonFrameData[i] < noiseThreshold)
                    {
                        if (insidePeak)
                        {
                            // End of a peak
                            StoreAverageOffsetForScansInPeak(sourceScan, sourceScanOffsetsInPeak, optimizedOffsetsBySourceScan, ref lastAverageOffset);
                            insidePeak = false;
                        }
                        else if (comparisonFrameData[i + 1] >= noiseThreshold)
                        {
                            // Start of a peak
                            insidePeak = true;
                            sourceScanOffsetsInPeak.Clear();

                            var offset = GetOffsetForSourceScan(sourceScan, offsetsBySourceScanSmoothed, searcher, false);
                            sourceScanOffsetsInPeak.Add(sourceScan, offset);
                        }
                        else
                        {
                            // Intensity is low, and we're not inside a peak
                            var offset = GetOffsetForSourceScan(sourceScan, offsetsBySourceScanSmoothed, searcher);
                            optimizedOffsetsBySourceScan.Add(sourceScan, offset);
                        }
                    }
                    else
                    {
                        if (insidePeak)
                        {
                            var offset = GetOffsetForSourceScan(sourceScan, offsetsBySourceScanSmoothed, searcher, false);
                            sourceScanOffsetsInPeak.Add(sourceScan, offset);
                        }
                        else
                        {
                            // Not inside a peak, yet this is point's intensity is above a threshold
                            // This happens when a single data point is below the noise threshold; start a new peak
                            insidePeak = true;
                            sourceScanOffsetsInPeak.Clear();

                            var offset = GetOffsetForSourceScan(sourceScan, offsetsBySourceScanSmoothed, searcher, false);
                            sourceScanOffsetsInPeak.Add(sourceScan, offset);
                        }
                    }
                }

                if (comparisonFrameData.Count > 1)
                {
                    // Process the final point
                    var sourceScan = comparisonFrameData.Count - 1 + scanStart;

                    if (insidePeak)
                    {
                        // End of a peak
                        StoreAverageOffsetForScansInPeak(sourceScan, sourceScanOffsetsInPeak, optimizedOffsetsBySourceScan, ref lastAverageOffset);
                    }
                    else
                    {
                        var offset = GetOffsetForSourceScan(sourceScan, offsetsBySourceScanSmoothed, searcher);
                        optimizedOffsetsBySourceScan.Add(sourceScan, offset);
                    }
                }
            }
            catch (Exception ex)
            {
                ReportError("Error in OptimizeOffsetsUsingPeaks", ex);
            }

            return optimizedOffsetsBySourceScan;
        }

        private void StoreAverageOffsetForScansInPeak(
            int sourceScan,
            Dictionary<int, int?> sourceScanOffsetsInPeak,
            IDictionary<int, int> optimizedOffsetsBySourceScan,
            ref int lastAverageOffset
            )
        {
            // Compute the average offset in sourceScanOffsetsInPeak
            // Skip null values
            var offsetCount = 0;
            var offsetSum = 0.0;

            foreach (var offset in sourceScanOffsetsInPeak.Values)
            {
                if (!offset.HasValue)
                    continue;

                offsetCount++;
                offsetSum += offset.Value;
            }

            int averageOffsetRounded;
            if (offsetCount == 0)
            {
                averageOffsetRounded = lastAverageOffset;
            }
            else
            {
                var averageOffset = offsetSum / offsetCount;
                averageOffsetRounded = (int)Math.Round(averageOffset);
                lastAverageOffset = averageOffsetRounded;
            }

            foreach (var item in sourceScanOffsetsInPeak)
            {
                var currentSourceScan = item.Key;

                if (optimizedOffsetsBySourceScan.TryGetValue(currentSourceScan, out var existingOffset))
                {
                    ConsoleMsgUtils.ShowDebug("Skipping storing offset of {0} for scan {1} since already defined with an offset of {2}",
                                                    averageOffsetRounded, currentSourceScan, existingOffset);
                }
                else
                {
                    optimizedOffsetsBySourceScan.Add(currentSourceScan, averageOffsetRounded);
                }
            }

            if (optimizedOffsetsBySourceScan.TryGetValue(sourceScan, out var existingOffset2))
            {
                ConsoleMsgUtils.ShowDebug("Skipping storing offset of {0} for scan {1} since already defined with an offset of {2}",
                                                averageOffsetRounded, sourceScan, existingOffset2);
            }
            else
            {
                optimizedOffsetsBySourceScan.Add(sourceScan, averageOffsetRounded);
            }
        }

        /// <summary>
        /// Process the UIMF file to align data
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputFilePath"></param>
        /// <returns>True if successful, false if an error</returns>
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

                if (outputFilePath.IndexOf(':') > 1)
                {
                    ReportError("Invalid output file path: " + outputFilePath);
                    return false;
                }

                if (Path.GetExtension(inputFilePath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    var success = ProcessTabDelimitedTextFile(inputFilePath, outputFilePath);
                    return success;
                }

                mSmoothedBaseFrameDataWritten = false;

                mFrameScanStats.Clear();

                if (!Options.PreviewMode)
                {
                    Console.WriteLine();
                    OnStatusEvent(string.Format("Opening {0}\n in directory {1}", sourceFile.Name, sourceFile.Directory));
                }

                var outputFile = InitializeOutputFile(sourceFile, outputFilePath);

                if (outputFile == null)
                    return false;

                if (outputFile.DirectoryName == null)
                    throw new DirectoryNotFoundException("Cannot determine the parent directory of " + outputFile.FullName);

                if (ShowDebugMessages)
                {
                    var outputDirectoryPath = outputFile.DirectoryName;

                    var debugDataFile = new FileInfo(Path.Combine(outputDirectoryPath, DEBUG_DATA_FILE));
                    if (debugDataFile.Exists)
                    {
                        if (Options.PreviewMode)
                        {
                            OnStatusEvent("Preview delete: " + PathUtils.CompactPathString(debugDataFile.FullName, 100));
                        }
                        else
                        {
                            debugDataFile.Delete();
                        }
                    }

                    var dtwDebugDataFile = new FileInfo(Path.Combine(outputDirectoryPath, DTW_DEBUG_DATA_FILE));
                    if (dtwDebugDataFile.Exists)
                    {
                        if (Options.PreviewMode)
                        {
                            OnStatusEvent("Preview delete: " + PathUtils.CompactPathString(dtwDebugDataFile.FullName, 100));
                        }
                        else
                        {
                            dtwDebugDataFile.Delete();
                        }
                    }
                }

                if (Options.PreviewMode)
                {
                    Console.WriteLine();
                    OnStatusEvent(string.Format(
                        "Preview processing \n{0} to create \n{1}",
                        PathUtils.CompactPathString(sourceFile.FullName, 125),
                        PathUtils.CompactPathString(outputFile.FullName, 125)));

                    return true;
                }

                var statsFilePath = Path.Combine(outputFile.DirectoryName, Path.GetFileNameWithoutExtension(outputFile.Name) + "_stats.txt");

                using var reader = new DataReader(sourceFile.FullName);
                {
                    reader.ErrorEvent += UIMFReader_ErrorEvent;

                    OnStatusEvent("Cloning the .UIMF file");
                    var success = CloneUimf(reader, sourceFile, outputFile);
                    if (!success)
                    {
                        // The error message has already been shown
                        return false;
                    }

                    GetFrameRangeToProcess(reader, out var frameStart, out var frameEnd);

                    var baseFrameList = GetBaseFrames(reader);

                    OnStatusEvent("Retrieving base frame scan data");

                    GetSummedFrameScans(reader, baseFrameList, out _, out var baseFrameScans);

                    if (Options.DebugMode)
                    {
                        var baseFrameDebugFile = new FileInfo(Path.Combine(outputFile.DirectoryName, Path.GetFileNameWithoutExtension(outputFile.Name) + "_baseframe.txt"));

                        WriteFrameScansDebugFile(baseFrameScans, baseFrameDebugFile);
                    }

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
                            {
                                ReportError(string.Format(
                                    "Unable to define the base frame scans. Perhaps the base frame range is invalid (currently {0} to {1})",
                                    Options.BaseFrameStart, Options.BaseFrameEnd));
                            }
                            else
                            {
                                ReportError("Unable to define the base frame scans; check the parameters");
                            }
                        }

                        return false;
                    }
                    var mergedFrameScans = new Dictionary<int, int[]>();

                    using (var statsWriter = new StatsWriter(statsFilePath, Options, CommandLine, baseFrameList))
                    using (var writer = new DataWriter(outputFile.FullName))
                    {
                        RegisterEvents(statsWriter);

                        Console.WriteLine();
                        if (baseFrameList.Count == 1)
                            OnStatusEvent("Actual base frame: " + baseFrameList.First());
                        else
                            OnStatusEvent("Actual base frames: " + string.Join(",", baseFrameList));

                        statsWriter.WriteHeader("Frame");

                        if (writer.HasLegacyParameterTables)
                            writer.ValidateLegacyHPFColumnsExist();

                        var nextFrameNumOutfile = 1;
                        bool insertEachFrame;

                        if (Options.MergeFrames)
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
                            ProcessFrame(
                                reader, writer, outputFile,
                                frameNum, baseFrameScans, mergedFrameScans,
                                statsWriter, insertEachFrame, nextFrameNumOutfile);

                            if (insertEachFrame)
                                nextFrameNumOutfile++;
                        }

                        if ((Options.AppendMergedFrame || Options.MergeFrames) && mergedFrameScans.Count > 0)
                        {
                            const int referenceFrameNum = 1;

                            AppendMergedFrame(reader, writer, referenceFrameNum, nextFrameNumOutfile, mergedFrameScans);
                        }

                        // Make sure values in Global_Params are up-to-date
                        // Specifically, the frame count (tracked by NumFrames) and the
                        // maximum number of scans in any frame (tracked by PrescanTOFPulses)
                        writer.UpdateGlobalStats();
                    }

                    Console.WriteLine();
                    OnStatusEvent("Processing Complete; created file " + PathUtils.CompactPathString(outputFile.FullName, 100));
                    OnStatusEvent("See also the stats file:          " + PathUtils.CompactPathString(statsFilePath, 100));
                }

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Error in ProcessFile", ex);
                return false;
            }
        }

        private bool ProcessTabDelimitedTextFile(string inputFilePath, string outputFilePath)
        {
            try
            {
                var inputFile = new FileInfo(inputFilePath);

                DirectoryInfo outputDirectory;
                if (string.IsNullOrWhiteSpace(outputFilePath))
                {
                    outputDirectory = inputFile.Directory;
                }
                else
                {
                    var outputFile = new FileInfo(outputFilePath);
                    outputDirectory = outputFile.Directory;
                }

                OnStatusEvent("Loading " + inputFile.FullName);

                if (outputDirectory == null)
                {
                    ConsoleMsgUtils.ShowWarning("Unable to determine the output directory");
                    return false;
                }
                OnStatusEvent("Output directory: " + outputDirectory.FullName);

                if (!outputDirectory.Exists && !Options.PreviewMode)
                {
                    outputDirectory.Create();
                }

                // Keys in this dictionary are 1, 2, 3, etc. up to columnValues.Count
                // Values are the data for the given column
                var columnDataMap = new Dictionary<int, List<double>>();

                using (var reader = new StreamReader(new FileStream(inputFile.FullName, FileMode.Open, FileAccess.ReadWrite)))
                {
                    var lineCount = 0;
                    var dataValues = new List<double>();

                    while (!reader.EndOfStream)
                    {
                        lineCount++;
                        var dataLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        var columnValues = dataLine.Split('\t').ToList();
                        if (columnValues.Count < 2)
                        {
                            OnStatusEvent(string.Format("Skipping line {0} since it does not have 2 or more columns", lineCount));
                            continue;
                        }

                        dataValues.Clear();
                        var columnNumber = 0;
                        foreach (var columnItem in columnValues)
                        {
                            columnNumber++;
                            if (!double.TryParse(columnItem, out var columnValue))
                            {
                                OnStatusEvent(string.Format("Value in column {0} of line {1} is not numeric; skipping this line", columnNumber, lineCount));
                                dataValues.Clear();
                                break;
                            }

                            dataValues.Add(columnValue);
                        }

                        if (dataValues.Count < columnValues.Count)
                        {
                            continue;
                        }

                        for (var i = 0; i < dataValues.Count; i++)
                        {
                            if (columnDataMap.TryGetValue(i + 1, out var dataPointList))
                            {
                                dataPointList.Add(dataValues[i]);
                            }
                            else
                            {
                                columnDataMap.Add(i + 1, new List<double> { dataValues[i] });
                            }
                        }
                    }
                }

                // Treat Options.BaseFrameStart as a 1-based column number
                // However, if it is 0 (or negative), force it to 1
                var alignmentBaseColumnNumber = Math.Max(1, Options.BaseFrameStart);

                if (alignmentBaseColumnNumber > columnDataMap.Count)
                {
                    OnWarningEvent(string.Format(
                        "Base column for alignment is out-of-range; should be between 1 and {0}, not {1}",
                        columnDataMap.Count, alignmentBaseColumnNumber));

                    return false;
                }

                var scanNumsInColumn = new List<int>();
                for (var i = 0; i < columnDataMap[alignmentBaseColumnNumber].Count; i++)
                {
                    scanNumsInColumn.Add(i + 1);
                }

                var scanStart = scanNumsInColumn.First();
                var scanEnd = scanNumsInColumn.Last();

                // Keys in this dictionary are 1, 2, 3, etc. up to columnValues.Count
                // Values are the processed data for the given column
                var columnDataProcessedMap = new Dictionary<int, List<double>>();
                var columnsWithAllZeroes = new SortedSet<int>();

                foreach (var columnDataKvp in columnDataMap)
                {
                    // The first column will have .Key = 1
                    var columnNumber = columnDataKvp.Key;
                    var columnData = columnDataKvp.Value;

                    var description = (columnNumber == Options.BaseFrameStart) ? BASE_FRAME_DESCRIPTION : $"Comparison Column {columnNumber}";
                    var processedData = SmoothAndFilterData(columnData, outputDirectory, description, scanStart);

                    var positiveDataPoints = processedData.Count(dataPoint => dataPoint > 0);
                    if (positiveDataPoints == 0)
                    {
                        OnWarningEvent(string.Format("Column {0} has no positive data points; it will not be aligned to the base column",
                            columnNumber));
                        columnsWithAllZeroes.Add(columnNumber);
                    }

                    columnDataProcessedMap.Add(columnNumber, processedData);
                }

                if (!outputDirectory.Exists && !Options.PreviewMode)
                {
                    OnStatusEvent("Creating output directory at " + outputDirectory.FullName);
                    outputDirectory.Create();
                }

                if (string.IsNullOrWhiteSpace(Options.DatasetName))
                {
                    Options.DatasetName = Path.GetFileNameWithoutExtension(inputFile.Name);
                }

                var datasetName = Options.DatasetName.Replace(" ", "_");

                var statsFileName = string.Format("{0}_{1}.txt", Options.AlignmentMethod.ToString(), datasetName);
                var statsFile = new FileInfo(Path.Combine(outputDirectory.FullName, statsFileName));

                if (Options.PreviewMode)
                {
                    OnStatusEvent("Would create the stats file at " + PathUtils.CompactPathString(statsFile.FullName, 80));
                }
                else
                {
                    OnStatusEvent("Creating stats file at " + PathUtils.CompactPathString(statsFile.FullName, 80));
                }

                if (statsFile.Exists && !Options.PreviewMode)
                {
                    statsFile.Delete();
                }

                var crosstabFileName = string.Format("{0}_Crosstab.txt", datasetName);
                var offsetCrosstabFile = new FileInfo(Path.Combine(outputDirectory.FullName, crosstabFileName));

                if (offsetCrosstabFile.Exists && !Options.PreviewMode)
                {
                    offsetCrosstabFile.Delete();
                }

                OnStatusEvent(string.Format("Loaded {0} columns of data from {1}", columnDataProcessedMap.Count, inputFile.Name));
                if (Options.PreviewMode)
                {
                    return true;
                }

                var success = true;

                using (var statsWriter = new StatsWriter(statsFile.FullName, Options, string.Empty))
                {
                    RegisterEvents(statsWriter);

                    statsWriter.WriteHeader("Column");

                    var baseColumnDataProcessed = columnDataProcessedMap[alignmentBaseColumnNumber];

                    for (var comparisonColumnNum = 1; comparisonColumnNum <= columnDataProcessedMap.Count; comparisonColumnNum++)
                    {
                        var comparisonColumnDataProcessed = columnDataProcessedMap[comparisonColumnNum];

                        var pngFileName = string.Format("{0}_Column{1}.png", datasetName, comparisonColumnNum);
                        var pngFileInfo = new FileInfo(Path.Combine(outputDirectory.FullName, pngFileName));

                        if (pngFileInfo.Exists && !Options.PreviewMode)
                        {
                            pngFileInfo.Delete();
                        }

                        var dataSourceDescription = "Column " + comparisonColumnNum;

                        // Keys are the old scan number and values are the new scan number
                        Dictionary<int, int> columnScanAlignmentMap;
                        bool alignmentSkipped;
                        if (columnsWithAllZeroes.Contains(comparisonColumnNum))
                        {
                            // Skip alignment of this column
                            columnScanAlignmentMap = new Dictionary<int, int>();
                            foreach (var item in scanNumsInColumn)
                            {
                                columnScanAlignmentMap.Add(item, item);
                            }

                            alignmentSkipped = true;
                        }
                        else
                        {
                            if (Options.AlignmentMethod == FrameAlignmentOptions.AlignmentMethods.LinearRegression)
                            {
                                columnScanAlignmentMap = AlignFrameDataLinearRegression(
                                    comparisonColumnNum, comparisonColumnDataProcessed,
                                    baseColumnDataProcessed, scanNumsInColumn, statsWriter);
                            }
                            else
                            {
                                columnScanAlignmentMap = AlignFrameDataDTW(
                                    "Column",
                                    comparisonColumnNum, comparisonColumnDataProcessed,
                                    baseColumnDataProcessed, scanNumsInColumn,
                                    statsWriter, scanStart, scanEnd,
                                    outputDirectory,
                                    pngFileInfo);
                            }
                            alignmentSkipped = false;
                        }

                        SaveAlignedData(dataSourceDescription, baseColumnDataProcessed, comparisonColumnDataProcessed,
                            scanStart, columnScanAlignmentMap, outputDirectory, offsetCrosstabFile);

                        if (!alignmentSkipped &&
                            Options.SaveDTWPlots &&
                            Options.AlignmentMethod == FrameAlignmentOptions.AlignmentMethods.DynamicTimeWarping)
                        {
                            pngFileInfo.Refresh();
                            if (!pngFileInfo.Exists)
                            {
                                ConsoleMsgUtils.ShowWarning("The plot file was not created: " + pngFileInfo.FullName);
                                success = false;
                            }
                            else
                            {
                                OnStatusEvent("Plot file created at " + pngFileInfo.FullName);
                            }
                        }

                        Console.WriteLine();
                    }

                    Console.WriteLine();

                    statsFile.Refresh();
                    if (!statsFile.Exists)
                    {
                        ConsoleMsgUtils.ShowWarning("The stats file was not created: " + statsFile.FullName);
                        success = false;
                    }
                    else
                    {
                        OnStatusEvent("Stats file created at " + statsFile.FullName);
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                ReportError("Error in ProcessTabDelimitedTextFile", ex);
                return false;
            }
        }

        /// <summary>
        /// Read the frame data from the source file, align it to base frame data, and write to the output file
        /// </summary>
        /// <param name="reader">UIMF Reader</param>
        /// <param name="writer">UIMF Writer</param>
        /// <param name="outputFile">Output file (used for debug purposes)</param>
        /// <param name="comparisonFrameNum">Current frame Number</param>
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
            FileInfo outputFile,
            int comparisonFrameNum,
            IReadOnlyList<ScanInfo> baseFrameScans,
            IDictionary<int, int[]> mergedFrameScans,
            StatsWriter statsWriter,
            bool insertFrame,
            int nextFrameNumOutfile
            )
        {
            Console.WriteLine();
            OnStatusEvent($"Process frame {comparisonFrameNum}");

            try
            {
                var currentFrameList = new List<int> { comparisonFrameNum };

                // Get the scans for frame comparisonFrameNum
                GetSummedFrameScans(reader, currentFrameList, out var scanNumsInFrame, out var frameScans);

                var datasetName = Path.GetFileNameWithoutExtension(outputFile.Name);

                if (Options.DebugMode && outputFile.Directory != null)
                {
                    var debugFileName = string.Format("{0}_frame{1:00}.txt", datasetName, comparisonFrameNum);
                    var frameDebugFile = new FileInfo(Path.Combine(outputFile.Directory.FullName, debugFileName));

                    WriteFrameScansDebugFile(frameScans, frameDebugFile);
                }

                // Dictionary where keys are the old scan number and values are the new scan number
                var frameScanAlignmentMap = AlignFrameTICToBase(comparisonFrameNum, baseFrameScans, frameScans,
                                                                scanNumsInFrame, statsWriter, datasetName, outputFile.Directory);
                if (frameScanAlignmentMap == null)
                    return;

                var frameParams = reader.GetFrameParams(comparisonFrameNum);

                // Assure that the frame type is not 0
                var frameType = frameParams.GetValueInt32(FrameParamKeyType.FrameType, 0);
                if (frameType == 0)
                {
                    frameParams.AddUpdateValue(FrameParamKeyType.FrameType, (int)UIMFData.FrameType.MS1);
                }

                if (insertFrame)
                {
                    // Determine the minimum and maximum values of the new scan numbers in frameScanAlignmentMap
                    if (GetScanRange(frameScanAlignmentMap.Values.ToList(), out var scanMin, out var scanMax))
                    {
                        UpdateScanRange(frameParams, scanMin, scanMax);
                    }

                    writer.InsertFrame(nextFrameNumOutfile, frameParams);
                }

                var scanFilterEnabled = Options.DriftScanFilterMin > 0 || Options.DriftScanFilterMax > 0;

                var binWidth = reader.GetGlobalParams().BinWidth;
                var lastProgressTime = DateTime.UtcNow;

                // Dynamic Time Warping will sometimes map two source scans to the same target scan
                // Examine frameScanAlignmentMap to populate a dictionary where keys are the target scan and values are the source scan(s)
                // Note that frameScanAlignmentMap might not have every scan in scanNumsInFrame; this is expected
                // missing scans will not be included in the new .uimf file (and will not be used to update mergedFrameScans)

                var targetScanSourceScans = new Dictionary<int, List<int>>();
                foreach (var item in frameScanAlignmentMap)
                {
                    var scanNumOld = item.Key;
                    var scanNumNew = item.Value;

                    if (targetScanSourceScans.TryGetValue(scanNumNew, out var sourceScans))
                    {
                        sourceScans.Add(scanNumOld);
                    }
                    else
                    {
                        targetScanSourceScans.Add(scanNumNew, new List<int> { scanNumOld });
                    }
                }

                // Read each scan in the frame and transform to the new scan number
                // Write the scan to the output file if insertFrame is true
                // Append the scan to mergedFrameScans if Options.AppendMergedFrame is true or Options.MergeFrames is true

                // Write the scans in ascending order
                var targetScanSourceScanKeys = targetScanSourceScans.Keys.ToList();
                targetScanSourceScanKeys.Sort();

                foreach (var scanNumNew in targetScanSourceScanKeys)
                {
                    var oldScanNums = targetScanSourceScans[scanNumNew];
                    var scanNumOldStart = oldScanNums.First();

                    if (scanNumNew % 10 == 0 && DateTime.UtcNow.Subtract(lastProgressTime).TotalMilliseconds >= 1000)
                    {
                        lastProgressTime = DateTime.UtcNow;
                        OnStatusEvent($"  storing scan {scanNumOldStart:#,##0}");
                    }

                    int[] targetScanIntensities = null;

                    foreach (var scanNumOld in oldScanNums)
                    {
                        int[] intensities;
                        try
                        {
                            intensities = reader.GetSpectrumAsBins(comparisonFrameNum, frameParams.FrameType, scanNumOld);
                        }
                        catch (Exception ex)
                        {
                            ReportError($"Error retrieving data for frame {comparisonFrameNum}, scan {scanNumOld}", ex);
                            continue;
                        }

                        if (targetScanIntensities == null)
                        {
                            targetScanIntensities = intensities;
                            continue;
                        }

                        for (var i = 0; i < intensities.Length; i++)
                        {
                            if (i >= targetScanIntensities.Length)
                                break;

                            targetScanIntensities[i] += intensities[i];
                        }
                    }

                    if (insertFrame)
                    {
                        writer.InsertScan(nextFrameNumOutfile, frameParams, scanNumNew, targetScanIntensities, binWidth);
                    }

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
                            if (i >= targetScanIntensities.Length)
                                break;

                            if (summedIntensities[i] + (long)targetScanIntensities[i] > int.MaxValue)
                                summedIntensities[i] = int.MaxValue;
                            else
                                summedIntensities[i] += targetScanIntensities[i];
                        }
                    }
                    else
                    {
                        mergedFrameScans.Add(scanNumNew, targetScanIntensities);
                    }
                }
            }
            catch (Exception ex)
            {
                ReportError("Error in ProcessFrame", ex);
            }
        }

        private void ReportError(string message)
        {
            OnErrorEvent(message, null);
            ErrorMessages.Add(message);
        }

        private void ReportError(string message, Exception ex)
        {
            OnErrorEvent(message, ex);
            ErrorMessages.Add(message);
        }

        private void ReportWarning(string message)
        {
            OnWarningEvent(message);
            WarningMessages.Add(message);
        }

        /// <summary>
        /// Write data to DebugDataDTW.txt
        /// </summary>
        /// <param name="outputDirectory"></param>
        /// <param name="frameDescription">Frame description</param>
        /// <param name="cost">Dynamic time warping cost</param>
        /// <param name="alignmentPath">Map from source scan to target scan; source scans might map to multiple target scans</param>
        /// <param name="consolidatedScanInfoFromDTW">Dictionary mapping source scan to target scan (unique target)</param>
        /// <param name="offsetsBySourceScanSmoothed"></param>
        /// <param name="frameScanAlignmentMap"></param>
        private void SaveDynamicTimeWarpingDataForDebug(
            FileSystemInfo outputDirectory,
            string frameDescription,
            double cost,
            IEnumerable<Tuple<int, int>> alignmentPath,
            IReadOnlyDictionary<int, int> consolidatedScanInfoFromDTW,
            IReadOnlyDictionary<int, int> offsetsBySourceScanSmoothed,
            IReadOnlyDictionary<int, int> frameScanAlignmentMap)
        {
            try
            {
                var debugDataFile = new FileInfo(Path.Combine(outputDirectory.FullName, DTW_DEBUG_DATA_FILE));

                using var writer = new StreamWriter(new FileStream(debugDataFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

                writer.WriteLine(frameDescription);
                writer.WriteLine();
                writer.WriteLine("Cost: {0:#,##0}", cost);
                writer.WriteLine();
                writer.WriteLine("Alignment path found using the Compressed Data:");
                writer.WriteLine("{0}\t{1}", "ComparisonData", "BaseData");

                foreach (var item in alignmentPath)
                {
                    writer.WriteLine("{0:0}\t{1:0}", item.Item1, item.Item2);
                }

                writer.WriteLine();
                writer.WriteLine("Mapping from Comparison Data to Base Data, for all scans:");
                writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", "SourceScan_ComparisonData", "TargetScan_BaseData", "Offset", "OffsetSmoothed", "TargetScan_via_OffsetSmoothed");

                var warningCount = 0;

                foreach (var item in consolidatedScanInfoFromDTW)
                {
                    var sourceScan = item.Key;
                    var targetScan = item.Value;

                    var offset = targetScan - sourceScan;
                    int targetScanViaOffsetSmoothed;

                    if (offsetsBySourceScanSmoothed.TryGetValue(sourceScan, out var offsetSmoothed))
                    {
                        targetScanViaOffsetSmoothed = sourceScan + offsetSmoothed;
                    }
                    else
                    {
                        OnWarningEvent(string.Format("Warning, did not find scan {0} in offsetsBySourceScanSmoothed", sourceScan));
                        offsetSmoothed = -1;
                        targetScanViaOffsetSmoothed = -1;
                    }

                    writer.WriteLine("{0:0}\t{1}\t{2}\t{3}\t{4}", sourceScan, targetScan, offset, offsetSmoothed, targetScanViaOffsetSmoothed);

                    // Note that frameScanAlignmentMap only has data for scans that have a detected ion
                    if (frameScanAlignmentMap.TryGetValue(sourceScan, out var targetScanToVerify))
                    {
                        if (targetScanToVerify != targetScanViaOffsetSmoothed)
                        {
                            warningCount++;

                            if (warningCount <= 10 || warningCount % 100 == 0)
                            {
                                OnWarningEvent(string.Format(
                                    "Warning, mismatch between expected target scan and frameScanAlignmentMap; {0} vs. {1}",
                                    targetScanToVerify, targetScanViaOffsetSmoothed));
                            }
                        }
                    }
                }
                writer.WriteLine();
            }
            catch (Exception ex)
            {
                ReportError("Error in SaveDynamicTimeWarpingDataForDebug", ex);
            }
        }

        /// <summary>
        /// Save the aligned data to a tab-delimited text file based on the frame or column name
        /// Optionally also add a new column to the scan offset crosstab file
        /// </summary>
        /// <param name="dataSourceDescription">Frame number or column number</param>
        /// <param name="baseData">Base frame or base column data</param>
        /// <param name="frameOrColumnData">Data being aligned to the base data</param>
        /// <param name="scanStart">Start scan</param>
        /// <param name="scanAlignmentMap">Scan alignment map</param>
        /// <param name="outputDirectory">Output directory</param>
        /// <param name="offsetCrosstabFile">
        /// When not null, append the data for this column to this scan offset crosstab file
        /// This is only done when the input file is a tab-delimited text file
        /// </param>
        private void SaveAlignedData(
            string dataSourceDescription,
            IReadOnlyList<double> baseData,
            IReadOnlyList<double> frameOrColumnData,
            int scanStart,
            IReadOnlyDictionary<int, int> scanAlignmentMap,
            FileSystemInfo outputDirectory,
            FileSystemInfo offsetCrosstabFile)
        {
            try
            {
                var debugFileName = string.Format("{0}.txt", dataSourceDescription.Replace(" ", string.Empty));
                var debugDataFile = new FileInfo(Path.Combine(outputDirectory.FullName, debugFileName));

                var offsetsByScan = new Dictionary<int, double>();

                using (var writer = new StreamWriter(new FileStream(debugDataFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    // Construct a mapping of the existing indices in frameOrColumnData to where the TIC value for that index would be shifted to using scanAlignmentMap
                    var targetIndex = new int[frameOrColumnData.Count];

                    // This dictionary is a histogram of scan shifts
                    // Keys are the number of scans a source scan is shifted by
                    // Values are the number of source scans shifted by this amount
                    var scanShiftStats = new Dictionary<int, int>();

                    for (var i = 0; i < frameOrColumnData.Count; i++)
                    {
                        var scanNumOld = scanStart + i;

                        if (!scanAlignmentMap.TryGetValue(scanNumOld, out var scanNumNew))
                        {
                            targetIndex[i] = -1;
                            continue;
                        }

                        var scanShift = scanNumNew - scanNumOld;
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
                            OnStatusEvent($"  Data in {dataSourceDescription} will not be shifted");
                            break;
                        case 1:
                            OnStatusEvent($"  Data in {dataSourceDescription} will be shifted by 1 scan");
                            break;
                        default:
                            OnStatusEvent($"  Data in {dataSourceDescription} will be shifted by {scanShiftApplied} scans");
                            break;
                    }

                    writer.WriteLine("{0}\t{1}\t{2}\t{3}", "Scan", "TIC_Base", "TIC_Compare_Offset", "TIC_Compare_Original");

                    // Use the mapping in targetIndex to populate frameOrColumnDataOffset
                    var frameOrColumnDataOffset = new double[frameOrColumnData.Count];
                    for (var i = 0; i < frameOrColumnData.Count; i++)
                    {
                        if (targetIndex[i] < 0 || targetIndex[i] >= frameOrColumnData.Count)
                            continue;

                        frameOrColumnDataOffset[targetIndex[i]] = frameOrColumnData[i];
                    }

                    for (var i = 0; i < frameOrColumnData.Count; i++)
                    {
                        var actualScan = scanStart + i;
                        writer.WriteLine("{0}\t{1}\t{2}\t{3}", actualScan, baseData[i], frameOrColumnDataOffset[i], frameOrColumnData[i]);
                        offsetsByScan.Add(actualScan, frameOrColumnDataOffset[i]);
                    }
                    writer.WriteLine();
                }

                if (offsetCrosstabFile != null)
                {
                    UpdateOffsetCrosstabFile(dataSourceDescription, offsetsByScan, offsetCrosstabFile);
                }
            }
            catch (Exception ex)
            {
                ReportError("Error in SaveAlignedData", ex);
            }
        }

        private void SaveSmoothedDataForDebug(
            FileSystemInfo outputDirectory,
            string frameOrColumnDescription,
            int scanStart,
            IReadOnlyList<double> frameData,
            IReadOnlyList<double> frameDataSmoothed)
        {
            try
            {
                var debugDataFile = new FileInfo(Path.Combine(outputDirectory.FullName, DEBUG_DATA_FILE));

                using var writer = new StreamWriter(new FileStream(debugDataFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

                writer.WriteLine("Data smoothing comparison for " + frameOrColumnDescription);
                writer.WriteLine("{0}\t{1}\t{2}", "Scan", "TIC_Original", "TIC_Smoothed");

                for (var i = 0; i < frameData.Count; i++)
                {
                    writer.WriteLine("{0}\t{1}\t{2}", scanStart + i, frameData[i], frameDataSmoothed[i]);
                }

                writer.WriteLine();
            }
            catch (Exception ex)
            {
                ReportError("Error in SaveSmoothedDataForDebug", ex);
            }
        }

        /// <summary>
        /// Smooth and/or filter the data in frameData, based on settings in Options
        /// </summary>
        /// <param name="frameData"></param>
        /// <param name="outputDirectory"></param>
        /// <param name="frameOrColumnDescription"></param>
        /// <param name="scanStart"></param>
        public List<double> SmoothAndFilterData(
            List<double> frameData,
            FileSystemInfo outputDirectory,
            string frameOrColumnDescription,
            int scanStart)
        {
            if (Options.ScanSmoothCount <= 1)
            {
                // Not using smoothing, but we may need to zero-out values below a threshold
                ZeroValuesBelowThreshold(frameData);

                return frameData;
            }

            // Apply a moving average smooth
            var frameDataSmoothed = MathNet.Numerics.Statistics.Statistics.MovingAverage(frameData, Options.ScanSmoothCount).ToList();

            // The smoothing algorithm results in some negative values very close to 0 (like -4.07E-12)
            // Change these to 0
            for (var i = 0; i < frameDataSmoothed.Count; i++)
            {
                if (frameDataSmoothed[i] < 0)
                    frameDataSmoothed[i] = 0;
            }

            ZeroValuesBelowThreshold(frameDataSmoothed);

            if (!ShowDebugMessages)
            {
                return frameDataSmoothed;
            }

            var writeData = true;
            if (frameOrColumnDescription == BASE_FRAME_DESCRIPTION)
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
                SaveSmoothedDataForDebug(outputDirectory, frameOrColumnDescription, scanStart, frameData, frameDataSmoothed);
            }

            return frameDataSmoothed;
        }

        private Dictionary<int, int> SmoothViaMovingAverage(Dictionary<int, int> dataToSmooth, int windowSize)
        {
            var dataKeys = new List<int>();
            var dataValues = new List<double>();
            foreach (var item in dataToSmooth)
            {
                dataKeys.Add(item.Key);
                dataValues.Add(item.Value);
            }

            var smoothedValues = MathNet.Numerics.Statistics.Statistics.MovingAverage(dataValues, windowSize).ToList();

            var smoothedData = new Dictionary<int, int>();
            for (var i = 0; i < dataKeys.Count; i++)
            {
                smoothedData.Add(dataKeys[i], (int)smoothedValues[i]);
            }

            return smoothedData;
        }

        private void UpdateOffsetCrosstabFile(string dataSourceDescription, Dictionary<int, double> offsetsByScan, FileSystemInfo offsetCrosstabFile)
        {
            try
            {
                offsetCrosstabFile.Refresh();
                if (!offsetCrosstabFile.Exists)
                {
                    using var writer = new StreamWriter(new FileStream(offsetCrosstabFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

                    // Header line
                    writer.WriteLine("{0}\t{1}", "Scan", dataSourceDescription);

                    foreach (var item in (from item in offsetsByScan.Keys orderby item select item))
                    {
                        writer.WriteLine("{0}\t{1}", item, offsetsByScan[item]);
                    }

                    return;
                }

                var existingOffsetsByScan = new Dictionary<int, string>();
                var headerLine = string.Empty;
                var existingDatasetCount = 0;

                using (var reader = new StreamReader(new FileStream(offsetCrosstabFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    var lineNumber = 0;
                    while (!reader.EndOfStream)
                    {
                        lineNumber++;

                        var dataLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        if (string.IsNullOrWhiteSpace(headerLine))
                        {
                            headerLine = dataLine;
                            var headers = headerLine.Split('\t');
                            existingDatasetCount = headers.Length - 1;
                            continue;
                        }

                        var lineParts = dataLine.Split(new[] { '\t' }, 2);

                        if (lineParts.Length < 2)
                        {
                            ReportWarning(string.Format(
                                "Line {0} should be tab-delimited with at least two columns; skipping: {1}",
                                lineNumber, dataLine));
                            continue;
                        }

                        if (!int.TryParse(lineParts[0], out var scanNumber))
                        {
                            ReportWarning(string.Format(
                                "Integer not found in the first column of line {0} (which should be tab-delimited): {1}",
                                lineNumber, dataLine));
                            continue;
                        }

                        existingOffsetsByScan.Add(scanNumber, dataLine);
                    }
                }

                // All of the keys in offsetsByScan should already be in existingOffsetsByScan
                // But, for good measure, check for new ones
                var missingKeys = existingOffsetsByScan.Keys.Except(offsetsByScan.Keys).ToList();

                if (missingKeys.Count > 0)
                {
                    var placeholderColumnSource = new List<string>();
                    for (var i = 0; i < existingDatasetCount; i++)
                    {
                        placeholderColumnSource.Add(string.Empty);
                    }

                    var placeholderColumnData = string.Join("\t", placeholderColumnSource);

                    foreach (var missingKey in missingKeys)
                    {
                        var dataLine = string.Format("{0}\t{1}", missingKey, placeholderColumnData);
                        existingOffsetsByScan.Add(missingKey, dataLine);
                    }
                }

                using (var writer = new StreamWriter(new FileStream(offsetCrosstabFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
                {
                    // Write the new header line
                    writer.WriteLine("{0}\t{1}", headerLine, dataSourceDescription);

                    foreach (var item in (from item in existingOffsetsByScan orderby item.Key select item))
                    {
                        if (offsetsByScan.TryGetValue(item.Key, out var dataValue))
                            writer.WriteLine("{0}\t{1}", item.Value, dataValue);
                        else
                            writer.WriteLine("{0}\t{1}", item.Value, string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                ReportError("Error in UpdateOffsetCrosstabFile", ex);
            }
        }

        private void UpdateScanRange(FrameParams frameParams, int scanMin, int scanMax)
        {
            frameParams.AddUpdateValue(FrameParamKeyType.Scans, scanMax);
            frameParams.AddUpdateValue(FrameParamKeyType.ScanNumFirst, scanMin);
            frameParams.AddUpdateValue(FrameParamKeyType.ScanNumLast, scanMax);
        }

        private void UpdateScanStats(ScanInfo sourceScanInfo, ScanStats scanStats)
        {
            sourceScanInfo.BPI = scanStats.BPI;
            sourceScanInfo.BPI_MZ = scanStats.BPI_MZ;
            sourceScanInfo.TIC = scanStats.TIC;
            sourceScanInfo.NonZeroCount = scanStats.NonZeroCount;
        }

        private void VisualizeDTWAlignment(
            string dataSourceDescription,
            int comparisonFrameNum,
            IReadOnlyList<double> comparisonFrameData,
            int scanStart,
            Dictionary<int, int> offsetsBySourceScan,
            Dictionary<int, int> offsetsBySourceScanSmoothed,
            Dictionary<int, int> optimizedOffsetsBySourceScan,
            IDtw dtwAligner,
            IReadOnlyCollection<double> baseDataToUse,
            int sakoeChibaMaxShift,
            FileSystemInfo pngFileInfo)
        {
            var offsetPlot = new PlotModel("Offset vs. drift time scan");

            var offsetSeries = new LineSeries("Scan shift")
            {
                XAxisKey = "DriftTimeScan",
                YAxisKey = "Offset"
            };

            foreach (var item in offsetsBySourceScan)
            {
                offsetSeries.Points.Add(new DataPoint(item.Key, item.Value));
            }

            var smoothedOffsetSeries = new LineSeries("Smoothed shift")
            {
                XAxisKey = "DriftTimeScan",
                YAxisKey = "Offset"
            };

            foreach (var item in offsetsBySourceScanSmoothed)
            {
                smoothedOffsetSeries.Points.Add(new DataPoint(item.Key, item.Value));
            }

            var optimizedOffsetSeries = new LineSeries("Optimized shift")
            {
                XAxisKey = "DriftTimeScan",
                YAxisKey = "Offset"
            };

            foreach (var item in optimizedOffsetsBySourceScan)
            {
                optimizedOffsetSeries.Points.Add(new DataPoint(item.Key, item.Value));
            }

            var xAxis = new LinearAxis(AxisPosition.Bottom, "Drift Time Scan")
            {
                Key = "DriftTimeScan"
            };

            var ticSeries = new LineSeries("Comparison TIC")
            {
                XAxisKey = "DriftTimeScan",
                YAxisKey = "TIC"
            };

            for (var i = 0; i < comparisonFrameData.Count; i++)
            {
                var scanNumber = i + scanStart;
                ticSeries.Points.Add(new DataPoint(scanNumber, comparisonFrameData[i]));
            }

            var yAxisTIC = new LinearAxis(AxisPosition.Right, "TIC")
            {
                Key = "TIC"
            };

            var yAxis = new LinearAxis(AxisPosition.Left, "Offset")
            {
                Key = "Offset"
            };

            offsetPlot.Series.Add(offsetSeries);
            offsetPlot.Series.Add(smoothedOffsetSeries);
            offsetPlot.Series.Add(optimizedOffsetSeries);
            offsetPlot.Series.Add(ticSeries);

            offsetPlot.Axes.Add(xAxis);
            offsetPlot.Axes.Add(yAxis);
            offsetPlot.Axes.Add(yAxisTIC);

            var visualizer = new DTWVisualization
            {
                Dtw = dtwAligner,
                Description = string.Format(
                    "{0} {1} with {2} points; max shift: {3} points",
                    dataSourceDescription, comparisonFrameNum, baseDataToUse.Count, sakoeChibaMaxShift),
                OffsetPlot = offsetPlot
            };

            if (Options.VisualizeDTW)
            {
                visualizer.ShowDialog();
            }
            else
            {
                visualizer.Show();
                visualizer.WindowState = WindowState.Minimized;
            }

            if (Options.SaveDTWPlots)
            {
                PngExporter.Export(offsetPlot.PlotControl.ActualModel, pngFileInfo.FullName, 1050, 650, OxyColors.White);
            }

            if (Options.SavePlotData)
            {
                var offsetsDataFilePath = Path.ChangeExtension(pngFileInfo.FullName, ".txt");
                WriteDTWPlotData(offsetsDataFilePath, offsetSeries, smoothedOffsetSeries, optimizedOffsetSeries, ticSeries);
            }

            if (!Options.VisualizeDTW)
            {
                visualizer.Hide();
            }
        }

        private void WriteFrameScansDebugFile(IEnumerable<ScanInfo> baseFrameScans, FileSystemInfo baseFrameFile)
        {
            using var debugWriter = new StreamWriter(new FileStream(baseFrameFile.FullName, FileMode.Create, FileAccess.Write));

            debugWriter.WriteLine("{0}\t{1}\t{2}\t{3}", "Scan", "DriftTime", "TIC", "BPI");

            var query = (from item in baseFrameScans orderby item.Scan select item);
            foreach (var item in query)
            {
                debugWriter.WriteLine("{0}\t{1}\t{2}\t{3}", item.Scan, item.DriftTime, item.TIC, item.BPI);
            }
        }

        private void WriteDTWPlotData(
            string offsetsDataFilePath,
            DataPointSeries offsetSeries,
            DataPointSeries smoothedOffsetSeries,
            DataPointSeries optimizedOffsetSeries,
            DataPointSeries ticSeries)
        {
            using var writer = new StreamWriter(new FileStream(offsetsDataFilePath, FileMode.Create, FileAccess.Write));

            writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", "Scan", "Offset", "SmoothedOffset", "OptimizedOffset", "TIC");

            var dataCount = offsetSeries.Points.Count;

            for (var i = 0; i < dataCount; i++)
            {
                writer.WriteLine(
                    "{0:0}\t{1:0}\t{2:0}\t{3:0}\t{4:0}",
                    offsetSeries.Points[i].X,
                    offsetSeries.Points[i].Y,
                    smoothedOffsetSeries.Points[i].Y,
                    optimizedOffsetSeries.Points[i].Y,
                    ticSeries.Points[i].Y
                );
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

        #region "Event Handlers"

        private void UIMFReader_ErrorEvent(object sender, UIMFLibrary.MessageEventArgs e)
        {
            ReportError("UIMFReader error: " + e.Message);
        }
        #endregion
    }
}
