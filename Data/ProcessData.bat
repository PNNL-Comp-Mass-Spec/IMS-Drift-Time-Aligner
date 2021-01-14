@echo off

set ExePath=IMSDriftTimeAligner.exe
if exist ..\IMSDriftTimeAligner.exe     (set ExePath=..\IMSDriftTimeAligner.exe)
if exist ..\bin\IMSDriftTimeAligner.exe (set ExePath=..\bin\IMSDriftTimeAligner.exe)

echo Set options using command line arguments
@echo on
%ExePath% 20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1.uimf /BaseFrameMode:3 /BaseStart:35 /BaseEnd:35 /FrameStart:30 /FrameEnd:50 /Append /OutputFilePath:20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1_aligned.uimf | tee IMSDriftTimeAligner_ConsoleOutput_20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1_CmdLine.txt

@echo off
echo Set options using a parameter file; linear alignment
@echo on
%ExePath% 20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1.uimf /ParamFile:AlignmentOptions_20180608_14_He_Mix4_p2p53_2p43.conf /OutputFilePath:20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1_aligned_Paramfile.uimf | tee IMSDriftTimeAligner_ConsoleOutput_20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1_Linear.txt

@echo off
echo Set options using a parameter file; DTW
@echo on
%ExePath% 20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1.uimf /ParamFile:AlignmentOptions_20180608_14_He_Mix4_p2p53_2p43_DTW.conf /OutputFilePath:20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1_aligned_DTW.uimf   | tee IMSDriftTimeAligner_ConsoleOutput_20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1_DTW.txt

@echo off
echo Demonstrate loading data from a text file

if exist Column1.txt del Column1.txt
if exist Column2.txt del Column2.txt

@echo on
%ExePath% TextFile\AlignmentTestData1.txt /Align:1 /ITF:0.1 /Plot /SavePlot /ITF:0.002  /O:.   | tee IMSDriftTimeAligner_ConsoleOutput_AlignmentTestData1_TextFile_DTW.txt

pause
