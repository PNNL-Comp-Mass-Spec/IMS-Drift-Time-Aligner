# IMS Drift Time Aligner

This program processes IMS data in a UIMF file to align all frames to a base frame, 
adjusting the observed drift times of each frame to align with the base frame.

The input file can also be a tab-delimited text file with two or more columns of data to align.
Intensity values in the second column will be aligned to intensity values in the first column.
If additional columns of data are included, they will also be aligned to the first column.

## Console Switches

The IMS Drift Time Aligner is a console application, and must be run from the Windows command prompt.

```
IMSDriftTimeAligner.exe
 InputFilePath [/O:OutputFilePath] [/S]
 [/Merge] [/Append]
 [/Align:Mode]
 [/BaseFrameMode:N] [/BaseCount:N] 
 [/BaseStart:N] [/BaseEnd:N]
 [/BaseFrameList:X,Y,Z]
 [/Start:N] [/End:N] 
 [/ScanMin:N] [/ScanMax:N]
 [/MzMin:N] [MzMax:N]
 [/MaxShift:N] [/Smooth:N]
 [/ITF:Fraction]
 [/DTWPoints:N] [/DTWShift:N]
 [/Vis] [/SavePlots] [/SavePlotData]
 [/Debug] [/Preview] [/WO:False]
 [/ParamFile:ParamFileName.conf] [/CreateParamFile]
```

InputFilePath is a path to the UIMF or tab-delimited file to process
* Wildcards are also supported, for example *.uimf or *.txt
* Text files should be tab-delimited with two or more intensity values per row

Use `/O` or `-O` to specify the output file path
* By default the output file will be named InputFileName_new.uimf

Use `/S` or `/Recurse` to find matching files in the current directory and subdirectories
* Useful when using a wildcard to find UIMF files
* `/O` is ignored when `/S` is provided
  * Output files will be auto-named and will be created in the same directory as the input file

Use `/Merge` or `-Merge` to specify that all of the aligned frames should be merged into a single frame by co-adding the data
* When this argument is provided, the output file will only contain the merged frame

Use `/Append` or `-Append` to specify that the data should be merged and appended as a new frame to the output file
* This option is only applicable if `/Merge` is not specified

Use `/Align` to define the alignment mode
* `/Align:0` or `/Align:LinearRegression` means linear regression and simple shifting of all data in a frame by the same number of scans
* `/Align:1` or `/Align:DynamicTimeWarping` means dynamic time warping, which provides for a non-linear shift of scans in a frame

Use `/BaseFrameMode` or `/BaseFrame` to specify how the base frame is selected; options are:

| Mode            | Description             |
|-----------------|-------------------------|
|  `/BaseFrame:0` |FirstFrame               |
|  `/BaseFrame:1` |MidpointFrame            |
|  `/BaseFrame:2` |MaxTICFrame              |
|  `/BaseFrame:3` |UserSpecifiedFrameRange  |
|  `/BaseFrame:4` |SumFirstNFrames          |
|  `/BaseFrame:5` |SumMidNFrames            |
|  `/BaseFrame:6` |SumFirstNPercent         |
|  `/BaseFrame:7` |SumMidNPercent           |
|  `/BaseFrame:8` |SumAll                   |

The default is `/BaseFrame:5`

Use `/BaseCount` to specify the number or frames or percent range to use when FrameMode is NFrame or NPercent based;
* Default is `/BaseCount:15`

Use `/BaseStart` and `/BaseEnd` to specify the range of frames to use as the base
* For .UIMF files, these are only valid when using `/BaseFrame:3` (aka UserSpecifiedFrameRange)
* `/BaseStart` can also be used to specify which column in a tab-delimited text file is the base column for alignment
  * The first column is column 1
  * To define column 8 as the base column, use `/BaseStart:8`

Use `/BaseFrameList` to define a comma separated list of frame numbers to use as the base frame
* Only valid when using `/BaseFrame:3` (aka UserSpecifiedFrameRange)

Use `/Start` and `/End` to limit the range of frames to align

Use `/ScanMin` and `/ScanMax` to limit the range of drift scans to use for generation of the TIC data to align

Use `/MzMin` and `/MzMax` to limit the range of m/z values to use for generation of the TIC data to align, for example:
* `/MzMin:170 /MzMax:190`

Use `/MaxShift` to specify the maximum allowed shift (in scans) that scans in a frame can be adjusted;
* Default is `/MaxShift:150`
* Only used when the alignment mode is 0 (Linear Regression). 
  * For Dynamic Time Warping (`/Align:1`) use `DTWShift` to define the maximum shift limits

Use `/Smooth` to specify the number of data points (scans) in the TIC to use for moving average smoothing 
prior to aligning each frame to the base frame;
* Default is `/Smooth:7`
 
Use `/ITF` to define the value to multiply the maximum TIC value by to determine an intensity threshold, 
below which intensity values will be set to 0
* Defaults to 0.10  (aka 10% of the max) for `/Align:0`  (Linear Regression)
* Defaults to 0.025 (aka 2.5% of the max) for `/Align:1` (Dynamic Time Warping)

Use `/DTWPoints` to define the maximum number of points to use for Dynamic Time Warping
* Defaults to 7500
* Use smaller values if you run out of memory (55,000 drift scans and a DTWPoints value of 7500 requires 20 GB of memory)

Use `/DTWShift` to define the maximum Sakoe Chiba shift
* The value is a percentage of the number of points used for Dynamic Time Warping
* Default is 5 (aka 5%)

Use `/Vis` or `/Plot` to visualize the dynamic time warping results for each aligned frame
* Shows interactive plots in a new window
* Zoom in by drawing square box using the middle mouse button; alternatively, use Ctrl + Alt + Left Mouse Button
* Zooom out by double clicking with the middle mouse button; alternatively, double click with Ctrl + Alt + Left Mouse Button

Use `/SavePlot` or `/SavePlots` to create a .png file visualizing dynamic time warping offsets for each frame

Use `/SavePlotData` to save the dynamic time warping offset data (plus TIC values) to a separate tab-delimited text file for each frame

Use `/Debug` to show additional debug messages at the console

Use `/Preview` to preview the file (or files) that would be processed
* Useful when using `/S`

By default, the processing options will be included in the alignment stats file (Dataset_stats.txt)
* This can be disabled with `/WO:False` or `/WriteOptions:False`

The processing options can be specified in a parameter file using `/ParamFile:Options.conf` or `/Conf:Options.conf`
* Define options using the format `ArgumentName=Value`
* Lines starting with `#` or `;` will be treated as comments
* Additional arguments on the command line can supplement or override the arguments in the parameter file

Use `/CreateParamFile` to create an example parameter file
* By default, the example parameter file content is shown at the console
* To create a file named Options.conf, use `/CreateParamFile:Options.conf`

## Contacts

Written by Matthew Monroe for PNNL (Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/

## License

IMSDriftTimeAligner is licensed under the Apache License, Version 2.0; you may not use this 
file except in compliance with the License.  You may obtain a copy of the 
License at https://opensource.org/licenses/Apache-2.0
