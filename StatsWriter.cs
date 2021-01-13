using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PRISM;

namespace IMSDriftTimeAligner
{
    /// <summary>
    /// This class writes alignment stats to a text file
    /// </summary>
    public class StatsWriter : EventNotifier, IDisposable
    {
        // Ignore Spelling: Sakoe, Nums

        private string AverageScanShiftHeader { get; set; }

        private FrameAlignmentOptions Options { get; }

        private StreamWriter Writer { get; }

        /// <summary>
        /// Constructor without baseFrameList
        /// </summary>
        /// <param name="statsFilePath"></param>
        /// <param name="options"></param>
        /// <param name="commandLine"></param>
        public StatsWriter(
            string statsFilePath,
            FrameAlignmentOptions options,
            string commandLine) : this(statsFilePath, options, commandLine, new List<int>()) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="statsFilePath"></param>
        /// <param name="options"></param>
        /// <param name="commandLine"></param>
        /// <param name="baseFrameList"></param>
        public StatsWriter(
        string statsFilePath,
        FrameAlignmentOptions options,
        string commandLine,
        IReadOnlyCollection<int> baseFrameList)
        {
            Options = options;

            Writer = new StreamWriter(new FileStream(statsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };

            if (Options.WriteOptionsToStatsFile)
            {
                Writer.WriteLine(commandLine);
                Writer.WriteLine();
                Writer.WriteLine("== Processing Options ==");
                Writer.WriteLine();
                Writer.WriteLine("AlignmentMethod=" + Options.AlignmentMethod);
                Writer.WriteLine("BaseFrameMode=" + Options.BaseFrameSelectionMode + " (" + (int)Options.BaseFrameSelectionMode + ")");
                Writer.WriteLine("BaseCount=" + Options.BaseFrameSumCount);
                Writer.WriteLine("BaseStart=" + Options.BaseFrameStart);
                Writer.WriteLine("BaseEnd=" + Options.BaseFrameEnd);
                Writer.WriteLine("BaseFrameList=" + Options.BaseFrameList);
                if (baseFrameList.Count == 1)
                    Writer.WriteLine("ActualBaseFrameNum=" + baseFrameList.First());
                else if (baseFrameList.Count > 1)
                    Writer.WriteLine("ActualBaseFrameNums=" + string.Join(",", baseFrameList));

                Writer.WriteLine("FrameStart=" + Options.FrameStart);
                Writer.WriteLine("FrameEnd=" + Options.FrameEnd);
                Writer.WriteLine("MaxShift=" + Options.MaxShiftScans);
                if (Options.AlignmentMethod == FrameAlignmentOptions.AlignmentMethods.DynamicTimeWarping)
                {
                    Writer.WriteLine("DynamicTimeWarpingMaxPoints=" + Options.DTWMaxPoints);
                    Writer.WriteLine("DynamicTimeWarpingSakoeChibaMaxShiftPercent=" + Options.DTWSakoeChibaMaxShiftPercent);
                }
                Writer.WriteLine("MinimumIntensityThresholdFraction=" + Options.MinimumIntensityThresholdFraction);
                Writer.WriteLine("DriftScanFilterMin=" + Options.DriftScanFilterMin);
                Writer.WriteLine("DriftScanFilterMax=" + Options.DriftScanFilterMax);
                Writer.WriteLine("MzFilterMin=" + Options.MzFilterMin);
                Writer.WriteLine("MzFilterMax=" + Options.MzFilterMax);
                Writer.WriteLine("ScanSmoothCount=" + Options.ScanSmoothCount);
                Writer.WriteLine("MergeFrames=" + Options.MergeFrames);
                Writer.WriteLine("AppendMergedFrame=" + Options.AppendMergedFrame);
                Writer.WriteLine();
                Writer.WriteLine("== Alignment Stats ==");
                Writer.WriteLine();
            }
        }

        /// <summary>
        /// Append linear regression alignment stats for one frame
        /// </summary>
        /// <param name="comparisonFrameNum"></param>
        /// <param name="bestOffset"></param>
        /// <param name="bestRSquared"></param>
        public void AppendStats(int comparisonFrameNum, int bestOffset, double bestRSquared)
        {
            var statsLine = string.Format("{0,-8} {1,-6} {2,-8:F5}", comparisonFrameNum, bestOffset, bestRSquared);
            Writer.WriteLine(statsLine.Trim());
        }

        /// <summary>
        /// Append DTW alignment stats for one frame
        /// </summary>
        /// <param name="comparisonFrameNum"></param>
        /// <param name="cost"></param>
        /// <param name="offsetsByPercentile"></param>
        public void AppendStats(int comparisonFrameNum, double cost, Dictionary<int, List<int>> offsetsByPercentile)
        {
            var statsLine = new StringBuilder();

            string costFormatString;
            if (cost < 10)
                costFormatString = "{1,-8:0.00}";
            else if (cost < 100)
                costFormatString = "{1,-8:0.0}";
            else
                costFormatString = "{1,-8:#,##0}";

            statsLine.AppendFormat("{0,-8} " + costFormatString, comparisonFrameNum, cost);

            for (var percentile = 10; percentile <= 90; percentile += 10)
            {
                if (offsetsByPercentile.TryGetValue(percentile, out var offsetList))
                {
                    statsLine.AppendFormat(" {0,-6:0}", offsetList.Average());
                }
                else
                {
                    statsLine.AppendFormat(" {0,-6:0}", 0);
                }
            }

            OnStatusEvent("  " + AverageScanShiftHeader);
            OnStatusEvent("  " + statsLine.ToString().Trim());
            Writer.WriteLine(statsLine.ToString().Trim());
        }

        /// <summary>
        /// Write the header line
        /// </summary>
        public void WriteHeader()
        {
            if (Options.AlignmentMethod == FrameAlignmentOptions.AlignmentMethods.DynamicTimeWarping)
            {
                AverageScanShiftHeader = string.Format("{0,-8} {1,-8} {2,-6} {3,-6} {4,-6} {5,-6} {6,-6} {7,-6} {8,-6} {9,-6} {10,-6}",
                    "Frame", "Cost", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%");

                Writer.WriteLine("{0,-8} {1,-8} {2,-14} ", string.Empty, string.Empty, "Average scan shift by drift time scan percentile");
                Writer.WriteLine(AverageScanShiftHeader);

                Console.WriteLine();
                OnStatusEvent("For each frame, will display the average scan shift by drift time scan percentile");
            }
            else
            {
                AverageScanShiftHeader = string.Empty;
                Writer.WriteLine("{0,-8} {1,-6} {2,-8}", "Frame", "Shift", "Best RSquared");
            }
        }

        /// <summary>
        /// Closes the writer
        /// </summary>
        public void Dispose()
        {
            Writer.Close();
        }
    }
}
