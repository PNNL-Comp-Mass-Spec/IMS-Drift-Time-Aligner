@echo off

set ExePath=IMSDriftTimeAligner.exe
if exist ..\IMSDriftTimeAligner.exe        (set ExePath=..\IMSDriftTimeAligner.exe)
if exist ..\..\bin\IMSDriftTimeAligner.exe (set ExePath=..\..\bin\IMSDriftTimeAligner.exe)

if exist LinearRegression\Column1.txt   del LinearRegression\Column1.txt
if exist LinearRegression\Column2.txt   del LinearRegression\Column2.txt
if exist LinearRegression\DebugData.txt del LinearRegression\DebugData.txt

if exist DTW_ITF_10pct\Column1.txt      del DTW_ITF_10pct\Column1.txt
if exist DTW_ITF_10pct\Column2.txt      del DTW_ITF_10pct\Column2.txt
if exist DTW_ITF_10pct\DebugData.txt    del DTW_ITF_10pct\DebugData.txt
if exist DTW_ITF_10pct\DebugDataDTW.txt del DTW_ITF_10pct\DebugDataDTW.txt


if exist DTW_ITF_15pct\Column1.txt      del DTW_ITF_15pct\Column1.txt
if exist DTW_ITF_15pct\Column2.txt      del DTW_ITF_15pct\Column2.txt
if exist DTW_ITF_15pct\DebugData.txt    del DTW_ITF_15pct\DebugData.txt
if exist DTW_ITF_15pct\DebugDataDTW.txt del DTW_ITF_15pct\DebugDataDTW.txt

@echo on
%ExePath% AlignmentTestData1.txt /conf:LinearRegressionOptions.conf /output:LinearRegression\Placeholder.txt

%ExePath% AlignmentTestData1.txt /conf:DTWOptions_ITF_10pct.conf /output:DTW_ITF_10pct\Placeholder.txt

%ExePath% AlignmentTestData1.txt /conf:DTWOptions_ITF_15pct.conf /output:DTW_ITF_15pct\Placeholder.txt

pause
