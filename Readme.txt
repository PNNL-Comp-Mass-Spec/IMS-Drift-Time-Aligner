== IMSDriftTimeAligner ==

This program processes IMS data in a UIMF file to align all frames to a base frame, 
adjusting the observed drift times of each frame to align with the base frame.

== Program syntax ==

IMSDriftTimeAligner.exe
 InputFilePath [/O:OutputFilePath] [/Merge] [/Append]
 [/BaseFrame:N] [/BaseCount:N] [/BaseStart:N] [/BaseEnd:N]
 [/Start:N] [/End:N] [/MaxShift:N] [/Smooth:N] [/Debug]

InputFilePath is a path to the UIMF file to process

Use /O to specify the output file path
By default the output file will be named InputFileName_new.uimf

Use /Merge to specify that all of the aligned frames should be merged into a single frame by co-adding the data
Use /Append to specify that the data should be merged and appended as a new frame to the output file

Use /BaseFrame to specify how the base frame is selected; options are:
  /BaseFrame:0 for FirstFrame
  /BaseFrame:1 for MidpointFrame
  /BaseFrame:2 for MaxTICFrame
  /BaseFrame:3 for UserSpecifiedFrameRange
  /BaseFrame:4 for SumFirstNFrames
  /BaseFrame:5 for SumMidNFrames
  /BaseFrame:6 for SumFirstNPercent
  /BaseFrame:7 for SumMidNPercent
  /BaseFrame:8 for SumAll
The default is /BaseFrame:5

Use /BaseCount to specify the number or frames or percent range to use when FrameMode is NFrame or NPercent based;
  default is /BaseCount:15

Use /BaseStart and /BaseEnd to specify the range of frames to use as the base when using /BaseFrame:3 (aka UserSpecifiedFrameRange)

Use /Start and /End to limit the range of frames to align

Use /MaxShift to specify the maximum allowed shift (in scans) that scans in a frame can be adjusted;
  default is /MaxShift:150
Use /Smooth to specify the number of data points (scans) in the TIC to use for moving average smoothing 
prior to aligning each frame to the base frame;
  default is /Smooth:7

Use /Debug to show additional debug messages at the console

-------------------------------------------------------------------------------
Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2017

E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
Website: http://panomics.pnnl.gov/ or http://omics.pnl.gov or http://www.sysbio.org/resources/staff/
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0
