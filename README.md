# IMS Drift Time Aligner

This program processes IMS data in a UIMF file to align all frames to a base frame, 
adjusting the observed drift times of each frame to align with the base frame.

## Console Switches

The IMS Drift Time Aligner is a console application, and must be run from the Windows command prompt.

```
IMSDriftTimeAligner.exe
 InputFilePath [/O:OutputFilePath] 
 [/Merge] [/Append]
 [/Align:Mode]
 [/BaseFrame:N] [/BaseCount:N] 
 [/BaseStart:N] [/BaseEnd:N]
 [/BaseFrameList:X,Y,Z]
 [/Start:N] [/End:N] 
 [/ScanMin:N] [/ScanMax:N]
 [/MzMin:N] [MzMax:N]
 [/MaxShift:N] [/Smooth:N]
 [/ITF:Fraction]
 [/DTWPoints:N] [/DTWShift:N]
 [/Vis] [/SavePlots]
 [/Debug] [/WO:False]
```

InputFilePath is a path to the UIMF file to process. Wildcards are also supported, for example *.uimf

Use `/O` or `-O` to specify the output file path
* By default the output file will be named InputFileName_new.uimf

Use `/Merge` or `-Merge` to specify that all of the aligned frames should be merged into a single frame by co-adding the data
* When this argument is provided, the output file will only contain the merged frame

Use `/Append` or `-Append` to specify that the data should be merged and appended as a new frame to the output file
* This option is only applicable if `/Merge` is not specified

Use `/Align` to define the alignment mode
* `/Align:0` means linear regression and simple shifting of all data in a frame by the same number of scans
* `/Align:1` means dynamic time warping, which provides for a non-linear shift of scans in a frame

Use `/BaseFrame` or `/BaseFrameMode` to specify how the base frame is selected; options are:

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
* Only valid when using `/BaseFrame:3` (aka UserSpecifiedFrameRange)

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
below which intensity values will be set to 0.
* Defaults to 0.10  (aka 10% of the max) for `/Align:0`  (Linear Regression)
* Defaults to 0.025 (aka 2.5% of the max) for `/Align:1` (Dynamic Time Warping)

Use `/DTWPoints` to define the maximum number of points to use for Dynamic Time Warping
* Defaults to 7500
* Use smaller values if you run out of memory (55,000 drift scans and a DTWPoints value of 7500 requires 20 GB of memory)

Use `/DTWShift` to define the maximum Sakoe Chiba shift
* The value is a percentage of the number of points used for Dynamic Time Warping
* Default is 5 (aka 5%)

Use `/Vis` or `/Plot` to visualize the dynamic time warping results for each aligned frame

Use `/SavePlot` or `/SavePlots` to create a .png file visualizing dynamic time warping offsets for each frame

Use `/Debug` to show additional debug messages at the console

By default, the processing options will be included in the alignment stats file (Dataset_stats.txt).
This can be disabled with `/WO:False`

## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/

## License

IMSDriftTimeAligner is licensed under the Apache License, Version 2.0; you may not use this 
file except in compliance with the License.  You may obtain a copy of the 
License at https://opensource.org/licenses/Apache-2.0
