# IMS Drift Time Aligner

This program processes IMS data in a UIMF file to align all frames to a base frame, 
adjusting the observed drift times of each frame to align with the base frame.

## Console Switches

The IMS Drift Time Aligner is a console application, and must be run from the Windows command prompt.

```
IMSDriftTimeAligner.exe
 InputFilePath [/O:OutputFilePath] [/Merge] [/Append]
 [/BaseFrame:N] [/BaseCount:N] [/BaseStart:N] [/BaseEnd:N]
 [/Start:N] [/End:N] 
 [/ScanMin:N] [/ScanMax:N]
 [/MzMin:N] [MzMax:N]
 [/MaxShift:N] [/Smooth:N] 
 [/Debug] [/WO:False]
```

InputFilePath is a path to the UIMF file to process.
Wildcards are also supported, for example *.uimf

Use `/O` to specify the output file path
By default the output file will be named InputFileName_new.uimf

Use `/Merge` to specify that all of the aligned frames should be merged into a single frame by co-adding the data
Use `/Append` to specify that the data should be merged and appended as a new frame to the output file

Use `/BaseFrame` to specify how the base frame is selected; options are:
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

Use `/BaseStart` and /BaseEnd to specify the range of frames to use as the base when using /BaseFrame:3 (aka UserSpecifiedFrameRange)

Use `/Start` and `/End` to limit the range of frames to align

Use `/ScanMin` and `/ScanMax` to limit the range of drift scans to use for generation of the TIC data to align

Use `/MzMin` and `/MzMax` to limit the range of m/z values to use for generation of the TIC data to align, for example:
* `/MzMin:170 /MzMax:190`

Use `/MaxShift` to specify the maximum allowed shift (in scans) that scans in a frame can be adjusted;
* Default is `/MaxShift:150`

Use `/Smooth` to specify the number of data points (scans) in the TIC to use for moving average smoothing 
prior to aligning each frame to the base frame;
* Default is `/Smooth:7`

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
