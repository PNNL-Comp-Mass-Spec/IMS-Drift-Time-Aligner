rem Option 1: use command line arguments
..\bin\IMSDriftTimeAligner.exe 20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1.uimf /BaseFrameMode:3 /BaseStart:35 /BaseEnd:35 /FrameStart:30 /FrameEnd:50 /Append /OutputFilePath:20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1_aligned.uimf

rem Option 2: use a parameter file
..\bin\IMSDriftTimeAligner.exe 20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1.uimf /ParamFile:AlignmentOptions_20180608_14_He_Mix4_p2p53_2p43.conf /OutputFilePath:20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1_aligned_Paramfile.uimf

..\bin\IMSDriftTimeAligner.exe 20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1.uimf /ParamFile:AlignmentOptions_20180608_14_He_Mix4_p2p53_2p43_DTW.conf /OutputFilePath:20180608_14_He_Mix4_p2p53_2p43_40kh_12v_1c_s750_1_aligned_DTW.uimf

pause

