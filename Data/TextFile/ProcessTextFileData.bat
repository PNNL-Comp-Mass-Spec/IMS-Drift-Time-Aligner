@echo off

set ExePath=IMSDriftTimeAligner.exe
if exist ..\IMSDriftTimeAligner.exe        (set ExePath=..\IMSDriftTimeAligner.exe)
if exist ..\..\bin\IMSDriftTimeAligner.exe (set ExePath=..\..\bin\IMSDriftTimeAligner.exe)

@echo on
%ExePath% AlignmentTestData1.txt /conf:LinearRegressionOptions.conf /output:LinearRegression\Placeholder.txt

%ExePath% AlignmentTestData1.txt /conf:DTWOptions_ITF_10pct.conf /output:DTW_ITF_10pct\Placeholder.txt

%ExePath% AlignmentTestData1.txt /conf:DTWOptions_ITF_15pct.conf /output:DTW_ITF_15pct\Placeholder.txt

pause
